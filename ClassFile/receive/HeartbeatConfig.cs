using System;
using System.Configuration;

namespace MES_SCAF
{
    /// <summary>
    /// 心跳全局配置（从 App.config 读取）
    /// 
    /// 【使用方式】
    /// 在程序启动时（mainform_Load）调用 LoadFromConfig() 加载配置，
    /// 之后 HeartbeatManager.GetMonitor 会自动使用这些配置。
    /// 
    /// 【配置项说明】
    /// - RequiredConsecutive        ：判【离线】所需连续失败次数（默认 10）
    /// - RequiredConsecutiveOnline  ：判【在线】所需连续成功次数（默认 3，新增）
    /// - HeartbeatTimeoutMs         ：超过该时间未收到心跳判定离线，默认 20000ms
    /// - HeartbeatReadIntervalMs    ：心跳读取间隔，默认 500ms
    /// - OfflineCloseBusinessSeconds：心跳离线后关闭业务连接的等待时间，默认 120 秒
    /// 
    /// 【为什么需要两个阈值？】
    /// 判离线要"稳"（防误判）→ 阈值大；
    /// 判在线要"快"（快速恢复业务）→ 阈值小。
    /// 这两个方向的最优值天然矛盾，用同一个阈值必然牺牲其中一方。
    /// 
    /// 【推荐值】
    /// | 网络质量 | 判离线阈值 | 判在线阈值 | 超时Ms | 关连接秒 |
    /// |---------|-----------|-----------|--------|---------|
    /// | 非常稳定 | 6         | 2         | 15000  | 90      |
    /// | 一般稳定 | 10        | 3         | 20000  | 120     |
    /// | 不稳定   | 15        | 3         | 30000  | 180     |
    /// 
    /// 【调优约束】
    ///   RequiredConsecutive × ReadIntervalMs 应明显小于 HeartbeatTimeoutMs，
    ///   否则会出现"连续失败未达阈值就被超时判离线"的状态机抖动。
    ///   例：10 × 500 = 5000ms ＜ 20000ms  ✓
    /// </summary>
    public static class HeartbeatConfig
    {
        /// <summary>【判离线】连续失败阈值，默认 10（阈值大 → 防误判）</summary>
        public static int RequiredConsecutive { get; private set; } = 10;

        /// <summary>
        /// 【判在线】连续成功阈值，默认 3（阈值小 → 快速恢复）。
        /// 网络一恢复，只需连续 3 次成功即可切回 Online，业务 1.5 秒内恢复。
        /// </summary>
        public static int RequiredConsecutiveOnline { get; private set; } = 3;

        /// <summary>心跳超时时间（毫秒），默认 20000（兜底判定）</summary>
        public static int HeartbeatTimeoutMs { get; private set; } = 20000;

        /// <summary>心跳读取间隔（毫秒），默认 500</summary>
        public static int HeartbeatReadIntervalMs { get; private set; } = 500;

        /// <summary>心跳离线后关闭业务连接的等待时间（秒），默认 120</summary>
        public static int OfflineCloseBusinessSeconds { get; private set; } = 120;

        /// <summary>配置是否已加载（只允许加载一次）</summary>
        public static bool IsLoaded { get; private set; } = false;

        /// <summary>
        /// 从 App.config 加载配置。
        /// 建议在程序启动时（mainform_Load）调用一次。
        /// </summary>
        public static void LoadFromConfig()
        {
            if (IsLoaded) return;

            try
            {
                // 读取【判离线】连续阈值
                string rc = ConfigurationManager.AppSettings["Heartbeat.RequiredConsecutive"];
                if (int.TryParse(rc, out int rcVal) && rcVal > 0)
                    RequiredConsecutive = rcVal;

                // 读取【判在线】连续阈值（新增项）
                string rco = ConfigurationManager.AppSettings["Heartbeat.RequiredConsecutiveOnline"];
                if (int.TryParse(rco, out int rcoVal) && rcoVal > 0)
                    RequiredConsecutiveOnline = rcoVal;

                // 读取心跳超时时间
                string ht = ConfigurationManager.AppSettings["Heartbeat.TimeoutMs"];
                if (int.TryParse(ht, out int htVal) && htVal > 0)
                    HeartbeatTimeoutMs = htVal;

                // 读取心跳读取间隔
                string hr = ConfigurationManager.AppSettings["Heartbeat.ReadIntervalMs"];
                if (int.TryParse(hr, out int hrVal) && hrVal > 0)
                    HeartbeatReadIntervalMs = hrVal;

                // 读取离线关闭业务连接等待时间
                string oc = ConfigurationManager.AppSettings["Heartbeat.OfflineCloseBusinessSeconds"];
                if (int.TryParse(oc, out int ocVal) && ocVal > 0)
                    OfflineCloseBusinessSeconds = ocVal;

                // ★ 一致性约束：判在线阈值不能大于判离线阈值
                // （否则可能出现"网络恢复也要等很久才能切 Online"的尴尬）
                if (RequiredConsecutiveOnline > RequiredConsecutive)
                    RequiredConsecutiveOnline = Math.Max(1, RequiredConsecutive / 3);

                IsLoaded = true;

                // 输出配置加载日志
                Console.WriteLine($"[HeartbeatConfig] 配置加载完成：");
                Console.WriteLine($"  RequiredConsecutive (判离线)     = {RequiredConsecutive}");
                Console.WriteLine($"  RequiredConsecutiveOnline (判在线) = {RequiredConsecutiveOnline}");
                Console.WriteLine($"  HeartbeatTimeoutMs                = {HeartbeatTimeoutMs}");
                Console.WriteLine($"  HeartbeatReadIntervalMs           = {HeartbeatReadIntervalMs}");
                Console.WriteLine($"  OfflineCloseBusinessSeconds       = {OfflineCloseBusinessSeconds}");
                Console.WriteLine($"  → 判离线时间 ≈ {RequiredConsecutive * HeartbeatReadIntervalMs}ms");
                Console.WriteLine($"  → 判在线时间 ≈ {RequiredConsecutiveOnline * HeartbeatReadIntervalMs}ms");
            }
            catch (Exception ex)
            {
                // 任何异常都吞掉，使用默认值即可
                Console.WriteLine($"[HeartbeatConfig] 加载配置失败，使用默认值：{ex.Message}");
                IsLoaded = true;
            }
        }

        /// <summary>
        /// 手动设置配置（用于测试或运行时调整）。
        /// 传 0 或负数的项会被忽略（保持原值）。
        /// </summary>
        public static void SetConfig(int requiredConsecutive, int requiredConsecutiveOnline,
            int heartbeatTimeoutMs, int heartbeatReadIntervalMs, int offlineCloseBusinessSeconds)
        {
            if (requiredConsecutive > 0) RequiredConsecutive = requiredConsecutive;
            if (requiredConsecutiveOnline > 0) RequiredConsecutiveOnline = requiredConsecutiveOnline;
            if (heartbeatTimeoutMs > 0) HeartbeatTimeoutMs = heartbeatTimeoutMs;
            if (heartbeatReadIntervalMs > 0) HeartbeatReadIntervalMs = heartbeatReadIntervalMs;
            if (offlineCloseBusinessSeconds > 0) OfflineCloseBusinessSeconds = offlineCloseBusinessSeconds;
        }
    }
}