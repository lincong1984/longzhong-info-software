using System;
using System.Configuration;

namespace TCPconnectSimaticPLC
{
    /// <summary>
    /// S7NetPlus 异步读写的 timeoutMs 配置。
    /// 从 App.config 读取，现场改配置不重编译。
    ///
    /// 【配置项】
    ///   S7.TimeoutMs.ReadInt32        单值 ReadInt32Async，默认 2000
    ///   S7.TimeoutMs.ReadString       单值 ReadStringAsync，默认 2000
    ///   S7.TimeoutMs.ReadStringsBatch 批量 ReadStringsBatchAsync，默认 6000
    ///   S7.TimeoutMs.WriteInt32       单值 WriteInt32Async，默认 2000
    ///   S7.TimeoutMs.WriteString      单值 WriteStringAsync，默认 2000
    ///   S7.TimeoutMs.Array            DInt/Real 数组读写，默认 3000
    ///
    /// 【用法】
    ///   1. 程序启动时调 S7TimeoutConfig.LoadFromConfig()
    ///   2. Simatic_NET 各 Async 方法 timeoutMs=0 时自动取这里配置
    ///   3. 业务层显式传 timeoutMs（非 0）时优先用业务层的值
    ///
    /// 【设计说明】
    ///   - 本类是纯静态类，全局只有一份配置，线程安全（只在启动时写一次）。
    ///   - 所有属性都是 private set，外部只能通过 LoadFromConfig / SetConfig 修改，
    ///     避免业务代码随意篡改导致超时行为不可预期。
    ///   - 任何一项解析失败（缺失、非数字、≤0）都会保留该项的默认值，
    ///     保证程序永远不会因为配置写错而拿到 0 或负数超时。
    /// </summary>
    public static class S7TimeoutConfig
    {
        /// <summary>
        /// 单值 ReadInt32Async 超时（毫秒），默认 2000。
        /// 对应配置键：S7.TimeoutMs.ReadInt32
        /// </summary>
        public static int ReadInt32 { get; private set; } = 2000;

        /// <summary>
        /// 单值 ReadStringAsync 超时（毫秒），默认 2000。
        /// 对应配置键：S7.TimeoutMs.ReadString
        /// </summary>
        public static int ReadString { get; private set; } = 2000;

        /// <summary>
        /// 批量 ReadStringsBatchAsync 超时（毫秒），默认 6000。
        /// 批量读一次要传多个字符串，耗时明显高于单值读，所以默认值更大。
        /// 对应配置键：S7.TimeoutMs.ReadStringsBatch
        /// </summary>
        public static int ReadStringsBatch { get; private set; } = 6000;

        /// <summary>
        /// 单值 WriteInt32Async 超时（毫秒），默认 2000。
        /// 对应配置键：S7.TimeoutMs.WriteInt32
        /// </summary>
        public static int WriteInt32 { get; private set; } = 2000;

        /// <summary>
        /// 单值 WriteStringAsync 超时（毫秒），默认 2000。
        /// 对应配置键：S7.TimeoutMs.WriteString
        /// </summary>
        public static int WriteString { get; private set; } = 2000;

        /// <summary>
        /// DInt/Real 数组读写超时（毫秒），默认 3000。
        /// 数组读写数据量比单值大，默认值略大于单值。
        /// 对应配置键：S7.TimeoutMs.Array
        /// </summary>
        public static int Array { get; private set; } = 3000;

        /// <summary>
        /// 配置是否已加载。
        /// 用于避免重复加载（LoadFromConfig 内部会判断，重复调用直接返回）。
        /// 注意：加载失败时也会置为 true，防止每次调用都重新走一遍解析逻辑。
        /// </summary>
        public static bool IsLoaded { get; private set; } = false;

