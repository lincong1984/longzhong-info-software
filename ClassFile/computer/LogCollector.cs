using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;

namespace MES_SCAF
{
    /// <summary>
    /// 全局线程安全日志收集器。
    ///
    /// ============================================================
    /// 【为什么需要它？】
    /// ============================================================
    /// 原代码里到处都是：
    ///     publicAddress.errtext += $"业务X异常: {ex.Message}\r\n";
    ///
    /// errtext 是 string（不可变），每次 += 都要：
    ///   1. 分配新 string（长度 = 旧长度 + 新增长度）
    ///   2. 复制旧内容
    ///   3. 复制新内容
    ///
    /// 按现场日志估算，8 小时运行后 errtext 累积到 10~15MB。
    /// 此时每秒 50 次 += 就是 750MB/s 的内存拷贝，
    /// CPU 直接满载、GC 疯狂回收 —— 表现就是"所有站同时卡顿"。
    ///
    /// ============================================================
    /// 【本类方案】
    /// ============================================================
    /// 1. Write() 只做"入队 + 加时间戳"，O(1)，立即返回
    /// 2. 后台 Timer 每秒 Flush 一次，批量拼成一个大字符串
    /// 3. 一次 Flush 只调用一次 WriteReadLog.WriteLog（复用现有落盘逻辑）
    /// 4. 文件路径、GB2312 编码、Log/yyyy-MM/log_yyyy-MM-dd.txt 格式完全不变
    ///    → 界面 ReadLogReversed / AddTxtToList 仍能正常读日志
    /// 5. 队列上限 10 万条，超了丢最旧 10%
    /// 6. AppDomain.ProcessExit 兜底 Flush，进程退出不丢最后一批
    ///
    /// ============================================================
    /// 【时间戳说明】
    /// ============================================================
    /// 入队时就用 DateTime.Now（毫秒精度）加前缀：
    ///   "2026-10-21 14:05:30.103: [saveResults-入口] ..."
    /// WriteReadLog.WriteLog 检测到行首已有时间戳，保留原样，不再覆盖。
    /// 好处：时间戳 = 事件真实发生时间，不是落盘时间。
    /// ============================================================
    /// </summary>
    public static class LogCollector
    {
        // ==================== 常量 ====================

        /// <summary>
        /// 队列上限。
        /// 超过此值后，一次性丢弃最旧的 10%，防止内存爆掉。
        /// </summary>
        private const int MAX_QUEUE_SIZE = 100_000;

        /// <summary>
        /// 单次 Flush 最多拼接的字符数。
        /// 控制单次文件 I/O 时长（一次写 256KB，约 1~5ms，非常安全）。
        /// </summary>
        private const int MAX_FLUSH_CHARS = 256 * 1024;

        /// <summary>
        /// 队列长度达到此值时立即触发一次 Flush（不等到 1 秒定时器）。
        /// 用于突发日志场景（例如 20 台 PLC 同时重连）。
        /// </summary>
        private const int FLUSH_TRIGGER_COUNT = 150;

        // ==================== 内部状态 ====================

        /// <summary>日志队列（无锁并发，多线程同时 Write 安全）</summary>
        private static readonly ConcurrentQueue<string> _queue = new ConcurrentQueue<string>();

        /// <summary>Flush 重入保护（0=空闲，1=正在执行）</summary>
        private static int _flushing = 0;

        /// <summary>累计丢弃的日志条数（用于诊断"日志太多被丢"的现场）</summary>
        private static long _droppedCount = 0;

        /// <summary>
        /// 落盘委托（复用现有 WriteReadLog）。
        /// 用字段而不是每次 new，避免大量临时对象。
        /// </summary>
        private static readonly WriteReadLog _writer = new WriteReadLog();

        // ==================== 公开属性 ====================

        /// <summary>当前待写入队列的日志条数</summary>
        public static int PendingCount => _queue.Count;

        /// <summary>累计丢弃的日志条数</summary>
        public static long DroppedCount => Interlocked.Read(ref _droppedCount);

        // ==================== 静态构造 ====================

