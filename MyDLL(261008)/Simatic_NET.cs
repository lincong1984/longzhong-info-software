using S7.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace TCPconnectSimaticPLC
{
    /// <summary>
    /// 西门子 PLC 通信封装类。
    ///
    /// ============================================================
    /// 【本版改动】
    /// ============================================================
    /// 保留原有的层1/层2同步优化：
    ///   层1-A：ReadString 从 2 次 socket 读改为 1 次
    ///   层1-B：WriteString 从 2 次 socket 写改为 1 次
    ///   层2-A：ReadStringsBatch（批量读连续 String）
    ///
    /// 新增【方案B：异步硬超时】：
    ///   ReadInt32Async / ReadInt32Async(array)
    ///   ReadRealAsync / ReadStringAsync / ReadStringsBatchAsync
    ///   WriteInt32Async / WriteInt32Async(array) / WriteRealAsync / WriteStringAsync
    ///
    /// 异步方法行为：
    ///   1. 传入 CancellationToken（尽力软取消）—— 注：本版实现里未使用，
    ///      硬超时靠 Task.WhenAny + 强关 TcpClient 实现。
    ///   2. Task.WhenAny(operation, Delay(timeoutMs)) 做硬超时
    ///   3. 超时后反射强制关闭 Plc.tcpClient 打断底层 IO
    ///   4. 置 IsConnectionBrokenByTimeout=true，调用方必须重连
    ///   5. 下次 initializeConnect 成功后自动清零
    ///
    /// 原有同步方法全部保留，供探针 / 心跳等场景使用。
    ///
    /// 【线程安全提示】
    ///   plc 字段是共享引用，多个线程同时调用同一个实例的方法时，
    ///   底层 S7.Net 的 Plc 并非线程安全，业务层需自行串行化或每线程独立实例。
    /// ============================================================
    /// </summary>
    public class Simatic_NET
    {
        #region SIMATIC S7超时配置 

        /// <summary>
        /// 发送超时时间（毫秒），默认 2000。
        /// 在 initializeConnect 中会赋值给 Plc.WriteTimeout（socket 层发送超时）。
        /// </summary>
        public int SendTimeout { get; set; } = 2000;

        /// <summary>
        /// 接收超时时间（毫秒），默认 800。
        /// 在 initializeConnect 中会赋值给 Plc.ReadTimeout（socket 层接收超时）。
        /// 注意：这是同步 socket 的超时，异步方法的硬超时由 timeoutMs 参数单独控制。
        /// </summary>
        public int ReceiveTimeout { get; set; } = 800;

        /// <summary>
        /// S7.Net 的 Plc 对象。
        /// 连接成功后由 initializeConnect 创建；断开/异常时可能为 null。
        /// 业务代码不要直接操作它，统一走本类封装的方法。
        /// </summary>
        public Plc plc;

        /// <summary>
        /// 【新增】是否因硬超时导致连接损坏。
        /// 硬超时后本属性为 true，调用方应放弃当前 plc 并重连。
        ///
        /// 原因：硬超时是通过强制 Close 底层 TcpClient 实现的，
        ///       socket 已被破坏，后续任何读写都不可靠，只能重连。
        /// 复位：initializeConnect 连接成功后自动置 false。
        /// </summary>
        public bool IsConnectionBrokenByTimeout { get; private set; }

        #endregion

        #region SIMATIC S7连接管理

        /// <summary>
        /// 连接 PLC（同步）。
        ///
        /// 【P0 修复】Open() 之后再设一次 ReadTimeout/WriteTimeout，
        /// 保证 timeout 真正传到 socket。
        ///
        /// 原因：部分 S7.Net 版本在 Open() 内部会重建 TcpClient 或覆盖超时字段，
        ///       导致 Open() 之前设置的值丢失。Open() 之后再设一次最稳妥。
        /// </summary>
        /// <param name="cpu">CPU 型号字符串，如 "S7-1200"</param>
        /// <param name="ip">PLC 的 IP 地址</param>
        /// <param name="rack">机架号</param>
        /// <param name="slot">槽号</param>
        /// <returns>OperateResult，IsSuccess 表示连接是否成功</returns>
        public OperateResult initializeConnect(string cpu, string ip, Int16 rack, Int16 slot)
        {
            // 统一返回对象，所有分支都往这里填结果
            OperateResult tcpsocket = new OperateResult();

            try
            {
                // 参数校验：CPU 类型不能为空
                if (string.IsNullOrEmpty(cpu))
                {
                    tcpsocket.IsSuccess = false;
                    tcpsocket.Message = "错误：CPU 类型不能为空";
                    return tcpsocket;
                }
                // 参数校验：IP 不能为空
                if (string.IsNullOrEmpty(ip))
                {
                    tcpsocket.IsSuccess = false;
                    tcpsocket.Message = "错误：IP 地址不能为空";
                    return tcpsocket;
                }

                // 字符串 CPU 型号 → 枚举
                CpuType plcCpu = GetCpuType(cpu);

                // 创建 Plc 实例（此时还未建立 TCP 连接）
                plc = new Plc(plcCpu, ip, rack, slot);

                // 从本实例属性取超时值，作为 socket 层读写超时
                int readTimeoutMs = ReceiveTimeout;
                int writeTimeoutMs = SendTimeout;

                // 第一次设置（保险）：在 Open 之前先设一遍
                plc.ReadTimeout = readTimeoutMs;
                plc.WriteTimeout = writeTimeoutMs;

                // 建立连接（tcpClient 在这里被创建，真正的 socket 握手发生在此）
                plc.Open();

                // 第二次设置（P0 核心）：Open 之后补设，确保值真正落到 socket 上
                plc.ReadTimeout = readTimeoutMs;
                plc.WriteTimeout = writeTimeoutMs;

                // 【新增】连接成功 → 硬超时标志清零，表示当前连接是干净可用的
                IsConnectionBrokenByTimeout = false;

                try
                {
                    // 打印连接成功的日志（含最终生效的超时值，便于现场排查）
                    Console.WriteLine(
                        $"[Simatic_NET] {ip}:{slot} 连接成功 " +
                        $"ReadTimeout={plc.ReadTimeout}ms WriteTimeout={plc.WriteTimeout}ms");
                }
                catch { } // 日志打印失败不影响连接结果，直接吞掉

                // 以 Plc.IsConnected 为准判断是否真的连上
                tcpsocket.IsSuccess = plc.IsConnected;
                if (!tcpsocket.IsSuccess)
                    tcpsocket.Message = "错误：无法连接到 Siemens PLC";
            }
            catch (Exception ex)
            {
                // 连接过程抛出异常：返回失败信息
                tcpsocket.IsSuccess = false;
                tcpsocket.Message = $"错误：连接 Siemens PLC 信息出错 - {ex.Message}";

                // 尽力关闭可能已部分建立的连接，避免 socket 泄漏
                try { plc?.Close(); } catch { }
                // 置空引用，后续调用会得到"PLC未连接"
                plc = null;
            }

            return tcpsocket;
        }

        /// <summary>
        /// 断开连接。
        ///
        /// 【本次修复】
        ///   1. plc.Close() 抛异常时，原代码 plc = null 不执行，
        ///      残留一个“半关闭”实例，后续 IsConnected 判断可能误判。
        ///   2. 改为 finally { plc = null; }，保证无论 Close 是否成功都置空。
        ///   3. 同时复位 IsConnectionBrokenByTimeout：连接已不存在，
        ///      该标志不应再代表“当前连接损坏”。
        /// </summary>
        public OperateResult ConnectClose()
        {
            OperateResult tcpsocket = new OperateResult();
            try
            {
                try
                {
                    plc?.Close();
                }
                catch (Exception ex)
                {
                    // 记录关闭异常，但不阻断置空
                    tcpsocket.Message = ex.Message;
                }
            }
            finally
            {
                // ★ 无论 Close 是否抛异常，都置空，保证下次必须重新 initializeConnect
                plc = null;
                // 连接已断开，硬超时标志复位
                IsConnectionBrokenByTimeout = false;
            }

            tcpsocket.IsSuccess = string.IsNullOrEmpty(tcpsocket.Message);
            if (!tcpsocket.IsSuccess)
                tcpsocket.Message = $"错误：Siemens PLC 断开连接信息出错 - {tcpsocket.Message}";

            return tcpsocket;
        }

        #endregion

        #region SIMATIC S7同步读写

        /// <summary>
        /// 读取单个 32 位整型数据。
        /// 使用 S7.Net 的地址字符串重载（如 "DB1.DBD0"）。
        /// </summary>
        /// <param name="address">PLC 地址，如 "DB1.DBD0"</param>
        /// <returns>OperateResult&lt;int&gt;，Content 为读到的值</returns>
        public OperateResult<int> ReadInt32(string address)
        {
            OperateResult<int> tcpsocket = new OperateResult<int>();
            try
            {
                // 前置检查：plc 存在且已连接
                if (plc != null && plc.IsConnected)
                {
                    // 地址统一转大写，S7.Net 解析更稳定
                    // 返回值是 object（S7.Net 的通用 Read），先转 uint 再 ConvertToInt
                    // 转 uint 是为了正确处理 DInt 的符号位（避免直接 (int) 转换出错）
                    int result = ((uint)plc.Read(address.ToUpper())).ConvertToInt();
                    tcpsocket.Content = result;
                    tcpsocket.IsSuccess = true;
                }
                else
                {
                    tcpsocket.IsSuccess = false;
                    tcpsocket.Message = "错误：PLC未连接！";
                }
            }
            catch (Exception ex)
            {
                // 任何异常（地址错误、socket 异常等）都转为失败结果
                tcpsocket.Message = ex.Message;
                tcpsocket.IsSuccess = false;
            }
            return tcpsocket;
        }

        /// <summary>
        /// 读取连续的 32 位整型数据（数组）。
        /// 内部使用 DataBlock + VarType.DInt 批量读，一次 socket 往返。
        /// </summary>
        /// <param name="address">起始地址，如 "DB1.DBD0" 或 "1.0"</param>
        /// <param name="length">要读取的 DInt 个数</param>
        /// <returns>OperateResult&lt;int[]&gt;，Content 为读到的数组</returns>
        public OperateResult<int[]> ReadInt32(string address, int length)
        {
            OperateResult<int[]> tcpsocket = new OperateResult<int[]>();
            try
            {
                if (plc != null && plc.IsConnected)
                {
                    // 地址预处理：去掉所有字母，只保留 "DB号.起始字节"
                    // 例如 "DB1.DBD0" → "1.0"，再按 '.' 拆成 [db, startByteAdr]
                    string[] add = Regex.Replace(address.ToUpper(), "[a-z]", "", RegexOptions.IgnoreCase).Split('.');
                    int db = int.Parse(add[0]);            // DB 块号
                    int startByteAdr = int.Parse(add[1]);  // 起始字节偏移

                    // 批量读 DInt：一次请求读 length 个
                    var dint = plc.Read(DataType.DataBlock, db, startByteAdr, VarType.DInt, length);

                    // 当 length==1 时，S7.Net 返回单个 int，需要包装成数组；
                    // 大于 1 时直接强转 int[]
                    if (length == 1)
                        tcpsocket.Content = new int[1] { Convert.ToInt32(dint) };
                    else
                        tcpsocket.Content = (int[])dint;
                    tcpsocket.IsSuccess = true;
                }
                else
                {
                    tcpsocket.IsSuccess = false;
                    tcpsocket.Message = "错误：PLC未连接！";
                }
            }
            catch (Exception ex)
            {
                tcpsocket.Message = ex.Message;
                tcpsocket.IsSuccess = false;
            }
            return tcpsocket;
        }

        /// <summary>
        /// 读取连续的 Byte 数据（字节数组）。
        /// 适合读取原始字节区（如配方、字符串区、自定义协议区）。
        /// </summary>
        /// <param name="address">起始地址，如 "DB1.DBB0"</param>
        /// <param name="length">要读取的字节数</param>
        /// <returns>OperateResult&lt;byte[]&gt;</returns>
        public OperateResult<byte[]> ReadByte(string address, int length)
        {
            OperateResult<byte[]> tcpsocket = new OperateResult<byte[]>();
            try
            {
                if (plc != null && plc.IsConnected)
                {
                    // 地址预处理：去掉字母后拆成 DB 号与起始字节
                    string[] add = Regex.Replace(address.ToUpper(), "[a-z]", "", RegexOptions.IgnoreCase).Split('.');
                    int db = int.Parse(add[0]);
                    int startByteAdr = int.Parse(add[1]);

                    // 直接按字节读，不涉及类型解析
                    var bytes = plc.ReadBytes(DataType.DataBlock, db, startByteAdr, length);
                    tcpsocket.Content = bytes;
                    tcpsocket.IsSuccess = true;
                }
                else
                {
                    tcpsocket.IsSuccess = false;
                    tcpsocket.Message = "错误：PLC未连接！";
                }
            }
            catch (Exception ex)
            {
                tcpsocket.Message = ex.Message;
                tcpsocket.IsSuccess = false;
            }
            return tcpsocket;
        }

        /// <summary>
        /// 读取西门子 String 类型（层1：一次读 length+2 字节）。
        ///
        /// 【S7 String 结构】
        ///   byte[0] = 最大长度 maxLen
        ///   byte[1] = 实际长度 realLen
        ///   byte[2..] = ASCII 字符数据
        /// 所以读 length 个字符需要读 length+2 字节。
        ///
        /// 【层1 优化】原实现分两次 socket 读（先读头再读数据），
        /// 本实现一次读 length+2 字节，减少一次网络往返。
        /// </summary>
        /// <param name="address">起始地址，如 "DB1.DBB0"</param>
        /// <param name="length">String 的最大字符数（不含 2 字节头）</param>
        /// <returns>OperateResult&lt;string&gt;，Content 为解码后的字符串</returns>
        public OperateResult<string> ReadString(string address, int length)
        {
            OperateResult<string> tcpsocket = new OperateResult<string>();
            tcpsocket.Content = "";   // 默认空串，避免调用方拿到 null
            try
            {
                if (plc != null && plc.IsConnected)
                {
                    // 地址预处理：得到 DB 号与起始字节
                    string[] add = Regex.Replace(address.ToUpper(), "[a-z]", "", RegexOptions.IgnoreCase).Split('.');
                    int db = int.Parse(add[0]);
                    int startByteAdr = int.Parse(add[1]);

                    // 一次读 length+2 字节：2 字节头 + length 字节数据
                    byte[] buffer = plc.ReadBytes(DataType.DataBlock, db, startByteAdr, length + 2);

                    // 防御：返回字节数不足 2，说明连头都没读到，直接报错
                    if (buffer == null || buffer.Length < 2)
                    {
                        tcpsocket.IsSuccess = false;
                        tcpsocket.Message = "错误：读取String返回字节不足";
                        return tcpsocket;
                    }

                    byte maxLen = buffer[0];   // 最大长度（本方法未使用，仅解构说明）
                    byte realLen = buffer[1];  // 实际长度

                    // 防御：实际长度不能超过声明长度，也不能超过缓冲区剩余字节数
                    if (realLen > length) realLen = (byte)length;
                    if (realLen > buffer.Length - 2) realLen = (byte)(buffer.Length - 2);

                    // 从第 2 字节开始按实际长度解码 ASCII
                    tcpsocket.Content = realLen == 0 ? "" : Encoding.ASCII.GetString(buffer, 2, realLen);
                    tcpsocket.IsSuccess = true;
                }
                else
                {
                    tcpsocket.IsSuccess = false;
                    tcpsocket.Message = "错误：PLC未连接！";
                }
            }
            catch (Exception ex)
            {
                tcpsocket.Message = ex.Message;
                tcpsocket.IsSuccess = false;
            }
            return tcpsocket;
        }

        /// <summary>
        /// 批量读取连续的多个 S7 String（层2）。
        ///
        /// 适用场景：一组等长 String 在 DB 中连续排列，每个占 stride 字节。
        /// 一次 socket 读就能拿到全部数据，大幅减少往返次数。
        ///
        /// 【参数约束】
        ///   stride >= 2（至少要容纳 2 字节头）
        ///   count  > 0
        ///   stride * count <= 2000（单次 PDU 上限，超了应回退到单个读）
        /// </summary>
        /// <param name="db">DB 块号</param>
        /// <param name="startByteAdr">第一个 String 的起始字节</param>
        /// <param name="stride">每个 String 占用的字节数（含 2 字节头）</param>
        /// <param name="count">要读取的 String 个数</param>
        /// <returns>OperateResult&lt;string[]&gt;</returns>
        public OperateResult<string[]> ReadStringsBatch(int db, int startByteAdr, int stride, int count)
        {
            OperateResult<string[]> result = new OperateResult<string[]>();
            result.Content = new string[0];   // 默认空数组，避免调用方拿到 null

            try
            {
                // 连接检查
                if (plc == null || !plc.IsConnected)
                {
                    result.IsSuccess = false;
                    result.Message = "错误：PLC未连接！";
                    return result;
                }

                // 参数校验：stride 至少 2（头），count 必须为正
                if (stride < 2 || count <= 0)
                {
                    result.IsSuccess = false;
                    result.Message = $"错误：参数无效 stride={stride}, count={count}";
                    return result;
                }

                // 总字节数校验：超过 2000 字节单次 PDU 读不下，需要业务层回退
                int totalBytes = stride * count;
                if (totalBytes > 2000)
                {
                    result.IsSuccess = false;
                    result.Message = $"错误：批量读总字节数 {totalBytes} 过大，应回退到单个读";
                    return result;
                }

                // 一次性读取全部字节
                byte[] buffer = plc.ReadBytes(DataType.DataBlock, db, startByteAdr, totalBytes);

                // 防御：返回字节不足，说明读失败
                if (buffer == null || buffer.Length < totalBytes)
                {
                    result.IsSuccess = false;
                    result.Message = $"错误：批量读返回字节不足，期望{totalBytes}，实际{buffer?.Length ?? 0}";
                    return result;
                }

                // 逐个解出每个 String
                string[] strings = new string[count];
                for (int i = 0; i < count; i++)
                {
                    int offset = i * stride;                 // 第 i 个 String 的起始偏移
                    byte realLen = buffer[offset + 1];       // 第 i 个 String 的实际长度
                    if (realLen > stride - 2) realLen = (byte)(stride - 2); // 防御：不能超过数据区容量
                    strings[i] = realLen == 0 ? "" : Encoding.ASCII.GetString(buffer, offset + 2, realLen);
                }

                result.Content = strings;
                result.IsSuccess = true;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Message = $"批量读异常：{ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 读取双精度浮点数（LReal，8 字节）。
        /// </summary>
        /// <param name="address">起始地址，如 "DB1.DBB0"</param>
        /// <param name="length">要读取的 LReal 个数</param>
        /// <returns>OperateResult&lt;double[]&gt;</returns>
        public OperateResult<double[]> ReadLReal(string address, int length)
        {
            OperateResult<double[]> tcpsocket = new OperateResult<double[]>();
            try
            {
                if (plc != null && plc.IsConnected)
                {
                    // 地址预处理：拿到 DB 号与起始字节
                    string[] add = Regex.Replace(address.ToUpper(), "[a-z]", "", RegexOptions.IgnoreCase).Split('.');
                    int db = int.Parse(add[0]);
                    int startByteAdr = int.Parse(add[1]);

                    // 按 VarType.LReal（8 字节）批量读
                    var real = plc.Read(DataType.DataBlock, db, startByteAdr, VarType.LReal, length);

                    // length==1 时返回单值，需要包装成数组
                    tcpsocket.Content = length == 1 ? new double[1] { Convert.ToDouble(real) } : (double[])real;
                    tcpsocket.IsSuccess = true;
                }
                else
                {
                    tcpsocket.IsSuccess = false;
                    tcpsocket.Message = "错误：PLC未连接！";
                }
            }
            catch (Exception ex)
            {
                tcpsocket.Message = ex.Message;
                tcpsocket.IsSuccess = false;
            }
            return tcpsocket;
        }

        /// <summary>
        /// 读取单精度浮点数（Real，4 字节）。
        /// </summary>
        /// <param name="address">起始地址，如 "DB1.DBB0"</param>
        /// <param name="length">要读取的 Real 个数</param>
        /// <returns>OperateResult&lt;float[]&gt;</returns>
        public OperateResult<float[]> ReadReal(string address, int length)
        {
            OperateResult<float[]> tcpsocket = new OperateResult<float[]>();
            try
            {
                if (plc != null && plc.IsConnected)
                {
                    // 地址预处理：拿到 DB 号与起始字节
                    string[] add = Regex.Replace(address.ToUpper(), "[a-z]", "", RegexOptions.IgnoreCase).Split('.');
                    int db = int.Parse(add[0]);
                    int startByteAdr = int.Parse(add[1]);

                    // 按 VarType.Real（4 字节）批量读
                    var real = plc.Read(DataType.DataBlock, db, startByteAdr, VarType.Real, length);

                    // length==1 时返回单值，需要包装成数组
                    tcpsocket.Content = length == 1 ? new float[1] { (float)real } : (float[])real;
                    tcpsocket.IsSuccess = true;
                }
                else
                {
                    tcpsocket.IsSuccess = false;
                    tcpsocket.Message = "错误：PLC未连接！";
                }
            }
            catch (Exception ex)
            {
                tcpsocket.Message = ex.Message;
                tcpsocket.IsSuccess = false;
            }
            return tcpsocket;
        }

        /// <summary>
        /// 写入单个 32 位整型数据。
        /// </summary>
        /// <param name="address">目标地址，如 "DB1.DBD0"</param>
        /// <param name="data">要写入的 int 值</param>
        /// <returns>OperateResult&lt;int&gt;，Content=1 表示成功</returns>
        public OperateResult<int> WriteInt32(string address, int data)
        {
            OperateResult<int> tcpsocket = new OperateResult<int>();
            try
            {
                if (plc != null && plc.IsConnected)
                {
                    // 使用地址字符串重载写入单个 int
                    plc.Write(address.ToUpper(), data);
                    tcpsocket.Content = 1;
                    tcpsocket.Message = "OK";
                    tcpsocket.IsSuccess = true;
                }
                else
                {
                    tcpsocket.IsSuccess = false;
                    tcpsocket.Message = "错误：PLC未连接！";
                }
            }
            catch (Exception ex)
            {
                tcpsocket.Message = ex.Message;
                tcpsocket.IsSuccess = false;
            }
            return tcpsocket;
        }

        /// <summary>
        /// 写入连续的 32 位整型数据（数组）。
        /// </summary>
        /// <param name="address">起始地址</param>
        /// <param name="length">数组长度（当前实现由 data.Length 决定，此参数保留以对齐接口）</param>
        /// <param name="data">要写入的 int 数组</param>
        /// <returns>OperateResult&lt;int&gt;，Content=1 表示成功</returns>
        public OperateResult<int> WriteInt32(string address, int length, int[] data)
        {
            OperateResult<int> tcpsocket = new OperateResult<int>();
            try
            {
                if (plc != null && plc.IsConnected)
                {
                    // 地址预处理：拿到 DB 号与起始字节
                    string[] add = Regex.Replace(address.ToUpper(), "[a-z]", "", RegexOptions.IgnoreCase).Split('.');
                    int db = int.Parse(add[0]);
                    int startByteAdr = int.Parse(add[1]);

                    // 使用 DataBlock 重载批量写入（S7.Net 会按 int[] 自动展开为 DInt）
                    plc.Write(DataType.DataBlock, db, startByteAdr, data);

                    tcpsocket.Content = 1;
                    tcpsocket.Message = "OK";
                    tcpsocket.IsSuccess = true;
                }
                else
                {
                    tcpsocket.IsSuccess = false;
                    tcpsocket.Message = "错误：PLC未连接！";
                }
            }
            catch (Exception ex)
            {
                tcpsocket.Message = ex.Message;
                tcpsocket.IsSuccess = false;
            }
            return tcpsocket;
        }

        /// <summary>
        /// 写入单个单精度 float（Real，4 字节）。
        /// </summary>
        /// <param name="address">目标地址</param>
        /// <param name="data">要写入的 float 值</param>
        /// <returns>OperateResult&lt;int&gt;，Content=1 表示成功</returns>
        public OperateResult<int> WriteReal(string address, float data)
        {
            OperateResult<int> tcpsocket = new OperateResult<int>();
            try
            {
                if (plc != null && plc.IsConnected)
                {
                    // 使用地址字符串重载写入单个 float
                    plc.Write(address.ToUpper(), data);
                    tcpsocket.Content = 1;
                    tcpsocket.Message = "OK";
                    tcpsocket.IsSuccess = true;
                }
                else
                {
                    tcpsocket.IsSuccess = false;
                    tcpsocket.Message = "错误：PLC未连接！";
                }
            }
            catch (Exception ex)
            {
                tcpsocket.Message = ex.Message;
                tcpsocket.IsSuccess = false;
            }
            return tcpsocket;
        }

        /// <summary>
        /// 写入单个双精度 double（LReal，8 字节）。
        /// </summary>
        /// <param name="address">目标地址</param>
        /// <param name="data">要写入的 double 值</param>
        /// <returns>OperateResult&lt;int&gt;，Content=1 表示成功</returns>
        public OperateResult<int> WriteLReal(string address, double data)
        {
            OperateResult<int> tcpsocket = new OperateResult<int>();
            try
            {
                if (plc != null && plc.IsConnected)
                {
                    // 使用地址字符串重载写入单个 double
                    plc.Write(address.ToUpper(), data);
                    tcpsocket.Content = 1;
                    tcpsocket.Message = "OK";
                    tcpsocket.IsSuccess = true;
                }
                else
                {
                    tcpsocket.IsSuccess = false;
                    tcpsocket.Message = "错误：PLC未连接！";
                }
            }
            catch (Exception ex)
            {
                tcpsocket.Message = ex.Message;
                tcpsocket.IsSuccess = false;
            }
            return tcpsocket;
        }

        /// <summary>
        /// 写入连续的单精度 float 数组（Real 数组）。
        /// </summary>
        /// <param name="address">起始地址</param>
        /// <param name="length">数组长度（当前实现由 data.Length 决定，此参数保留以对齐接口）</param>
        /// <param name="data">要写入的 float 数组</param>
        /// <returns>OperateResult&lt;int&gt;，Content=1 表示成功</returns>
        public OperateResult<int> WriteReal(string address, int length, float[] data)
        {
            OperateResult<int> tcpsocket = new OperateResult<int>();
            try
            {
                if (plc != null && plc.IsConnected)
                {
                    // 地址预处理：拿到 DB 号与起始字节
                    string[] add = Regex.Replace(address.ToUpper(), "[a-z]", "", RegexOptions.IgnoreCase).Split('.');
                    int db = int.Parse(add[0]);
                    int startByteAdr = int.Parse(add[1]);

                    // 批量写入 float 数组
                    plc.Write(DataType.DataBlock, db, startByteAdr, data);

                    tcpsocket.Content = 1;
                    tcpsocket.Message = "OK";
                    tcpsocket.IsSuccess = true;
                }
                else
                {
                    tcpsocket.IsSuccess = false;
                    tcpsocket.Message = "错误：PLC未连接！";
                }
            }
            catch (Exception ex)
            {
                tcpsocket.Message = ex.Message;
                tcpsocket.IsSuccess = false;
            }
            return tcpsocket;
        }

        /// <summary>
        /// 写入连续的双精度 double 数组（LReal 数组）。
        /// </summary>
        /// <param name="address">起始地址</param>
        /// <param name="length">数组长度（当前实现由 data.Length 决定，此参数保留以对齐接口）</param>
        /// <param name="data">要写入的 double 数组</param>
        /// <returns>OperateResult&lt;int&gt;，Content=1 表示成功</returns>
        public OperateResult<int> WriteLReal(string address, int length, double[] data)
        {
            OperateResult<int> tcpsocket = new OperateResult<int>();
            try
            {
                if (plc != null && plc.IsConnected)
                {
                    // 地址预处理：拿到 DB 号与起始字节
                    string[] add = Regex.Replace(address.ToUpper(), "[a-z]", "", RegexOptions.IgnoreCase).Split('.');
                    int db = int.Parse(add[0]);
                    int startByteAdr = int.Parse(add[1]);

                    // 批量写入 double 数组
                    plc.Write(DataType.DataBlock, db, startByteAdr, data);

                    tcpsocket.Content = 1;
                    tcpsocket.Message = "OK";
                    tcpsocket.IsSuccess = true;
                }
                else
                {
                    tcpsocket.IsSuccess = false;
                    tcpsocket.Message = "错误：PLC未连接！";
                }
            }
            catch (Exception ex)
            {
                tcpsocket.Message = ex.Message;
                tcpsocket.IsSuccess = false;
            }
            return tcpsocket;
        }

        /// <summary>
        /// 写入字符串（层1：2 次 socket 写合并为 1 次）。
        ///
        /// 【S7 String 结构】
        ///   payload[0] = maxLen（这里直接用实际长度，业务上要求目标区 maxLen 足够）
        ///   payload[1] = realLen
        ///   payload[2..] = ASCII 字符数据
        ///
        /// 【层1 优化】原实现分两次 WriteBytes（先写头、再写数据），
        /// 本实现把 2 字节头和数据拼成一个 payload 一次写出，减少一次网络往返。
        /// </summary>
        /// <param name="address">起始地址</param>
        /// <param name="strings">要写入的字符串（ASCII）</param>
        /// <returns>OperateResult&lt;int&gt;，Content=1 表示成功</returns>
        public OperateResult<int> WriteString(string address, string strings)
        {
            OperateResult<int> tcpsocket = new OperateResult<int>();
            try
            {
                if (plc != null && plc.IsConnected)
                {
                    // 地址预处理：拿到 DB 号与起始字节
                    string[] add = Regex.Replace(address.ToUpper(), "[a-z]", "", RegexOptions.IgnoreCase).Split('.');
                    int db = int.Parse(add[0]);
                    int startByteAdr = int.Parse(add[1]);

                    // 字符串转 ASCII 字节
                    byte[] byteArray = Encoding.ASCII.GetBytes(strings);

                    // 组装完整 payload：2 字节头 + 数据
                    byte[] payload = new byte[2 + byteArray.Length];
                    payload[0] = (byte)byteArray.Length;   // 最大长度（此处取实际长度）
                    payload[1] = (byte)byteArray.Length;   // 实际长度
                    if (byteArray.Length > 0)
                    {
                        // 数据区紧跟头部
                        Array.Copy(byteArray, 0, payload, 2, byteArray.Length);
                    }

                    // 一次 socket 写完成全部内容
                    plc.WriteBytes(DataType.DataBlock, db, startByteAdr, payload);

                    tcpsocket.Content = 1;
                    tcpsocket.Message = "OK";
                    tcpsocket.IsSuccess = true;
                }
                else
                {
                    tcpsocket.IsSuccess = false;
                    tcpsocket.Message = "错误：PLC未连接！";
                }
            }
            catch (Exception ex)
            {
                tcpsocket.Message = ex.Message;
                tcpsocket.IsSuccess = false;
            }
            return tcpsocket;
        }

        /// <summary>
        /// 写入 WString 类型（Unicode，每个字符 2 字节）。
        ///
        /// 【S7 WString 结构】
        ///   byte[0..1] = maxLen（大端 ushort，字符数）
        ///   byte[2..3] = realLen（大端 ushort，字符数）
        ///   byte[4..]  = UTF-16BE 字符数据
        ///
        /// 注意：长度单位是"字符数"而不是"字节数"。
        /// </summary>
        /// <param name="address">起始地址</param>
        /// <param name="strings">要写入的字符串</param>
        /// <returns>OperateResult&lt;int&gt;，Content=1 表示成功</returns>
        public OperateResult<int> WriteWString(string address, string strings)
        {
            OperateResult<int> result = new OperateResult<int>();

            // 参数校验：地址不能为空
            string errorMessage = ValidateParameters(address);
            if (!string.IsNullOrEmpty(errorMessage))
            {
                result.Message = errorMessage;
                result.IsSuccess = false;
                return result;
            }

            try
            {
                // 连接检查
                if (plc == null || !plc.IsConnected)
                {
                    result.Message = "错误：PLC未连接！";
                    result.IsSuccess = false;
                    return result;
                }

                // 地址预处理：去掉字母后按 '.' 拆分
                string[] parts = Regex.Replace(address.ToUpper(), "[a-z]", "", RegexOptions.IgnoreCase).Split('.');
                if (parts.Length != 2)
                {
                    // 必须能拆成两段：DB 号 + 起始字节
                    result.Message = "错误：地址格式不正确（应为 DB号.偏移量，如 1.0）";
                    result.IsSuccess = false;
                    return result;
                }

                int db = int.Parse(parts[0]);        // DB 块号
                int startByte = int.Parse(parts[1]); // 起始字节

                // WString 用大端 UTF-16 编码
                byte[] stringBytes = Encoding.BigEndianUnicode.GetBytes(strings);
                int charCount = strings.Length;      // 字符数（不是字节数）

                // 头部两个 ushort：maxLen 与 curLen，这里都等于实际字符数
                ushort maxLen = (ushort)charCount;
                ushort curLen = (ushort)charCount;

                // 手工把 ushort 按大端拆成字节
                byte[] header = new byte[4];
                header[0] = (byte)(maxLen >> 8);    // maxLen 高字节
                header[1] = (byte)(maxLen & 0xFF);  // maxLen 低字节
                header[2] = (byte)(curLen >> 8);    // curLen 高字节
                header[3] = (byte)(curLen & 0xFF);  // curLen 低字节

                // 组装完整 payload：4 字节头 + UTF-16BE 数据
                byte[] wstringPayload = new byte[4 + stringBytes.Length];
                Array.Copy(header, 0, wstringPayload, 0, 4);
                Array.Copy(stringBytes, 0, wstringPayload, 4, stringBytes.Length);

                // 一次写入
                plc.WriteBytes(DataType.DataBlock, db, startByte, wstringPayload);

                result.Content = 1;
                result.Message = "OK";
                result.IsSuccess = true;
            }
            catch (SocketException ex)
            {
                // 细分 socket 异常：超时单独给出提示，便于现场判断
                result.Message = ex.SocketErrorCode == SocketError.TimedOut
                    ? "错误：PLC接收超时"
                    : $"错误：Socket异常 - {ex.Message}";
                result.IsSuccess = false;
            }
            catch (FormatException ex)
            {
                // 地址里的 DB 号/偏移量不是数字
                result.Message = $"错误：地址格式不正确 - {ex.Message}";
                result.IsSuccess = false;
            }
            catch (Exception ex)
            {
                // 其余异常统一兜底
                result.Message = $"错误：siemensPLC写入字符串数据信息出错 - {ex.Message}";
                result.IsSuccess = false;
            }

            return result;
        }

        #endregion

        #region  SIMATIC S7异步硬超时读写
        /*        
            本版改法：
              1. 用 Task.Run 包装同步 Read/Write —— 不依赖任何异步 API 签名
              2. Task.WhenAny(operationTask, Task.Delay(timeoutMs)) 做硬超时
              3. 超时后反射强制关闭 Plc.tcpClient
                 —— 底层同步 Read 会立即抛 SocketException 返回，
                    线程池线程恢复，不会留下"幽灵任务"
              4. 置 IsConnectionBrokenByTimeout=true，调用方必须重连
              5. 下次 initializeConnect 成功后自动清零

            优点：
              - 兼容任何 S7NetPlus 版本，编译必过
              - 业务行为与直接用 ReadAsync 一致（单次最多卡 timeoutMs）

            注意事项：
              - Task.Delay 未传 CancellationToken，超时后定时器仍会走完，
                毫秒级开销可忽略，换来的是实现简单。
              - 硬超时会破坏 socket，因此同一连接上后续操作必须重连。
       ********************************************************/

        /// <summary>
        /// 反射获取 Plc 内部的 TcpClient 并强制 Close，打断底层阻塞 IO。
        ///
        /// 原理：S7.Net 的 Plc 把 TcpClient 放在私有字段 tcpClient 中，
        ///       关闭它会让正在阻塞的 Socket.Receive 立即抛异常返回，
        ///       从而释放被 Task.Run 占用的线程池线程。
        ///
        /// 注意：本方法吞掉所有异常，保证在超时路径上不会二次抛出。
        /// </summary>
        /// <param name="plcInstance">要打断的 Plc 实例，可为 null</param>
        private static void ForceCloseTcpClient(Plc plcInstance)
        {
            if (plcInstance == null) return;   // 实例为空直接返回
            try
            {
                // 反射拿私有实例字段 tcpClient
                var field = typeof(Plc).GetField(
                    "tcpClient",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
                if (field != null)
                {
                    var tcpClient = field.GetValue(plcInstance) as TcpClient;
                    if (tcpClient != null)
                    {
                        // 先 Close 再 Dispose，尽量释放干净
                        try { tcpClient.Close(); } catch { }
                        try { tcpClient.Dispose(); } catch { }
                    }
                }
            }
            catch { }   // 反射失败也不能让超时路径崩溃
        }

        /// <summary>
        /// 通用硬超时包装（无返回值，用于 Write 等操作）。
        /// 不依赖 S7NetPlus 异步 API，用 Task.Run 包装同步操作。
        ///
        /// 返回 (completed, error)：
        ///   completed=false               → 硬超时（TcpClient 已被强制关闭，IsConnectionBrokenByTimeout=true）
        ///   completed=true,  error=null   → 成功
        ///   completed=true,  error!=null  → 同步或异步异常（调用方必须把 error.Message 写进 OperateResult）
        /// </summary>
        /// <param name="operationFactory">返回 Task 的工厂方法（内部通常用 Task.Run 包装同步调用）</param>
        /// <param name="timeoutMs">硬超时毫秒数</param>
        /// <param name="plcLocal">本次操作使用的 Plc 实例（超时时要关它的 TcpClient）</param>
        private async Task<(bool completed, Exception error)> AwaitVoidWithHardTimeoutAsync(
            Func<Task> operationFactory, int timeoutMs, Plc plcLocal)
        {
            Task operationTask;
            try
            {
                // 启动操作（通常立即返回一个未完成的 Task）
                operationTask = operationFactory();
            }
            catch (Exception ex)
            {
                // operationFactory 内部同步抛异常（例如地址解析失败）
                // 视作"已完成 + 有异常"，交给调用方处理
                return (true, ex);
            }

            // 谁先完成就用谁：操作本身 或 超时计时器
            var completed = await Task.WhenAny(operationTask, Task.Delay(timeoutMs)).ConfigureAwait(false);

            if (completed == operationTask)
            {
                // 操作先完成：区分成功 / 异常
                try
                {
                    await operationTask.ConfigureAwait(false);
                    return (true, null);          // 正常完成
                }
                catch (Exception ex)
                {
                    return (true, ex);            // 任务异常完成（如 SocketException）
                }
            }

            // ---- 超时分支 ----
            // 观察异常，防止 UnobservedTaskException：
            // 被强关 TcpClient 后原任务会抛异常，这里挂一个 continuation 把异常读掉
            _ = operationTask.ContinueWith(t =>
            {
                try { _ = t.Exception; } catch { }
            }, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);

            // 强制关闭底层 socket，让阻塞中的操作尽快返回
            ForceCloseTcpClient(plcLocal);
            // 标记连接已损坏，调用方必须重连
            IsConnectionBrokenByTimeout = true;
            // 返回"未完成 + 超时异常"
            return (false, new TimeoutException($"硬超时({timeoutMs}ms)"));
        }

        /// <summary>
        /// 通用硬超时包装（泛型，用于 Read 等有返回值的操作）。
        /// 不依赖 S7NetPlus 异步 API，用 Task.Run 包装同步操作。
        ///
        /// 返回 (success, task)：
        ///   success=false → 硬超时（TcpClient 已被强制关闭，IsConnectionBrokenByTimeout=true），
        ///                   调用方直接返回超时错误，不要 await task
        ///   success=true  → 操作已完成（成功或异常），调用方 await task 取结果或捕获异常
        /// </summary>
        /// <param name="operationFactory">返回 Task&lt;T&gt; 的工厂方法</param>
        /// <param name="timeoutMs">硬超时毫秒数</param>
        /// <param name="plcLocal">本次操作使用的 Plc 实例</param>
        private async Task<(bool success, Task<T> task)> AwaitWithHardTimeoutAsync<T>(
            Func<Task<T>> operationFactory, int timeoutMs, Plc plcLocal)
        {
            Task<T> operationTask;
            try
            {
                // 启动操作
                operationTask = operationFactory();
            }
            catch (Exception ex)
            {
                // 同步异常：构造一个已失败的 Task，让上层 await 时拿到同样的异常
                var tcs = new TaskCompletionSource<T>();
                tcs.SetException(ex);
                return (true, tcs.Task);   // 让上层 await 时拿到异常
            }

            // 竞速：操作 vs 超时
            var completed = await Task.WhenAny(operationTask, Task.Delay(timeoutMs)).ConfigureAwait(false);
            if (completed == operationTask) return (true, operationTask);   // 操作先完成

            // ---- 超时分支 ----
            // 观察异常，防止 UnobservedTaskException
            _ = operationTask.ContinueWith(t =>
            {
                try { _ = t.Exception; } catch { }
            }, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);

            // 强关 socket，打断底层阻塞读
            ForceCloseTcpClient(plcLocal);
            IsConnectionBrokenByTimeout = true;
            return (false, operationTask);   // success=false 告诉上层这是超时
        }


        /// <summary>
        /// 异步读取单个 32 位整型。Task.Run 包装同步 Read，硬超时强关 TcpClient。
        /// </summary>
        /// <param name="address">PLC 地址，如 "DB1.DBD0"</param>
        /// <param name="timeoutMs">硬超时毫秒数；≤0 时取 S7TimeoutConfig.ReadInt32</param>
        /// <returns>OperateResult&lt;int&gt;</returns>
        public async Task<OperateResult<int>> ReadInt32Async(string address, int timeoutMs = 0)
        {
            // 未显式指定超时则使用全局配置
            if (timeoutMs <= 0) timeoutMs = S7TimeoutConfig.ReadInt32;

            OperateResult<int> result = new OperateResult<int>();

            // 快照当前 plc，避免执行期间 plc 被其他线程替换
            Plc plcLocal = plc;
            if (plcLocal == null || !plcLocal.IsConnected)
            {
                result.IsSuccess = false;
                result.Message = "错误：PLC未连接！";
                return result;
            }

            // 启动带硬超时的读操作
            var (ok, task) = await AwaitWithHardTimeoutAsync(
                () => Task.Run(() => plcLocal.Read(address.ToUpper())),
                timeoutMs, plcLocal).ConfigureAwait(false);

            if (!ok)
            {
                // 硬超时：底层 socket 已被关，必须重连
                result.IsSuccess = false;
                result.Message = $"硬超时({timeoutMs}ms) addr={address}";
                return result;
            }

            try
            {
                // 等待任务结果（成功或抛出真实异常）
                object raw = await task.ConfigureAwait(false);
                // object → uint → int，正确处理 DInt 符号位
                result.Content = ((uint)raw).ConvertToInt();
                result.IsSuccess = true;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Message = ex.Message;
            }
            return result;
        }

        /// <summary>
        /// 异步读取连续的 32 位整型数组。
        /// </summary>
        /// <param name="address">起始地址</param>
        /// <param name="length">要读取的 DInt 个数</param>
        /// <param name="timeoutMs">硬超时毫秒数；≤0 时取 S7TimeoutConfig.Array</param>
        /// <returns>OperateResult&lt;int[]&gt;</returns>
        public async Task<OperateResult<int[]>> ReadInt32Async(string address, int length, int timeoutMs = 0)
        {
            // 数组读写走统一的 Array 超时配置
            if (timeoutMs <= 0) timeoutMs = S7TimeoutConfig.Array;

            OperateResult<int[]> result = new OperateResult<int[]>();

            // 快照 plc
            Plc plcLocal = plc;
            if (plcLocal == null || !plcLocal.IsConnected)
            {
                result.IsSuccess = false;
                result.Message = "错误：PLC未连接！";
                return result;
            }

            // 地址解析：提前做，避免把明显错误的地址送进 Task.Run
            string[] add = Regex.Replace(address.ToUpper(), "[a-z]", "", RegexOptions.IgnoreCase).Split('.');
            if (add.Length != 2)
            {
                result.IsSuccess = false;
                result.Message = "错误：地址格式不正确";
                return result;
            }
            int db = int.Parse(add[0]);
            int startByteAdr = int.Parse(add[1]);

            // 启动带硬超时的批量读
            var (ok, task) = await AwaitWithHardTimeoutAsync(
                () => Task.Run(() => plcLocal.Read(DataType.DataBlock, db, startByteAdr, VarType.DInt, length)),
                timeoutMs, plcLocal).ConfigureAwait(false);

            if (!ok)
            {
                result.IsSuccess = false;
                result.Message = $"硬超时({timeoutMs}ms) addr={address} len={length}";
                return result;
            }

            try
            {
                object raw = await task.ConfigureAwait(false);
                // length==1 时 S7.Net 返回单值，需要包装成数组
                result.Content = length == 1 ? new int[1] { Convert.ToInt32(raw) } : (int[])raw;
                result.IsSuccess = true;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Message = ex.Message;
            }
            return result;
        }

        /// <summary>
        /// 异步读取连续的 Real（float）数组。
        /// </summary>
        /// <param name="address">起始地址</param>
        /// <param name="length">要读取的 Real 个数</param>
        /// <param name="timeoutMs">硬超时毫秒数；≤0 时取 S7TimeoutConfig.Array</param>
        /// <returns>OperateResult&lt;float[]&gt;</returns>
        public async Task<OperateResult<float[]>> ReadRealAsync(string address, int length, int timeoutMs = 0)
        {
            // 数组读写走统一的 Array 超时配置
            if (timeoutMs <= 0) timeoutMs = S7TimeoutConfig.Array;

            OperateResult<float[]> result = new OperateResult<float[]>();

            // 快照 plc
            Plc plcLocal = plc;
            if (plcLocal == null || !plcLocal.IsConnected)
            {
                result.IsSuccess = false;
                result.Message = "错误：PLC未连接！";
                return result;
            }

            // 地址解析
            string[] add = Regex.Replace(address.ToUpper(), "[a-z]", "", RegexOptions.IgnoreCase).Split('.');
            if (add.Length != 2)
            {
                result.IsSuccess = false;
                result.Message = "错误：地址格式不正确";
                return result;
            }
            int db = int.Parse(add[0]);
            int startByteAdr = int.Parse(add[1]);

            // 启动带硬超时的批量读
            var (ok, task) = await AwaitWithHardTimeoutAsync(
                () => Task.Run(() => plcLocal.Read(DataType.DataBlock, db, startByteAdr, VarType.Real, length)),
                timeoutMs, plcLocal).ConfigureAwait(false);

            if (!ok)
            {
                result.IsSuccess = false;
                result.Message = $"硬超时({timeoutMs}ms) addr={address} len={length}";
                return result;
            }

            try
            {
                object raw = await task.ConfigureAwait(false);
                // length==1 时包装成数组
                result.Content = length == 1 ? new float[1] { (float)raw } : (float[])raw;
                result.IsSuccess = true;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Message = ex.Message;
            }
            return result;
        }

        /// <summary>
        /// 异步读取西门子 String 类型（一次读 length+2 字节）。
        /// </summary>
        /// <param name="address">起始地址</param>
        /// <param name="length">String 最大字符数（不含 2 字节头）</param>
        /// <param name="timeoutMs">硬超时毫秒数；≤0 时取 S7TimeoutConfig.ReadString</param>
        /// <returns>OperateResult&lt;string&gt;</returns>
        public async Task<OperateResult<string>> ReadStringAsync(string address, int length, int timeoutMs = 0)
        {
            // 单值 String 读走 ReadString 超时配置
            if (timeoutMs <= 0) timeoutMs = S7TimeoutConfig.ReadString;

            OperateResult<string> result = new OperateResult<string>();
            result.Content = "";   // 默认空串

            // 快照 plc
            Plc plcLocal = plc;
            if (plcLocal == null || !plcLocal.IsConnected)
            {
                result.IsSuccess = false;
                result.Message = "错误：PLC未连接！";
                return result;
            }

            // 地址解析
            string[] add = Regex.Replace(address.ToUpper(), "[a-z]", "", RegexOptions.IgnoreCase).Split('.');
            if (add.Length != 2)
            {
                result.IsSuccess = false;
                result.Message = "错误：地址格式不正确";
                return result;
            }
            int db = int.Parse(add[0]);
            int startByteAdr = int.Parse(add[1]);

            // 启动带硬超时的读（一次读 length+2 字节，层1 优化）
            var (ok, task) = await AwaitWithHardTimeoutAsync(
                () => Task.Run(() => plcLocal.ReadBytes(DataType.DataBlock, db, startByteAdr, length + 2)),
                timeoutMs, plcLocal).ConfigureAwait(false);

            if (!ok)
            {
                result.IsSuccess = false;
                result.Message = $"硬超时({timeoutMs}ms) addr={address}";
                return result;
            }

            try
            {
                byte[] buffer = await task.ConfigureAwait(false);
                // 防御：至少要有 2 字节头
                if (buffer == null || buffer.Length < 2)
                {
                    result.IsSuccess = false;
                    result.Message = "错误：读取String返回字节不足";
                    return result;
                }

                byte realLen = buffer[1];   // 实际长度
                // 防御：实际长度不能超过声明长度，也不能超过缓冲区剩余
                if (realLen > length) realLen = (byte)length;
                if (realLen > buffer.Length - 2) realLen = (byte)(buffer.Length - 2);

                // 从第 2 字节起解码 ASCII
                result.Content = realLen == 0 ? "" : Encoding.ASCII.GetString(buffer, 2, realLen);
                result.IsSuccess = true;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Message = ex.Message;
            }
            return result;
        }

        /// <summary>
        /// 异步批量读取连续的多个 S7 String。
        /// </summary>
        /// <param name="db">DB 块号</param>
        /// <param name="startByteAdr">第一个 String 的起始字节</param>
        /// <param name="stride">每个 String 占用的字节数（含 2 字节头）</param>
        /// <param name="count">要读取的 String 个数</param>
        /// <param name="timeoutMs">硬超时毫秒数；≤0 时取 S7TimeoutConfig.ReadStringsBatch</param>
        /// <returns>OperateResult&lt;string[]&gt;</returns>
        public async Task<OperateResult<string[]>> ReadStringsBatchAsync(
            int db, int startByteAdr, int stride, int count, int timeoutMs = 0)
        {
            // 批量读走 ReadStringsBatch 超时配置（默认 6000ms）
            if (timeoutMs <= 0) timeoutMs = S7TimeoutConfig.ReadStringsBatch;

            OperateResult<string[]> result = new OperateResult<string[]>();
            result.Content = new string[0];   // 默认空数组

            // 快照 plc
            Plc plcLocal = plc;
            if (plcLocal == null || !plcLocal.IsConnected)
            {
                result.IsSuccess = false;
                result.Message = "错误：PLC未连接！";
                return result;
            }

            // 参数校验
            if (stride < 2 || count <= 0)
            {
                result.IsSuccess = false;
                result.Message = $"错误：参数无效 stride={stride}, count={count}";
                return result;
            }

            // 单次 PDU 上限校验
            int totalBytes = stride * count;
            if (totalBytes > 2000)
            {
                result.IsSuccess = false;
                result.Message = $"错误：批量读总字节数 {totalBytes} 过大，应回退到单个读";
                return result;
            }

            // 启动带硬超时的批量读
            var (ok, task) = await AwaitWithHardTimeoutAsync(
                () => Task.Run(() => plcLocal.ReadBytes(DataType.DataBlock, db, startByteAdr, totalBytes)),
                timeoutMs, plcLocal).ConfigureAwait(false);

            if (!ok)
            {
                result.IsSuccess = false;
                result.Message = $"硬超时({timeoutMs}ms) db={db} off={startByteAdr} count={count}";
                return result;
            }

            try
            {
                byte[] buffer = await task.ConfigureAwait(false);
                // 防御：字节数不足
                if (buffer == null || buffer.Length < totalBytes)
                {
                    result.IsSuccess = false;
                    result.Message = $"错误：批量读返回字节不足，期望{totalBytes}，实际{buffer?.Length ?? 0}";
                    return result;
                }

                // 逐个解出每个 String
                string[] strings = new string[count];
                for (int i = 0; i < count; i++)
                {
                    int offset = i * stride;                 // 第 i 个 String 的起始偏移
                    byte realLen = buffer[offset + 1];       // 实际长度
                    if (realLen > stride - 2) realLen = (byte)(stride - 2); // 防御越界
                    strings[i] = realLen == 0 ? "" : Encoding.ASCII.GetString(buffer, offset + 2, realLen);
                }
                result.Content = strings;
                result.IsSuccess = true;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Message = ex.Message;
            }
            return result;
        }

        // ---------- 异步写 ----------

        /// <summary>
        /// 异步写入单个 32 位整型。
        /// </summary>
        /// <param name="address">目标地址</param>
        /// <param name="data">要写入的 int 值</param>
        /// <param name="timeoutMs">硬超时毫秒数；≤0 时取 S7TimeoutConfig.WriteInt32</param>
        /// <returns>OperateResult&lt;int&gt;，Content=1 表示成功</returns>
        public async Task<OperateResult<int>> WriteInt32Async(string address, int data, int timeoutMs = 0)
        {
            // 单值写走 WriteInt32 超时配置
            if (timeoutMs <= 0) timeoutMs = S7TimeoutConfig.WriteInt32;

            OperateResult<int> result = new OperateResult<int>();

            // 快照 plc
            Plc plcLocal = plc;
            if (plcLocal == null || !plcLocal.IsConnected)
            {
                result.IsSuccess = false;
                result.Message = "错误：PLC未连接！";
                return result;
            }

            // 启动带硬超时的写
            var (completed, error) = await AwaitVoidWithHardTimeoutAsync(
                () => Task.Run(() => plcLocal.Write(address.ToUpper(), data)),
                timeoutMs, plcLocal).ConfigureAwait(false);

            if (!completed)
            {
                // 硬超时：socket 已被关，必须重连
                result.IsSuccess = false;
                result.Message = $"硬超时({timeoutMs}ms) addr={address}";
                return result;
            }
            if (error != null)
            {
                // 真实异常透传给上层（如 SocketException / 地址格式错误）
                result.IsSuccess = false;
                result.Message = error.Message;   // ★ 真实异常透传给上层
                return result;
            }

            // 正常完成
            result.Content = 1;
            result.Message = "OK";
            result.IsSuccess = true;
            return result;
        }

        /// <summary>
        /// 异步写入连续的 32 位整型数组。
        /// </summary>
        /// <param name="address">起始地址</param>
        /// <param name="length">数组长度</param>
        /// <param name="data">要写入的 int 数组</param>
        /// <param name="timeoutMs">硬超时毫秒数；≤0 时取 S7TimeoutConfig.Array</param>
        /// <returns>OperateResult&lt;int&gt;，Content=1 表示成功</returns>
        public async Task<OperateResult<int>> WriteInt32Async(string address, int length, int[] data, int timeoutMs = 0)
        {
            // 数组写走 Array 超时配置
            if (timeoutMs <= 0) timeoutMs = S7TimeoutConfig.Array;

            OperateResult<int> result = new OperateResult<int>();

            // 快照 plc
            Plc plcLocal = plc;
            if (plcLocal == null || !plcLocal.IsConnected)
            {
                result.IsSuccess = false;
                result.Message = "错误：PLC未连接！";
                return result;
            }

            // 地址解析
            string[] add = Regex.Replace(address.ToUpper(), "[a-z]", "", RegexOptions.IgnoreCase).Split('.');
            if (add.Length != 2)
            {
                result.IsSuccess = false;
                result.Message = "错误：地址格式不正确";
                return result;
            }
            int db = int.Parse(add[0]);
            int startByteAdr = int.Parse(add[1]);

            // 启动带硬超时的批量写
            var (completed, error) = await AwaitVoidWithHardTimeoutAsync(
                () => Task.Run(() => plcLocal.Write(DataType.DataBlock, db, startByteAdr, data)),
                timeoutMs, plcLocal).ConfigureAwait(false);

            if (!completed)
            {
                result.IsSuccess = false;
                result.Message = $"硬超时({timeoutMs}ms) addr={address} len={length}";
                return result;
            }
            if (error != null)
            {
                result.IsSuccess = false;
                result.Message = error.Message;
                return result;
            }

            result.Content = 1;
            result.Message = "OK";
            result.IsSuccess = true;
            return result;
        }

        /// <summary>
        /// 异步写入 Real（float）数组。
        /// </summary>
        /// <param name="address">起始地址</param>
        /// <param name="length">数组长度</param>
        /// <param name="data">要写入的 float 数组</param>
        /// <param name="timeoutMs">硬超时毫秒数；≤0 时取 S7TimeoutConfig.Array</param>
        /// <returns>OperateResult&lt;int&gt;，Content=1 表示成功</returns>
        public async Task<OperateResult<int>> WriteRealAsync(string address, int length, float[] data, int timeoutMs = 0)
        {
            // 数组写走 Array 超时配置
            if (timeoutMs <= 0) timeoutMs = S7TimeoutConfig.Array;

            OperateResult<int> result = new OperateResult<int>();

            // 快照 plc
            Plc plcLocal = plc;
            if (plcLocal == null || !plcLocal.IsConnected)
            {
                result.IsSuccess = false;
                result.Message = "错误：PLC未连接！";
                return result;
            }

            // 地址解析
            string[] add = Regex.Replace(address.ToUpper(), "[a-z]", "", RegexOptions.IgnoreCase).Split('.');
            if (add.Length != 2)
            {
                result.IsSuccess = false;
                result.Message = "错误：地址格式不正确";
                return result;
            }
            int db = int.Parse(add[0]);
            int startByteAdr = int.Parse(add[1]);

            // 启动带硬超时的批量写
            var (completed, error) = await AwaitVoidWithHardTimeoutAsync(
                () => Task.Run(() => plcLocal.Write(DataType.DataBlock, db, startByteAdr, data)),
                timeoutMs, plcLocal).ConfigureAwait(false);

            if (!completed)
            {
                result.IsSuccess = false;
                result.Message = $"硬超时({timeoutMs}ms) addr={address} len={length}";
                return result;
            }
            if (error != null)
            {
                result.IsSuccess = false;
                result.Message = error.Message;
                return result;
            }

            result.Content = 1;
            result.Message = "OK";
            result.IsSuccess = true;
            return result;
        }

        /// <summary>
        /// 异步写入 String（合并头部 + 数据，一次 socket 写）。
        ///
        /// 【payload 结构】
        ///   payload[0] = 最大长度（此处取实际长度）
        ///   payload[1] = 实际长度
        ///   payload[2..] = ASCII 字符数据
        /// </summary>
        /// <param name="address">起始地址</param>
        /// <param name="value">要写入的字符串（null 按空串处理）</param>
        /// <param name="timeoutMs">硬超时毫秒数；≤0 时取 S7TimeoutConfig.WriteString</param>
        /// <returns>OperateResult&lt;int&gt;，Content=1 表示成功</returns>
        public async Task<OperateResult<int>> WriteStringAsync(string address, string value, int timeoutMs = 0)
        {
            // 单值 String 写走 WriteString 超时配置
            if (timeoutMs <= 0) timeoutMs = S7TimeoutConfig.WriteString;

            OperateResult<int> result = new OperateResult<int>();

            // 快照 plc
            Plc plcLocal = plc;
            if (plcLocal == null || !plcLocal.IsConnected)
            {
                result.IsSuccess = false;
                result.Message = "错误：PLC未连接！";
                return result;
            }

            // 地址解析
            string[] add = Regex.Replace(address.ToUpper(), "[a-z]", "", RegexOptions.IgnoreCase).Split('.');
            if (add.Length != 2)
            {
                result.IsSuccess = false;
                result.Message = "错误：地址格式不正确";
                return result;
            }
            int db = int.Parse(add[0]);
            int startByteAdr = int.Parse(add[1]);

            // 组装 payload：2 字节头 + ASCII 数据（null 安全）
            byte[] byteArray = Encoding.ASCII.GetBytes(value ?? "");
            byte[] payload = new byte[2 + byteArray.Length];
            payload[0] = (byte)byteArray.Length;   // 最大长度
            payload[1] = (byte)byteArray.Length;   // 实际长度
            if (byteArray.Length > 0) Array.Copy(byteArray, 0, payload, 2, byteArray.Length);

            // 启动带硬超时的写（一次 WriteBytes 完成）
            var (completed, error) = await AwaitVoidWithHardTimeoutAsync(
                () => Task.Run(() => plcLocal.WriteBytes(DataType.DataBlock, db, startByteAdr, payload)),
                timeoutMs, plcLocal).ConfigureAwait(false);

            if (!completed)
            {
                result.IsSuccess = false;
                result.Message = $"硬超时({timeoutMs}ms) addr={address}";
                return result;
            }
            if (error != null)
            {
                result.IsSuccess = false;
                result.Message = error.Message;
                return result;
            }

            result.Content = 1;
            result.Message = "OK";
            result.IsSuccess = true;
            return result;
        }

        #endregion

        #region  SIMATIC S7辅助方法

        /// <summary>
        /// 简单参数校验：目前只检查地址是否为空。
        /// </summary>
        /// <param name="address">待校验的地址</param>
        /// <returns>空字符串表示通过；否则返回错误信息</returns>
        private string ValidateParameters(string address)
        {
            if (string.IsNullOrEmpty(address))
                return "错误：地址不能为空";
            return string.Empty;
        }

        /// <summary>
        /// 字符串 CPU 类型 → CpuType 枚举。
        /// 未识别的型号统一按 S7-1200 处理，避免调用方拿到非法枚举。
        /// </summary>
        /// <param name="cpu">业务层传入的 CPU 型号字符串，如 "S7-1500"</param>
        /// <returns>对应的 S7.Net CpuType 枚举值</returns>
        private CpuType GetCpuType(string cpu)
        {
            // 逐个匹配常见型号；大小写需与业务层保持一致（本处区分大小写）
            switch (cpu)
            {
                case "S7-200": return CpuType.S7200;   // S7-200
                case "S7-300": return CpuType.S7300;   // S7-300
                case "S7-400": return CpuType.S7400;   // S7-400
                case "S7-1200": return CpuType.S71200; // S7-1200
                case "S7-1500": return CpuType.S71500; // S7-1500
                default: return CpuType.S71200;        // 未知型号：兜底用 S7-1200
            }
        }

        #endregion

    }
}