        /// <summary>
        /// 从 App.config 加载配置。建议在程序启动时调用一次。
        /// 任何一项解析失败都保持默认值。
        ///
        /// 【实现说明】
        ///   - int.TryParse 解析失败返回 false，此时不赋值，属性保持默认值。
        ///   - 额外要求 v > 0，防止现场把超时配成 0 或负数导致 Task.Delay 立即超时。
        ///   - 所有配置项共享同一个局部变量 v，逐项解析，互不影响。
        /// </summary>
        public static void LoadFromConfig()
        {
            // 已经加载过就直接返回，保证只加载一次（幂等）
            if (IsLoaded) return;

            try
            {
                // 复用的临时变量：每项都先 TryParse 到 v，再判断是否 > 0 才赋值
                int v;

                // 单值 ReadInt32Async 超时
                if (int.TryParse(ConfigurationManager.AppSettings["S7.TimeoutMs.ReadInt32"], out v) && v > 0)
                    ReadInt32 = v;

                // 单值 ReadStringAsync 超时
                if (int.TryParse(ConfigurationManager.AppSettings["S7.TimeoutMs.ReadString"], out v) && v > 0)
                    ReadString = v;

                // 批量 ReadStringsBatchAsync 超时
                if (int.TryParse(ConfigurationManager.AppSettings["S7.TimeoutMs.ReadStringsBatch"], out v) && v > 0)
                    ReadStringsBatch = v;

                // 单值 WriteInt32Async 超时
                if (int.TryParse(ConfigurationManager.AppSettings["S7.TimeoutMs.WriteInt32"], out v) && v > 0)
                    WriteInt32 = v;

                // 单值 WriteStringAsync 超时
                if (int.TryParse(ConfigurationManager.AppSettings["S7.TimeoutMs.WriteString"], out v) && v > 0)
                    WriteString = v;

                // DInt/Real 数组读写超时
                if (int.TryParse(ConfigurationManager.AppSettings["S7.TimeoutMs.Array"], out v) && v > 0)
                    Array = v;

                // 无论上面几项是否解析成功，都标记为已加载，避免重复解析
                IsLoaded = true;

                // 打印最终生效的配置，方便现场确认到底用了哪些值
                Console.WriteLine("[S7TimeoutConfig] 配置加载完成：");
                Console.WriteLine($"  ReadInt32        = {ReadInt32}ms");
                Console.WriteLine($"  ReadString       = {ReadString}ms");
                Console.WriteLine($"  ReadStringsBatch = {ReadStringsBatch}ms");
                Console.WriteLine($"  WriteInt32       = {WriteInt32}ms");
                Console.WriteLine($"  WriteString      = {WriteString}ms");
                Console.WriteLine($"  Array            = {Array}ms");
            }
            catch (Exception ex)
            {
                // 极少数情况（如配置文件格式损坏）会走到这里：
                // 打印错误，保留所有默认值，并仍然标记为已加载
                Console.WriteLine($"[S7TimeoutConfig] 加载配置失败，使用默认值：{ex.Message}");
                IsLoaded = true;
            }
        }

        /// <summary>
        /// 运行时手动设置（用于测试或代码内覆盖配置）。
        /// 传 0 或负数的项会被忽略。
        ///
        /// 【说明】
        ///   - 与 LoadFromConfig 不同，这里不会改 IsLoaded，
        ///     所以可以在 LoadFromConfig 之后调用做局部覆盖。
        ///   - 每个参数独立判断，只覆盖 > 0 的项，其余保持不变。
        /// </summary>
        public static void SetConfig(int readInt32, int readString, int readStringsBatch,
            int writeInt32, int writeString, int array)
        {
            if (readInt32 > 0) ReadInt32 = readInt32;                 // 覆盖单值读 int 超时
            if (readString > 0) ReadString = readString;              // 覆盖单值读 string 超时
            if (readStringsBatch > 0) ReadStringsBatch = readStringsBatch; // 覆盖批量读 string 超时
            if (writeInt32 > 0) WriteInt32 = writeInt32;              // 覆盖单值写 int 超时
            if (writeString > 0) WriteString = writeString;           // 覆盖单值写 string 超时
            if (array > 0) Array = array;                             // 覆盖数组读写超时
        }
    }
}