        /// <summary>
        /// 静态构造函数：初始化后台 Flush 定时器 + 进程退出兜底。
        /// 只在首次访问本类时执行一次，全部开销 &lt; 1ms。
        /// </summary>
        static LogCollector()
        {
            // ========== 每秒 Flush 一次 ==========
            // Timer 用线程池线程，但 Flush 内部只是拼串 + 一次 AppendAllText（毫秒级），
            // 对业务线程池的影响可忽略。
            // 参数：回调、状态对象、首次延迟 1000ms、周期 1000ms
            var timer = new Timer(_ => Flush(), null, 1000, 1000);

            // ========== 进程退出兜底 ==========
            // 避免最后 1 秒的日志留在内存里没写盘
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                try
                {
                    // 最多尝试 2 秒，避免进程退出卡太久
                    var deadline = DateTime.Now.AddSeconds(2);
                    while (_queue.Count > 0 && DateTime.Now < deadline)
                    {
                        Flush();
                        // 队列还有剩余就短暂让步，让 Flush 有机会完成
                        if (_queue.Count > 0) Thread.Sleep(50);
                    }
                }
                catch { }   // 退出兜底不允许抛异常
            };
        }

        // ==================== 公开方法 ====================

        /// <summary>
        /// ★★★ 就是你问的 Write 方法 ★★★
        ///
        /// 写入一条日志（线程安全，立即返回，不阻塞调用方）。
        ///
        /// 【本方法是 publicAddress.AddLog 的实际实现】
        /// 与原来 `errtext +=` 的关键区别：
        ///   - errtext += ：O(n) 字符串复制，调用方被拖慢
        ///   - Write()    ：O(1) 入队 + 毫秒时间戳前缀，调用方立即返回
        ///
        /// 真正的磁盘 I/O 由后台 Timer 批量完成。
        /// </summary>
        /// <param name="message">日志内容（不需要时间戳，不需要末尾换行）</param>
        public static void Write(string message)
        {
            // 空消息直接忽略（避免写空行）
            if (string.IsNullOrEmpty(message)) return;

            try
            {
                // ========== 队列超限保护 ==========
                // 一次丢弃 10% 的量，比"一次丢一条"更高效
                if (_queue.Count >= MAX_QUEUE_SIZE)
                {
                    int toDrop = MAX_QUEUE_SIZE / 10;
                    // TryDequeue 出队丢弃；返回 false 说明队列已空，提前结束
                    for (int i = 0; i < toDrop && _queue.TryDequeue(out _); i++) { }
                    // 累加丢弃计数（原子操作）
                    Interlocked.Add(ref _droppedCount, toDrop);
                }

                // ========== 入队时加时间戳（毫秒精度） ==========
                // 格式：yyyy-MM-dd HH:mm:ss.fff: 内容
                // WriteReadLog.WriteLog 检测到行首已有时间戳，保留原样、不再覆盖
                // 好处：时间戳 = 事件真实发生时间，不是落盘时间
                string stamped = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + ": " + message;
                _queue.Enqueue(stamped);

                // ========== 突发触发 ==========
                // 队列达到阈值时立即 Flush 一次，避免突发日志延迟 1 秒
                if (_queue.Count >= FLUSH_TRIGGER_COUNT)
                {
                    Flush();
                }
            }
            catch
            {
                // 日志系统本身不能抛异常，否则会挂掉调用方
            }
        }

        /// <summary>
        /// 立即把队列内容刷到文件（同步）。
        /// 一般由定时器调用，也可手动调用（例如程序退出前）。
        /// </summary>
        public static void Flush()
        {
            // 防重入：上一次 Flush 还没结束时，本次直接返回
            // CompareExchange 原子地把 0 换成 1；返回值 != 0 说明已有其它线程在执行
            if (Interlocked.CompareExchange(ref _flushing, 1, 0) != 0) return;

            try
            {
                // 预分配 256KB 的 StringBuilder，减少扩容
                var sb = new StringBuilder(MAX_FLUSH_CHARS);

                // 批量出队（每次最多 256KB）
                while (sb.Length < MAX_FLUSH_CHARS && _queue.TryDequeue(out var line))
                {
                    sb.AppendLine(line);
                }

                // 本次没有出队到任何内容，直接返回
                if (sb.Length == 0) return;

                // 落盘（复用 WriteReadLog）
                _writer.WriteLog(sb.ToString());
            }
            catch (Exception ex)
            {
                // Flush 异常不能影响业务，仅打印控制台
                Console.WriteLine($"[LogCollector] Flush 异常: {ex.Message}");
            }
            finally
            {
                // 无论成功失败，都要释放重入标志
                Interlocked.Exchange(ref _flushing, 0);
            }
        }

        /// <summary>
        /// 立即写一条日志到文件（不走队列），用于"关键事件必须立刻落盘"的场景。
        /// 【性能开销】约 0.1~1ms，不要高频调用。
        /// </summary>
        public static void WriteImmediate(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            try
            {
                // 直接同步写盘，加上毫秒时间戳
                _writer.WriteLog(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + ": " + message);
            }
            catch { }   // 立即写也吞异常，不影响业务
        }
    }
}