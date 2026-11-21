using System;
using System.Collections.Concurrent;
using System.Threading;

namespace MES_SCAF
{
    /// <summary>
    /// 心跳状态枚举
    /// </summary>
    public enum HeartbeatStatus
    {
        /// <summary>初始状态，尚未确定（刚启动或刚 Reset）</summary>
        Unknown,

        /// <summary>在线（连续成功达到阈值）</summary>
        Online,

        /// <summary>离线（连续失败达到阈值，或超时未收到心跳）</summary>
        Offline,

        /// <summary>抖动（频繁切换，目前保留供扩展）</summary>
        Flapping
    }

    /// <summary>
    /// 心跳状态变化事件参数
    /// </summary>
    public class HeartbeatStatusChangedEventArgs : EventArgs
    {
        /// <summary>新状态</summary>
        public HeartbeatStatus NewStatus { get; set; }

        /// <summary>旧状态</summary>
        public HeartbeatStatus OldStatus { get; set; }

        /// <summary>状态变化时间</summary>
        public DateTime ChangedTime { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 全局心跳监控器（每个 IP:Port 一个实例）。
    /// 
    /// ============================================================
    /// 【非对称阈值设计】
    /// ============================================================
    /// 判离线用大阈值（默认 10）：防止网络抖动导致误判
    /// 判在线用小阈值（默认 3） ：让网络一恢复就快速切回 Online，业务立即恢复
    /// 
    /// 【为什么不用同一个阈值？】
    /// - 用同一个大阈值（10）：切 Offline 稳，但切 Online 慢
    ///   （10% 丢包下，连续 10 次成功概率 ≈ 35%，平均要等 14 秒才能切回 Online）
    /// - 用同一个小阈值（3）：切 Online 快，但切 Offline 容易误判
    /// 
    /// 【事件触发锁外】
    /// 事件触发（StatusChanged / HeartbeatReceived）全部移到锁外，
    /// 避免事件处理链（例如 PLCService 的 _offlineLock）反向持有监控器锁，
    /// 造成状态机抖动或潜在死锁。
    /// 
    /// ============================================================
    /// 【本次关键修复：CheckTimeout 定时器周期】
    /// ============================================================
    /// 原代码：new Timer(CheckTimeout, null, _heartbeatTimeoutMs, _heartbeatTimeoutMs)
    ///         → 检查周期 = 超时时间（20000ms），判离线实际延迟 20~40s
    /// 修复后：new Timer(CheckTimeout, null, _heartbeatTimeoutMs, CHECK_TIMEOUT_PERIOD_MS)
    ///         → 检查周期 = 500ms，判离线实际延迟 20~20.5s
    /// 
    /// 【为什么原设计是缺陷？】
    /// 定时器是离散采样，超时是连续时间。检查周期 = 超时时间时：
    /// - 最后一次心跳恰好落在定时器触发前一刻 → 延迟 ≈ 20000ms（最好）
    /// - 最后一次心跳恰好落在定时器触发后一刻 → 延迟 ≈ 40000ms（最坏）
    /// 而心跳连接断开时 ReportHeartbeatFailure 根本不会被调用（HeartbeatLoop 里
    /// EnsureHeartbeatConnectionAsync 返回 false 就 continue 了），判离线完全依赖
    /// CheckTimeout 兜底，于是这 20~40s 的量化误差直接进入业务停摆时长。
    /// ============================================================
    /// </summary>
    public class HeartbeatMonitor
    {
        // ==================== 定时器检查周期常量 ====================

        /// <summary>
        /// CheckTimeout 定时器的检查周期（毫秒）。
        ///
        /// 【为什么不能等于超时时间？】
        /// 定时器是离散采样，超时是连续时间。如果检查周期 = 超时时间（20000ms），
        /// 判离线实际延迟会在 [20000, 40000]ms 之间波动：
        /// - 最后一次心跳恰好落在定时器触发前一刻 → 延迟 ≈ 20000ms（最好）
        /// - 最后一次心跳恰好落在定时器触发后一刻 → 延迟 ≈ 40000ms（最坏）
        ///
        /// 【为什么是 500ms？】
        /// - 与 HeartbeatConfig.HeartbeatReadIntervalMs（心跳读取间隔）对齐，
        ///   判离线延迟收敛到 [超时时间, 超时时间 + 500ms]。
        /// - 回调只做一次 DateTime 减法和一次状态比较（纳秒级），
        ///   每秒 2 次回调的 CPU 开销可忽略。
        /// - 如果现场对离线响应更敏感，可降到 200ms；
        ///   如果想让日志更安静，可放宽到 1000ms（延迟 ≤ 超时时间 + 1000ms）。
        /// </summary>
        private const int CHECK_TIMEOUT_PERIOD_MS = 500;

        // ==================== 基础字段 ====================

        /// <summary>PLC IP</summary>
        private readonly string _ip;

        /// <summary>PLC 端口</summary>
        private readonly int _port;

        /// <summary>状态与计数器的同步锁（只保护内部字段，不用于触发事件）</summary>
        private readonly object _lock = new object();

        // ==================== 连续成功/失败计数器 ====================

        /// <summary>连续成功次数（每次成功 +1，失败归零）</summary>
        private int _consecutiveSuccess = 0;

        /// <summary>连续失败次数（每次失败 +1，成功归零）</summary>
        private int _consecutiveFailure = 0;

        // ==================== 配置项（非对称） ====================

        /// <summary>
        /// ★ 判【离线】阈值：连续失败达到该次数才切 Offline。
        /// 值越大 → 越不容易误判，但对真宕机响应越慢。
        /// </summary>
        private readonly int _requiredConsecutiveOffline;

        /// <summary>
        /// ★ 判【在线】阈值：连续成功达到该次数才切 Online。
        /// 值越小 → 网络恢复后越快切 Online，业务恢复越快。
        /// 必须 ≤ _requiredConsecutiveOffline。
        /// </summary>
        private readonly int _requiredConsecutiveOnline;

        /// <summary>【可配置】超时阈值（毫秒）</summary>
        private readonly int _heartbeatTimeoutMs;

        // ==================== 运行状态 ====================

        /// <summary>当前稳定状态（Online/Offline/Unknown）</summary>
        private HeartbeatStatus _stableStatus = HeartbeatStatus.Unknown;

        /// <summary>最后收到心跳包的时间（用于超时检测）</summary>
        private DateTime _lastHeartbeatTime = DateTime.MinValue;

        /// <summary>定时检查超时的定时器</summary>
        private Timer _timeoutTimer;

        /// <summary>释放标志，避免 Dispose 之后定时器回调仍触发事件</summary>
        private volatile bool _disposed = false;

        // ==================== 事件 ====================

        /// <summary>
        /// 状态变化事件（可用于 UI 更新）。
        /// 仅当 Online / Offline / Unknown 之间发生切换时触发。
        /// </summary>
        public event EventHandler<HeartbeatStatusChangedEventArgs> StatusChanged;

        /// <summary>
        /// 每次成功读取到心跳包时触发（无论状态是否变化）。
        /// 用于业务侧重置离线计时器等。
        /// </summary>
        public event Action HeartbeatReceived;

        // ==================== 只读属性 ====================

        /// <summary>唯一标识 Key（IP:Port）</summary>
        public string Key => $"{_ip}:{_port}";

        /// <summary>当前稳定状态</summary>
        public HeartbeatStatus CurrentStatus => _stableStatus;

        /// <summary>是否在线（CurrentStatus == Online）</summary>
        public bool IsOnline => _stableStatus == HeartbeatStatus.Online;

        /// <summary>【只读】判离线阈值</summary>
        public int RequiredConsecutive => _requiredConsecutiveOffline;

        /// <summary>【只读】判在线阈值</summary>
        public int RequiredConsecutiveOnline => _requiredConsecutiveOnline;

        /// <summary>【只读】超时时间（毫秒）</summary>
        public int HeartbeatTimeoutMs => _heartbeatTimeoutMs;

        // ==================== 构造函数 ====================

        /// <summary>
        /// 构造函数（带配置参数）。
        /// 区分【判离线阈值】和【判在线阈值】。
        /// </summary>
        /// <param name="ip">PLC IP</param>
        /// <param name="port">PLC 端口</param>
        /// <param name="requiredConsecutive">判离线连续失败阈值（默认从 HeartbeatConfig 读）</param>
        /// <param name="heartbeatTimeoutMs">超时时间（毫秒，默认从 HeartbeatConfig 读）</param>
        /// <param name="requiredConsecutiveOnline">
        /// 判在线连续成功阈值（可选）。
        /// 不传则从 HeartbeatConfig.RequiredConsecutiveOnline 读（默认 3）。
        /// </param>
        public HeartbeatMonitor(string ip, int port,
            int? requiredConsecutive = null,
            int? heartbeatTimeoutMs = null,
            int? requiredConsecutiveOnline = null)
        {
            _ip = ip;
            _port = port;

            // ★ 判离线阈值：优先使用传入参数，其次使用全局配置，最后兜底 10
            _requiredConsecutiveOffline = requiredConsecutive ?? HeartbeatConfig.RequiredConsecutive;
            if (_requiredConsecutiveOffline <= 0) _requiredConsecutiveOffline = 10;

            // ★ 判在线阈值：优先使用传入参数，其次使用全局配置，最后兜底 3
            _requiredConsecutiveOnline = requiredConsecutiveOnline ?? HeartbeatConfig.RequiredConsecutiveOnline;
            if (_requiredConsecutiveOnline <= 0) _requiredConsecutiveOnline = 3;

            // ★ 一致性约束：判在线阈值不能超过判离线阈值
            //   否则可能出现"网络恢复也要等很久才能切 Online"的尴尬
            if (_requiredConsecutiveOnline > _requiredConsecutiveOffline)
                _requiredConsecutiveOnline = Math.Max(1, _requiredConsecutiveOffline / 3);

            _heartbeatTimeoutMs = heartbeatTimeoutMs ?? HeartbeatConfig.HeartbeatTimeoutMs;
            if (_heartbeatTimeoutMs <= 0) _heartbeatTimeoutMs = 20000;

            _lastHeartbeatTime = DateTime.Now;

            // ============================================================
            // ★ 改动：定时器检查周期从 _heartbeatTimeoutMs（20000ms）
            //        改为 CHECK_TIMEOUT_PERIOD_MS（500ms）。
            //
            // 原代码：
            //   _timeoutTimer = new Timer(CheckTimeout, null,
            //       _heartbeatTimeoutMs, _heartbeatTimeoutMs);
            //
            // 问题：检查周期 = 超时时间，判离线实际延迟 20~40s（最坏 2 倍）。
            //
            // 修复后：首次仍等一个完整超时（dueTime = _heartbeatTimeoutMs），
            //        之后每 500ms 检查一次，判离线延迟收敛到 20~20.5s。
            // ============================================================
            _timeoutTimer = new Timer(CheckTimeout, null,
                _heartbeatTimeoutMs,          // dueTime：首次等 20s，避免刚启动就判离线
                CHECK_TIMEOUT_PERIOD_MS);     // period ：之后每 500ms 检查一次
        }

        // ==================== 上报接口 ====================

        /// <summary>
        /// 上报一次成功读取到的心跳包。
        /// 
        /// 【流程】
        /// 1. 锁内：更新 _lastHeartbeatTime，累加成功计数，清零失败计数，
        ///    判断是否达到【判在线阈值】（非对称设计，用小阈值 3）。
        /// 2. 锁外：依次触发 HeartbeatReceived 事件和 StatusChanged 事件。
        /// 
        /// 【为什么事件要放锁外？】
        /// 事件订阅方（PLCService.OnHeartbeatReceived）内部会获取 _offlineLock，
        /// 而 HeartbeatLoop 会先获取 _offlineLock 再获取 _heartbeatSem，
        /// 如果事件在锁内触发，就会出现"监控器锁 → _offlineLock"与"_offlineLock → 业务锁"的锁顺序交叉，
        /// 一旦并发就会出现长时间阻塞甚至死锁。放在锁外可完全消除该风险。
        /// </summary>
        public void ReportHeartbeatSuccess()
        {
            bool fireReceived = false;
            bool fireStatusChanged = false;
            HeartbeatStatus oldStatus = HeartbeatStatus.Unknown;

            // ===================== 锁内：仅更新计数与状态 =====================
            lock (_lock)
            {
                // 更新最后收到心跳的时间（CheckTimeout 依赖此值）
                _lastHeartbeatTime = DateTime.Now;

                // 连续成功 +1，失败清零
                _consecutiveSuccess++;
                _consecutiveFailure = 0;

                // ★ 用【判在线阈值】(小)，而不是判离线阈值
                //   效果：网络恢复后只需连续 3 次成功即可切回 Online，业务 1.5 秒内恢复
                if (_consecutiveSuccess >= _requiredConsecutiveOnline &&
                    _stableStatus != HeartbeatStatus.Online)
                {
                    oldStatus = _stableStatus;
                    _stableStatus = HeartbeatStatus.Online;
                    fireStatusChanged = true;
                }

                fireReceived = true;
            }

            // ===================== 锁外：触发事件 =====================
            if (fireReceived)
            {
                try { HeartbeatReceived?.Invoke(); } catch { }
            }

            if (fireStatusChanged)
            {
                try
                {
                    StatusChanged?.Invoke(this, new HeartbeatStatusChangedEventArgs
                    {
                        NewStatus = HeartbeatStatus.Online,
                        OldStatus = oldStatus,
                        ChangedTime = DateTime.Now
                    });
                }
                catch { }
            }
        }

        /// <summary>
        /// 上报一次读取心跳失败。
        /// 
        /// 【流程】
        /// 1. 锁内：累加失败计数，清零成功计数，
        ///    判断是否达到【判离线阈值】（非对称设计，用大阈值 10）。
        /// 2. 锁外：触发 StatusChanged 事件。
        /// </summary>
        public void ReportHeartbeatFailure()
        {
            bool fireStatusChanged = false;
            HeartbeatStatus oldStatus = HeartbeatStatus.Unknown;

            // ===================== 锁内：仅更新计数与状态 =====================
            lock (_lock)
            {
                // 连续失败 +1，成功清零
                _consecutiveFailure++;
                _consecutiveSuccess = 0;

                // ★ 用【判离线阈值】(大)，防止网络抖动误判
                if (_consecutiveFailure >= _requiredConsecutiveOffline &&
                    _stableStatus != HeartbeatStatus.Offline)
                {
                    oldStatus = _stableStatus;
                    _stableStatus = HeartbeatStatus.Offline;
                    fireStatusChanged = true;
                }
            }

            // ===================== 锁外：触发事件 =====================
            if (fireStatusChanged)
            {
                try
                {
                    StatusChanged?.Invoke(this, new HeartbeatStatusChangedEventArgs
                    {
                        NewStatus = HeartbeatStatus.Offline,
                        OldStatus = oldStatus,
                        ChangedTime = DateTime.Now
                    });
                }
                catch { }
            }
        }

        // ==================== 超时检测 ====================

        /// <summary>
        /// 定时检查是否超时未收到任何心跳包。
        /// 
        /// 【触发时机】由 _timeoutTimer 定时回调，周期 = CHECK_TIMEOUT_PERIOD_MS（500ms）。
        /// 【判定条件】距上次成功心跳的时间 > 超时时间，且当前不是 Offline。
        /// 【副作用】超时后立即把状态置为 Offline，
        ///          并把失败计数同步到【判离线阈值】（防止下一次成功时误切 Online）。
        /// 
        /// 【兜底作用】
        /// 如果 ReportHeartbeatFailure 因为某种原因从没被调用（比如心跳连接断开后
        /// HeartbeatLoop 里 EnsureHeartbeatConnectionAsync 返回 false 就 continue，
        /// 根本没走到 ReportHeartbeatFailure），计数路径就失效了。
        /// 超时路径作为兜底，只要时间过了 TimeoutMs，直接切 Offline。
        /// 
        /// 【★ 本次改动】
        /// 1. 外层加 try-catch：Timer 回调里的异常不能静默吞掉（原代码没有）。
        /// 2. 判离线时输出实际离线时长：便于现场核对"到底等了多久"。
        /// 3. 判定逻辑本身不变（elapsed > timeout && status != Offline）。
        /// </summary>
        private void CheckTimeout(object state)
        {
            if (_disposed) return;

            try
            {
                bool fireStatusChanged = false;
                HeartbeatStatus oldStatus = HeartbeatStatus.Unknown;
                double elapsedMs = 0;

                // ===================== 锁内：仅更新状态 =====================
                lock (_lock)
                {
                    var elapsed = (DateTime.Now - _lastHeartbeatTime).TotalMilliseconds;

                    if (elapsed > _heartbeatTimeoutMs &&
                        _stableStatus != HeartbeatStatus.Offline)
                    {
                        oldStatus = _stableStatus;
                        _stableStatus = HeartbeatStatus.Offline;

                        // ★ 把失败计数直接拉到【判离线阈值】，确保下一次成功上报不会立刻切 Online
                        _consecutiveFailure = _requiredConsecutiveOffline;
                        _consecutiveSuccess = 0;

                        elapsedMs = elapsed;
                        fireStatusChanged = true;
                    }
                }

                // ===================== 锁外：触发事件 =====================
                if (fireStatusChanged)
                {
                    // ★ 新增：输出实际离线时长，便于现场核对"等了多久"
                    //   正常情况应接近 _heartbeatTimeoutMs（±CHECK_TIMEOUT_PERIOD_MS）
                    //   如果显著大于 _heartbeatTimeoutMs，说明定时器线程被阻塞或系统时间有跳变
                    try
                    {
                        Console.WriteLine(
                            $"[CheckTimeout] {Key} 超时判离线：距上次心跳 {elapsedMs:F0}ms（配置阈值 {_heartbeatTimeoutMs}ms）");
                    }
                    catch { }

                    try
                    {
                        StatusChanged?.Invoke(this, new HeartbeatStatusChangedEventArgs
                        {
                            NewStatus = HeartbeatStatus.Offline,
                            OldStatus = oldStatus,
                            ChangedTime = DateTime.Now
                        });
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                // ★ 新增：Timer 回调里的异常不能让它静默吞掉
                try { Console.WriteLine($"[CheckTimeout] {Key} 执行异常：{ex.Message}"); } catch { }
            }
        }

        // ==================== 重置接口 ====================

        /// <summary>
        /// 完整重置（含状态回退到 Unknown，会触发状态变化事件）。
        /// 
        /// 【用途】强制重连后清理历史计数并把状态打回 Unknown。
        /// 【注意】一般业务场景请使用 Reset()，避免把已经 Online 的状态打回 Unknown。
        /// </summary>
        public void Reset1()
        {
            bool fireStatusChanged = false;
            HeartbeatStatus oldStatus = HeartbeatStatus.Unknown;

            lock (_lock)
            {
                oldStatus = _stableStatus;

                _consecutiveSuccess = 0;
                _consecutiveFailure = 0;
                _stableStatus = HeartbeatStatus.Unknown;
                _lastHeartbeatTime = DateTime.Now;

                if (oldStatus != _stableStatus)
                    fireStatusChanged = true;
            }

            if (fireStatusChanged)
            {
                try
                {
                    StatusChanged?.Invoke(this, new HeartbeatStatusChangedEventArgs
                    {
                        NewStatus = HeartbeatStatus.Unknown,
                        OldStatus = oldStatus,
                        ChangedTime = DateTime.Now
                    });
                }
                catch { }
            }
        }

        /// <summary>
        /// 重置监控器的连续成功/失败计数（不改变当前稳定状态，不触发事件）。
        ///
        /// 【用途】
        /// 连接重连成功后清理历史失败计数，让后续的成功/失败计数从零开始，
        /// 但不会把已经判定为 Online 的状态打回 Unknown，
        /// 从而避免业务循环因为一次重连而被误判为"心跳离线"。
        ///
        /// 【注意】
        /// 此方法不会修改 _stableStatus，也不会触发 StatusChanged 事件。
        /// 如果需要恢复到 Unknown 初始状态（例如进程重启/配置重置），请调用 Reset1()。
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                _consecutiveSuccess = 0;
                _consecutiveFailure = 0;
                _lastHeartbeatTime = DateTime.Now;
                // 不修改 _stableStatus，不触发 StatusChanged
            }
        }

        // ==================== 释放 ====================

        /// <summary>
        /// 释放资源，关闭定时器。
        /// </summary>
        public void Dispose()
        {
            _disposed = true;
            try { _timeoutTimer?.Dispose(); } catch { }
        }
    }

    /// <summary>
    /// 全局心跳监控器管理器（按 IP:Port 提供单例）。
    /// 
    /// 【使用方式】
    /// 通过 HeartbeatManager.GetMonitor(ip, port) 获取监控器，
    /// 相同 IP:Port 返回同一个实例，多个 PLCService 可共享同一监控器。
    /// 
    /// 【本次改动】无。HeartbeatMonitor 的构造签名未变，本管理器代码无需调整。
    /// </summary>
    public static class HeartbeatManager
    {
        // 键："IP:Port"，值：对应的 HeartbeatMonitor 实例
        private static readonly ConcurrentDictionary<string, HeartbeatMonitor> _monitors =
            new ConcurrentDictionary<string, HeartbeatMonitor>();

        /// <summary>
        /// 获取或创建指定 IP:Port 的心跳监控器。
        /// </summary>
        /// <param name="ip">PLC IP</param>
        /// <param name="port">PLC 端口</param>
        /// <param name="requiredConsecutive">判离线阈值（可选，默认从 HeartbeatConfig 读）</param>
        /// <param name="heartbeatTimeoutMs">超时时间（可选，默认从 HeartbeatConfig 读）</param>
        /// <param name="requiredConsecutiveOnline">判在线阈值（可选，默认从 HeartbeatConfig 读）</param>
        public static HeartbeatMonitor GetMonitor(string ip, int port,
            int? requiredConsecutive = null,
            int? heartbeatTimeoutMs = null,
            int? requiredConsecutiveOnline = null)
        {
            string key = $"{ip}:{port}";
            // GetOrAdd 保证同一 Key 只创建一次
            return _monitors.GetOrAdd(key, k => new HeartbeatMonitor(
                ip, port,
                requiredConsecutive,
                heartbeatTimeoutMs,
                requiredConsecutiveOnline));
        }

        /// <summary>
        /// 移除指定 IP:Port 的心跳监控器（会释放其内部定时器）。
        /// </summary>
        public static bool RemoveMonitor(string ip, int port)
        {
            string key = $"{ip}:{port}";
            if (_monitors.TryRemove(key, out var monitor))
            {
                monitor.Dispose();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 清除所有监控器（用于配置变更后重建）。
        /// </summary>
        public static void ClearAll()
        {
            foreach (var key in _monitors.Keys)
            {
                if (_monitors.TryRemove(key, out var monitor))
                    monitor.Dispose();
            }
        }
    }
}