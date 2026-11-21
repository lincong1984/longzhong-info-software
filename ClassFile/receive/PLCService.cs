using System;
using System.Collections.Concurrent;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using TCPconnectSimaticPLC;
using generaControlProcedure;

namespace MES_SCAF
{
    /// <summary>
    /// 逻辑站上下文：保存每个站（PLC站号+线程）特有的地址表、业务数据等，
    /// 并实现原 receiveClass 中的所有业务逻辑（报警/参数/刷卡/点检、结果保存、条码扫描等）。
    ///
    /// ============================================================
    /// 【本次改动汇总】
    /// ============================================================
    /// 2. 新增 ExecuteResultSaveAsync / ExecuteBarcodeScanAsync / ExecuteParameterAndCheckAsync（异步版）
    /// 3. 全文 publicAddress.errtext += → publicAddress.AddLog
    /// 4. 新增 QuickCheckResultSaveAsync / QuickCheckBarcodeScanAsync / QuickCheckSwipeCardAsync
    /// 5. Inspection / PatrolCheckout / Verification 全部改为 Async 版本
    /// 6. WritePlan / ParameterWrite / Checkout 内部改为调用异步 PLC API
    /// ============================================================
    /// </summary>
    public class StationContext
    {
        // ==================== 基本属性 ====================

        /// <summary>线程编号（逻辑站标识，从 1 开始，用于界面数组索引）</summary>
        public int ThreadNum { get; set; }

        /// <summary>PLC 站号（数据库标识，用于查询本站的配置数据）</summary>
        public int PLCstationNum { get; set; }

        /// <summary>低位地址（心跳读取地址，读下位机心跳值）</summary>
        public string LowerAddress { get; set; }

        /// <summary>高位地址（心跳写入地址，回写上位机心跳序号）</summary>
        public string UpperAddress { get; set; }

        /// <summary>地址表（DataTable），存储该站所有 IO 地址配置</summary>
        public DataTable AddressTable { get; set; }

        /// <summary>工序作业指导书 XML 数据表（包含当前站的工序配置）</summary>
        public DataTable IntOprNoXML { get; set; }

        /// <summary>数据集（包含多个表：地址表、检验表、工单XML映射表等）</summary>
        public DataSet DsTable { get; set; }

        /// <summary>PLC 名称（用于日志/界面显示）</summary>
        public string PLCname { get; set; }

        /// <summary>IP 和端口组合字符串（如 "192.168.1.10:8501"）</summary>
        public string IpProt { get; set; }

        /// <summary>最后一次读取到的心跳值（用于界面显示）</summary>
        public int LastHeartbeatValue { get; set; }

        // ==================== 业务相关字段 ====================

        /// <summary>校检模式标记（0=正常模式，1=校检模式），由 VerificationAsync 方法设置</summary>
        private int calibrationTAG = 0;

        /// <summary>工序/工位 ID（用于检测计划切换）</summary>
        private int _modid;

        /// <summary>参数版本号（用于检测参数更新）</summary>
        private float _versionNumber;

        // ★ 新增：参数写互斥锁（防 ModID setter 与 VersionNumber setter 并发写参数）
        private readonly SemaphoreSlim _paramWriteLock = new SemaphoreSlim(1, 1);

        // ★ 新增：最后触发的 ModID（让被新计划顶替的旧任务主动放弃）
        private int _lastModIdTriggered = -1;

        // ==================== 业务1子任务频率控制（以 200ms 为基准单位） ====================

        /// <summary>业务1循环计数器（每 200ms 递增一次，用于取模判断子任务频率）</summary>
        private int _loopCounter = 0;

        /// <summary>点检频率：每 3 次循环（600ms）执行一次</summary>
        private const int FREQ_INSPECTION = 3;

        /// <summary>巡检频率：每 3 次循环（600ms）执行一次</summary>
        private const int FREQ_PATROL = 3;

        /// <summary>校检模式 / 参数版本频率：每 5 次循环（1s）执行一次</summary>
        private const int FREQ_VERIFY = 5;

        /// <summary>刷卡频率：每 1 次循环（200ms）执行一次</summary>
        private const int FREQ_SWIPE_CARD = 1;

        /// <summary>投产数频率：每 25 次循环（5s）执行一次</summary>
        private const int FREQ_PLAN_QTY = 25;

        // ==================== 依赖对象（工具类） ====================

        /// <summary>PLC 读写工具类</summary>
        private PLCRead_WriteClass _plcReadWrite = new PLCRead_WriteClass();

        /// <summary>类型转换工具类</summary>
        private convertType _convertType = new convertType();

        // ==================== 属性 ====================

        /// <summary>
        /// 工序/工位 ID。
        /// 【触发逻辑】当值变化时触发重新加载工序 XML + 计划数据 + 参数写入 + 首检。
        /// </summary>
        public int ModID
        {
            get => _modid;
            set
            {
                // 值变化时才触发重载
                if (_modid != value)
                {
                    // 重新加载本站工序 XML
                    IntOprNoXML.Clear();
                    IntOprNoXML = GetRoutingToIntOprNoXML(PLCstationNum);

                    // 仅当加载成功时才触发后续动作
                    if (IntOprNoXML != null && IntOprNoXML.Rows != null)
                    {
                        // ★ 串行化三个任务，消除同一次切计划内部 A/B/C 抢 PLC 通讯
                        // ★ 记录本次 ModID，让被新计划顶替的旧任务主动放弃
                        _lastModIdTriggered = value;
                        int currentModId = value;

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                if (_lastModIdTriggered != currentModId) return;
                                await PlanDataSendAsync(1).ConfigureAwait(false);

                                if (_lastModIdTriggered != currentModId) return;
                                await ParameterWriteAsync(1).ConfigureAwait(false);

                                if (_lastModIdTriggered != currentModId) return;
                                await CheckoutAsync(0).ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                publicAddress.AddLog($"线程{ThreadNum} 站{PLCstationNum} ModID 切换任务异常:{ex.Message}");
                            }
                        });
                    }
                }
                // 无论是否变化，都记录最新值
                _modid = value;
            }
        }

        /// <summary>
        /// 参数版本号。
        /// 【触发逻辑】值变化时异步触发参数写入（ParameterWrite）。
        /// </summary>
        public float VersionNumber
        {
            get => _versionNumber;
            set
            {
                // 只在值变化时触发参数写（避免每次刷新都触发）
                if (_versionNumber != value) _ = ParameterWriteAsync(1);
                _versionNumber = value;
            }
        }

        // ==================== 公共方法 ====================

        /// <summary>
        /// 更新界面监控状态（联机/断线、心跳值、IP、设备名等）。
        /// </summary>
        public void UpdateTrafficStatus(HeartbeatMonitor monitor, string ip, int port)
        {
            try
            {
                string statusText;
                string color;

                if (monitor == null)
                {
                    // 尚未拿到监控器（启动初期）
                    statusText = "检测中...";
                    color = "Color.Yellow";
                }
                else if (monitor.IsOnline)
                {
                    // 监控器判定在线
                    statusText = "联机";
                    color = "Color.Lime";
                }
                else
                {
                    // 监控器判定离线
                    statusText = "断线";
                    color = "Color.Red";
                }

                // 写入全局界面状态数组（索引 = ThreadNum-1）
                publicAddress.TrafficStatus[ThreadNum - 1, 0] = statusText;                  // 状态文字
                publicAddress.TrafficStatus[ThreadNum - 1, 1] = color;                       // 状态颜色
                publicAddress.TrafficStatus[ThreadNum - 1, 2] = LastHeartbeatValue.ToString(); // 心跳值
                publicAddress.TrafficStatus[ThreadNum - 1, 3] = IpProt;                      // IP:Port
                publicAddress.TrafficStatus[ThreadNum - 1, 4] = PLCname;                     // 设备名
            }
            catch (Exception ex)
            {
                // 界面更新异常不向外抛，仅记录日志
                publicAddress.AddLog($"线程：{ThreadNum} PLC站：{PLCstationNum} 设备：{PLCname} 监控状态更新异常:{ex.Message}");
            }
        }

        /// <summary>
        /// 业务2预检（异步版）。
        ///
        /// 【目的】快速检查是否有工位请求保存结果（category=08 值为 3）。
        /// 【性能】一旦发现任何工位有请求就返回 true，避免遍历所有工位。
        /// 【异常】任何异常都返回 false，静默处理。
        /// </summary>
        public async Task<bool> QuickCheckResultSaveAsync(Simatic_NET plc)
        {
            try
            {
                // 地址表为空直接返回
                if (AddressTable == null || AddressTable.Rows.Count == 0) return false;

                // 取本工位最大值
                int maxWork = GetMaxWorkNumber();
                for (int j = 1; j <= maxWork; j++)
                {
                    // 找当前工位的保存请求地址（category=08）
                    DataRow[] rows = AddressTable.Select($"category='08' and PLCstationNum='{PLCstationNum}' and workNumber='{j}'");
                    if (rows.Length == 0) continue;   // 无该工位配置则跳过

                    string saveAddress = rows[0]["address"].ToString();
                    // 异步读标志
                    var result = await plc.ReadInt32Async(saveAddress.ToUpper()).ConfigureAwait(false);

                    // 读失败视为无请求（整体返回 false）
                    if (result == null || !result.IsSuccess) return false;
                    if (result.Content == 3) return true;   // 有请求
                }
                return false;
            }
            catch { return false; }   // 静默异常
        }

        /// <summary>
        /// 业务3预检（异步版）。
        ///
        /// 【目的】快速检查是否有工位请求扫码（category=19 值为 3）。
        /// 【性能】一旦发现任何工位有请求就返回 true。
        /// </summary>
        public async Task<bool> QuickCheckBarcodeScanAsync(Simatic_NET plc)
        {
            try
            {
                if (AddressTable == null || AddressTable.Rows.Count == 0) return false;

                int maxWork = GetMaxWorkNumber();
                for (int j = 1; j <= maxWork; j++)
                {
                    // 扫码请求地址（category=19）
                    DataRow[] rows = AddressTable.Select($"category='19' and PLCstationNum='{PLCstationNum}' and workNumber='{j}'");
                    if (rows.Length == 0) continue;

                    string scanAddress = rows[0]["address"].ToString();
                    var result = await plc.ReadInt32Async(scanAddress.ToUpper()).ConfigureAwait(false);

                    if (result == null || !result.IsSuccess) return false;
                    if (result.Content == 3) return true;
                }
                return false;
            }
            catch { return false; }
        }

        /// <summary>
        /// 业务1刷卡预检（异步版）。
        ///
        /// 【目的】快速检查是否有工位有刷卡请求（category=38 值 > 0）。
        /// 【覆盖】支持 intOprNoXML 中所有工序行。
        /// </summary>
        public async Task<bool> QuickCheckSwipeCardAsync(Simatic_NET plc)
        {
            try
            {
                if (IntOprNoXML == null || IntOprNoXML.Rows.Count == 0) return false;

                // 遍历所有工序行
                for (int i = 0; i < IntOprNoXML.Rows.Count; i++)
                {
                    // 刷卡标志地址（category=38）
                    DataRow[] rows = AddressTable.Select($"category='38' and workNumber='{IntOprNoXML.Rows[i]["workNumber"]}' and PLCstationNum='{IntOprNoXML.Rows[i]["PLCstationNum"]}'");
                    if (rows.Length == 0) continue;

                    string userTagAddress = rows[0]["address"].ToString();
                    var result = await plc.ReadInt32Async(userTagAddress.ToUpper()).ConfigureAwait(false);

                    if (result == null || !result.IsSuccess) return false;
                    if (result.Content > 0) return true;   // 有刷卡请求
                }
                return false;
            }
            catch { return false; }
        }

        /// <summary>辅助方法：获取当前站点的最大工位号。</summary>
        private int GetMaxWorkNumber()
        {
            try
            {
                if (AddressTable == null || AddressTable.Rows.Count == 0) return 0;
                // 计算列聚合：max(workNumber)，过滤无效值
                return Convert.ToInt32(AddressTable.Compute("max(workNumber)", "isnull(workNumber,0)>0"));
            }
            catch { return 0; }
        }

        /// <summary>
        /// 业务1：报警/参数/刷卡/点检（异步版）。
        ///
        /// 【调度策略】每 200ms 调用一次；各子任务通过 _loopCounter 取模实现不同频率。
        /// 【失败处理】任何子任务抛出异常都被捕获并记录，不影响下一轮循环。
        /// </summary>
        public async Task ExecuteParameterAndCheckAsync(Simatic_NET plc)
        {
            try
            {
                // 计数器自增，到 1000000 后回绕，避免溢出
                _loopCounter++;
                if (_loopCounter >= 1000000) _loopCounter = 0;

                // 按频率执行各子任务
                if (_loopCounter % FREQ_INSPECTION == 0)
                    await InspectionAsync(plc).ConfigureAwait(false);
                if (_loopCounter % FREQ_PATROL == 0)
                    await PatrolCheckoutAsync(plc).ConfigureAwait(false);
                if (_loopCounter % FREQ_VERIFY == 0)
                    await VerificationAsync(plc).ConfigureAwait(false);

                if (_loopCounter % FREQ_SWIPE_CARD == 0)
                {
                    // 先预检，避免无谓的刷卡处理开销
                    if (await QuickCheckSwipeCardAsync(plc).ConfigureAwait(false))
                    {
                        await _plcReadWrite.SwipingCardAsync(plc, IntOprNoXML, AddressTable).ConfigureAwait(false);
                    }
                }

                if (_loopCounter % FREQ_PLAN_QTY == 0)
                {
                    // 每 5 秒写一次计划数/投产数
                    await _plcReadWrite.WritePlanBatchChargeQtyAsync(plc, IntOprNoXML, AddressTable).ConfigureAwait(false);
                }

                // 每次循环都更新 ModID（若变化则触发重新加载）
                ModID = publicAddress.Modid;

                if (_loopCounter % FREQ_VERIFY == 0)
                {
                    // 定期刷新参数版本号
                    SQLToolsYY sqlToolsYY2 = new SQLToolsYY();
                    DataTable dt1 = new DataTable();

                    Hint hint2 = sqlToolsYY2.GetDataTable("", "exec fynP_getParameterVersionNumber " + PLCstationNum.ToString(), true, out dt1);

                    // 查询有效且有数据时才更新版本号
                    if (hint2.Valid && dt1.Rows.Count > 0)
                        VersionNumber = Convert.ToSingle(dt1.Rows[0]["versionNumber"]);
                }

                // 每 10 个包执行一次报警保存
                if (publicAddress.package % 10 == 0)
                {
                    await _plcReadWrite.SaveErrorAsync(plc, PLCstationNum, AddressTable, GetIPFromStation(), GetPortFromStation()).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                // 顶层兜底：任何异常不向外抛，仅记日志
                publicAddress.AddLog($"线程：{ThreadNum} PLC站：{PLCstationNum} 设备：{PLCname} 业务1异常:{ex.Message}");
            }
        }

        // ==================== 业务2：异步版（生产路径） ====================

        /// <summary>
        /// 业务2：结果保存（异步版）。
        /// 调用 PLCRead_WriteClass.saveResultsAsync，所有 PLC 读写为异步硬超时（2 秒）。
        /// 单次超时后 Simatic_NET 会强制关闭 TcpClient，
        /// 由 PLCService.EnsureBusiness2ConnectionAsync 检测到后自动重连。
        ///
        /// 【分支】
        ///   calibrationTAG == 0 → saveResultsAsync（正常）
        ///   calibrationTAG == 1 → calibrationSaveResultsAsync（标样）
        /// </summary>
        public async Task ExecuteResultSaveAsync(Simatic_NET plc)
        {
            try
            {
                // 根据校检标志选择分支
                switch (calibrationTAG)
                {
                    case 0:
                        // 正常模式：调用普通版结果保存
                        await _plcReadWrite.SaveResultsAsync(
                            plc, PLCstationNum, AddressTable,
                            GetIPFromStation(), GetPortFromStation(), IntOprNoXML).ConfigureAwait(false);
                        break;
                    case 1:
                        // 标样模式：调用标样版结果保存
                        await _plcReadWrite.CalibrationSaveResultsAsync(
                            plc, PLCstationNum, AddressTable,
                            GetIPFromStation(), GetPortFromStation(), IntOprNoXML).ConfigureAwait(false);
                        break;
                }
            }
            catch (Exception ex)
            {
                publicAddress.AddLog($"线程：{ThreadNum} 结果保存异步异常：{ex.Message}");
            }
        }

        // ==================== 业务3：异步版（生产路径） ====================

        /// <summary>
        /// 业务3：条码扫描（异步版）。
        /// 调用 PLCRead_WriteClass.scanBarCcodeAsync，除单次异步硬超时外，
        /// 还带有方法级 10 秒硬截断，防止 20 个字段串行累加拖到 40~50 秒。
        ///
        /// 【分支】
        ///   calibrationTAG == 0 → scanBarCcodeAsync（正常）
        ///   calibrationTAG == 1 → calibrationScanBarCcodeAsync（标样）
        /// </summary>
        public async Task ExecuteBarcodeScanAsync(Simatic_NET plc)
        {
            try
            {
                // 根据校检标志选择分支
                switch (calibrationTAG)
                {
                    case 0:
                        // 正常模式扫码
                        await _plcReadWrite.ScanBarCcodeAsync(
                            plc, PLCstationNum, AddressTable, IntOprNoXML).ConfigureAwait(false);
                        break;
                    case 1:
                        // 标样模式扫码
                        await _plcReadWrite.CalibrationScanBarCcodeAsync(
                            plc, PLCstationNum, AddressTable, IntOprNoXML).ConfigureAwait(false);
                        break;
                }
            }
            catch (Exception ex)
            {
                publicAddress.AddLog($"线程：{ThreadNum} 条码扫描异步异常：{ex.Message}");
            }
        }

        // ==================== 私有业务方法 ====================

        /// <summary>
        /// 点检判断（异步版）。
        ///
        /// 【逻辑】
        ///   1. 查点检计划 fynV_inspection
        ///   2. 遍历每个工位：查当天是否工作日 + 计算时间差
        ///   3. 判定结果：0=不需要/已点检，1=需要点检，2=超时
        ///   4. 写入 category=29 地址
        ///
        /// 【本次改动】
        ///   - DsTable.Tables[0].DefaultView → new DataView(DsTable.Tables[0])
        ///     避免多站点检并发时互相污染共享表的 DefaultView
        ///   - category=29 过滤结果为空时直接跳过该工位，避免 Rows[0] 越界
        /// </summary>
        private async Task InspectionAsync(Simatic_NET plc)
        {
            try
            {
                SQLToolsYY sqlToolsYY = new SQLToolsYY();
                Hint hint = new Hint();
                DataTable dt1 = new DataTable();

                // 查点检计划
                hint = sqlToolsYY.GetDataTable("", " SELECT* FROM fynV_inspection where PLCstationNum = " + PLCstationNum.ToString(), true, out dt1);

                if (hint.Valid)
                {
                    int inspectionValue = 0;
                    DateTime inspectResultTime = new DateTime();

                    // 有点检计划才处理
                    if (dt1.Rows.Count > 0)
                    {
                        // 计算本工位最大值
                        int worknum = Convert.ToInt32(dt1.Compute("max(workNumber)", "isnull(workNumber,0)>0"));

                        for (int i = 1; i <= worknum; i++)
                        {
                            // ★ 独立 DataView，不污染共享地址表的 DefaultView
                            DataView dv1 = new DataView(DsTable.Tables[0]);
                            dv1.RowFilter = "category='29' and workNumber='" + i.ToString() + "' and  PLCstationNum ='" + PLCstationNum.ToString() + "'";
                            DataTable inspectionAddrTable = dv1.ToTable();

                            // ★ 空检查：该工位未配 category=29 时跳过，不再抛 IndexOutOfRange
                            if (inspectionAddrTable.Rows.Count == 0)
                            {
                                publicAddress.AddLog(
                                    $"站{PLCstationNum} 工位{i} 未配置点检地址(category=29)，跳过错点检");
                                continue;
                            }
                            string inspectionAddr = inspectionAddrTable.Rows[0]["address"].ToString();

                            // dt1 是方法内局部表，DefaultView 不共享，保持原写法
                            DataView dv = dt1.DefaultView;
                            dv.RowFilter = "workNumber=" + i.ToString();
                            DataTable planTable = dv.ToTable();

                            if (planTable.Rows.Count == 0) continue;

                            string inadvance = planTable.Rows[0]["inadvance"].ToString();       // 提前时间
                            string inspectTime = planTable.Rows[0]["inspectTime"].ToString();   // 点检时间
                            int repetition = Convert.ToInt32(planTable.Rows[0]["repetition"]);  // 星期位图

                            bool[] setweek = _convertType.IntToBoolArray(repetition);

                            // 计算今天星期几（0=周日）
                            string[] Day = new string[] { "0", "1", "2", "3", "4", "5", "6" };
                            string week = Day[Convert.ToInt32(DateTime.Now.DayOfWeek.ToString("d"))].ToString();

                            if (setweek[Convert.ToInt32(week)])
                            {
                                // 当天需要点检：查最近一次点检记录
                                DataTable dt = new DataTable();
                                hint = sqlToolsYY.GetDataTable("", "select top 1 * from fynV_inspectionResult where PLCstationNum ='" + PLCstationNum.ToString() + "' and workNumber='" + i.ToString() + "' and convert(date,[time])='" + DateTime.Now.ToShortDateString() + "' order by [time] desc", true, out dt);

                                if (hint.Valid && dt.Rows.Count > 0)
                                    inspectResultTime = DateTime.Parse(dt.Rows[0]["time"].ToString());

                                // 时间差计算：判断是否需要点检、是否超时
                                TimeSpan t1 = DateTime.Now - DateTime.Parse(inadvance);        // 当前距提前时间
                                TimeSpan t2 = DateTime.Now - DateTime.Parse(inspectTime);      // 当前距规定时间
                                TimeSpan t3 = inspectResultTime - DateTime.Parse(inadvance);   // 实际点检距提前时间
                                TimeSpan t4 = inspectResultTime - DateTime.Parse(inspectTime); // 实际点检距规定时间

                                if (t1.TotalSeconds > 0)
                                {
                                    // 已到提前时间：需要点检
                                    inspectionValue = 1;
                                    // 已点检过（在提前时间之后、规定时间之前完成）
                                    if (t3.TotalSeconds > 0 && t4.TotalSeconds < 0)
                                        inspectionValue = 0;
                                }

                                // 超过规定时间且尚未在区间内点检 → 超时
                                if (t2.TotalSeconds > 0 && t3.TotalSeconds < 0)
                                    inspectionValue = 2;
                            }
                            else
                            {
                                inspectionValue = 0;   // 当天不需要点检
                            }

                            // 改异步：写入点检值
                            await plc.WriteInt32Async(inspectionAddr.ToUpper(), inspectionValue).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                publicAddress.AddLog(ex.Message);
            }
        }

        /// <summary>
        /// 巡检判断（含首检，异步版）。
        ///
        /// 【第一部分：巡检】
        ///   根据 fynV_statisticalMagnitude 累计量判断是否到了巡检间隔，
        ///   到间隔则触发新巡检（category=33 写 1）；
        ///   未到间隔则查未完成巡检状态，按状态写入结果（category=40）。
        ///
        /// 【第二部分：首检】
        ///   查未完成首检，按状态（7/8/9/10）计算首检结果，写入 category=11；
        ///   首检完成（结果=1）时清零触发和数量（category=09/10）。
        /// </summary>
        private async Task PatrolCheckoutAsync(Simatic_NET plc)
        {
            try
            {
                if (IntOprNoXML == null || IntOprNoXML.Rows.Count == 0) return;
                Hint hint = new Hint();
                SQLToolsYY sqlToolsYY = new SQLToolsYY();
                DataTable dt = new DataTable();
                string patrolCheckoutAddress = "";
                string patrolCheckoutResultAddress = "";

                // ---- 第一部分：巡检 ----
                for (int i = 0; i < IntOprNoXML.Rows.Count; i++)
                {
                    // 查统计量
                    string SQLstr = "select * from fynV_statisticalMagnitude where intOprNo='" + IntOprNoXML.Rows[i]["intOprNo"].ToString() + "'";
                    hint = sqlToolsYY.GetDataTable("", SQLstr, true, out dt);

                    if (hint.Valid && dt.Rows.Count > 0)
                    {
                        int sumQty = Convert.ToInt32(dt.Rows[0]["sumQty"]);

                        // 取巡检地址（category 33 或 40）
                        DataTable addressDt = DsTable.Tables[0];
                        DataRow[] addressRow = addressDt.Select("(category='33' or category='40') and workNumber='" + IntOprNoXML.Rows[i]["workNumber"].ToString() + "' and  PLCstationNum ='" + IntOprNoXML.Rows[i]["PLCstationNum"].ToString() + "'");

                        // 按 category 分派地址
                        foreach (DataRow row in addressRow)
                        {
                            switch (row["category"].ToString())
                            {
                                case "33": patrolCheckoutAddress = row["address"].ToString(); break;         // 巡检触发
                                case "40": patrolCheckoutResultAddress = row["address"].ToString(); break;   // 巡检结果
                            }
                        }

                        // 查巡检配置
                        hint = sqlToolsYY.GetDataTable("", "SELECT * FROM MesV_CheckoutXMLNEW where intOprNo='" + IntOprNoXML.Rows[i]["intOprNo"].ToString() + "'", true, out dt);

                        if (hint.Valid && dt.Rows.Count > 0)
                        {
                            DataRow cfgRow = dt.Rows[0];
                            switch (Convert.ToInt32(cfgRow["chechkoutType"]))
                            {
                                case 2:
                                case 3:
                                    // 间隔巡检：按累计量取模判断
                                    int intervalWqty = Convert.ToInt32(cfgRow["intervalWqty"]);
                                    int intervalAqty = Convert.ToInt32(cfgRow["intervalAqty"]);
                                    int intervalWqtyDemo = sumQty % intervalWqty;
                                    int intervalAqtyDemo = intervalWqtyDemo == intervalAqty ? 0 : 1;

                                    if (intervalWqtyDemo == 0)
                                    {
                                        // 达到巡检间隔，触发新巡检
                                        SQLstr = @"exec fynP_saveCheckoutPlan '" + IntOprNoXML.Rows[i]["intOprNo"].ToString() + "',2";
                                        hint = sqlToolsYY.GetDataTable("", SQLstr, true, out dt);

                                        if (hint.Valid && Convert.ToInt32(dt.Rows[0][0]) == 1)
                                        {
                                            // 触发巡检 + 复位结果
                                            await plc.WriteInt32Async(patrolCheckoutAddress, 1).ConfigureAwait(false);
                                            await plc.WriteInt32Async(patrolCheckoutResultAddress, 0).ConfigureAwait(false);
                                        }
                                    }
                                    else
                                    {
                                        // 未到间隔，查未完成巡检状态
                                        SQLstr = " exec fynP_checkoutUnfinished '" + IntOprNoXML.Rows[i]["intOprNo"].ToString() + "',2";
                                        hint = sqlToolsYY.GetDataTable("", SQLstr, true, out dt);

                                        if (hint.Valid && dt.Rows.Count > 0)
                                        {
                                            int count = Convert.ToInt32(dt.Rows[0]["checkoutStartQty"]);
                                            int checkoutState = Convert.ToInt32(dt.Rows[0]["checkoutState"]);

                                            if (count > 0)
                                            {
                                                switch (checkoutState)
                                                {
                                                    case 7:
                                                        // 未完成：写结果 0，若正好到间隔量则触发
                                                        await plc.WriteInt32Async(patrolCheckoutResultAddress, 0).ConfigureAwait(false);
                                                        if (intervalAqtyDemo == 0)
                                                            await plc.WriteInt32Async(patrolCheckoutAddress, 2).ConfigureAwait(false);
                                                        break;
                                                    case 8:
                                                        // 完成：写结果 1
                                                        await plc.WriteInt32Async(patrolCheckoutResultAddress, 1).ConfigureAwait(false);
                                                        break;
                                                    case 9:
                                                        // 不合格：写结果 2
                                                        await plc.WriteInt32Async(patrolCheckoutResultAddress, 2).ConfigureAwait(false);
                                                        break;
                                                    case 10:
                                                        // 完成确认：写结果 1，并清零触发
                                                        await plc.WriteInt32Async(patrolCheckoutResultAddress, 1).ConfigureAwait(false);
                                                        await plc.WriteInt32Async(patrolCheckoutAddress, 0).ConfigureAwait(false);
                                                        break;
                                                }
                                            }
                                        }
                                        else
                                        {
                                            // 无未完成巡检：清零
                                            await plc.WriteInt32Async(patrolCheckoutResultAddress, 0).ConfigureAwait(false);
                                            await plc.WriteInt32Async(patrolCheckoutAddress, 0).ConfigureAwait(false);
                                        }
                                    }
                                    break;

                                default:
                                    // 非巡检类型：清零
                                    await plc.WriteInt32Async(patrolCheckoutResultAddress, 0).ConfigureAwait(false);
                                    await plc.WriteInt32Async(patrolCheckoutAddress, 0).ConfigureAwait(false);
                                    break;
                            }
                        }
                    }
                }

                // ==================== 第二部分：首检判断 ====================
                string checkoutResultAddress = "";
                string checkoutAddress = "";
                string checkoutQty = "";

                for (int i = 0; i < IntOprNoXML.Rows.Count; i++)
                {
                    int checkoutResult = 0;

                    // 取首检相关地址（09/10/11）
                    DataTable addressDt = DsTable.Tables[0];
                    DataRow[] addressRow = addressDt.Select("(category='09' or category='10' or category='11') and workNumber='" + IntOprNoXML.Rows[i]["workNumber"].ToString() + "' and  PLCstationNum ='" + IntOprNoXML.Rows[i]["PLCstationNum"].ToString() + "'");

                    foreach (DataRow row in addressRow)
                    {
                        switch (row["category"].ToString())
                        {
                            case "09": checkoutAddress = row["address"].ToString(); break;        // 首检触发
                            case "10": checkoutQty = row["address"].ToString(); break;             // 允许数量
                            case "11": checkoutResultAddress = row["address"].ToString(); break;   // 首检结果
                        }
                    }

                    // 查未完成首检
                    string SQLstr = " exec fynP_checkoutUnfinished '" + IntOprNoXML.Rows[i]["intOprNo"].ToString() + "',1";
                    hint = sqlToolsYY.GetDataTable("", SQLstr, true, out dt);

                    if (hint.Valid && dt.Rows.Count > 0)
                    {
                        int count = Convert.ToInt32(dt.Rows[0]["checkoutStartQty"]);
                        int checkoutState = Convert.ToInt32(dt.Rows[0]["checkoutState"]);

                        hint = sqlToolsYY.GetDataTable("", "SELECT * FROM MesV_CheckoutXMLNEW where intOprNo='" + IntOprNoXML.Rows[i]["intOprNo"].ToString() + "'", true, out dt);
                        int allowqty = 0;

                        if (hint.Valid && dt.Rows.Count > 0)
                        {
                            allowqty = dt.Rows[0]["allowqty"].ToString() == "" ? 0 : Convert.ToInt32(dt.Rows[0]["allowqty"].ToString());

                            // 按状态计算首检结果
                            switch (checkoutState)
                            {
                                case 7:
                                    // 未开始：结果 0
                                    checkoutResult = 0;
                                    break;
                                case 9:
                                    // 不合格：累计达到允许数量时结果 2
                                    if (count >= allowqty) checkoutResult = 2;
                                    break;
                                case 8:
                                    // 完成：累计达到允许数量时结果 3
                                    if (count >= allowqty) checkoutResult = 3;
                                    break;
                                case 10:
                                    // 完成确认：有产出即结果 1
                                    if (count > 0) checkoutResult = 1;
                                    break;
                            }
                        }

                        // 写首检结果
                        await plc.WriteInt32Async(checkoutResultAddress, checkoutResult).ConfigureAwait(false);

                        // 首检已完成，重置触发和数量
                        if (checkoutResult == 1)
                        {
                            await plc.WriteInt32Async(checkoutAddress, 0).ConfigureAwait(false);
                            await plc.WriteInt32Async(checkoutQty, 0).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                publicAddress.AddLog($"PLC站：{PLCstationNum} 设备：{PLCname} 巡检判断:{ex.Message}");
            }
        }

        /// <summary>
        /// 校检模式判断（异步版）。
        ///
        /// 【逻辑】
        ///   1. 查 fynV_getcalibrationXML 拿到上位机校检状态（0~9）
        ///   2. 写 category=30 地址（上位机下发）
        ///   3. 读 category=41 地址（PLC 回传），与上位机值比对
        ///   4. 双方都认可（calibration 在 1~9 之间且等于 PLC 值）→ 进入校检模式
        /// </summary>
        private async Task VerificationAsync(Simatic_NET plc)
        {
            try
            {
                if (IntOprNoXML == null || IntOprNoXML.Rows.Count == 0) return;
                int calibration = 0;
                SQLToolsYY sqlToolsYY = new SQLToolsYY();
                Hint hint = new Hint();
                DataTable dt = new DataTable();
                string sqlStr = "";
                string calibrationTAGAddress = "";
                string PLCcalibrationTAGAddress = "";

                for (int i = 0; i < IntOprNoXML.Rows.Count; i++)
                {
                    // 查校检状态（取最小校准状态）
                    sqlStr = @"select min(calibrationState) from fynV_getcalibrationXML where intOprNo='" + IntOprNoXML.Rows[i]["intOprNo"].ToString() + "'";
                    hint = sqlToolsYY.GetDataTable("", sqlStr, true, out dt);

                    if (hint.Valid && dt.Rows.Count > 0)
                        calibration = Convert.ToInt32(dt.Rows[0][0]);

                    // 写上位机校检状态（category=30）
                    DataRow[] dtRow = AddressTable.Select("PLCstationNum=" + IntOprNoXML.Rows[i]["PLCstationNum"].ToString() + " and workNumber=" + IntOprNoXML.Rows[i]["workNumber"].ToString() + " and category='30'");
                    foreach (DataRow row in dtRow)
                    {
                        calibrationTAGAddress = row["address"].ToString();
                        await plc.WriteInt32Async(calibrationTAGAddress, calibration).ConfigureAwait(false);
                    }

                    // 读 PLC 校检状态（category=41），与上位机比对
                    dtRow = AddressTable.Select("PLCstationNum=" + IntOprNoXML.Rows[i]["PLCstationNum"].ToString() + " and workNumber=" + IntOprNoXML.Rows[i]["workNumber"].ToString() + " and category='41'");
                    foreach (DataRow row in dtRow)
                    {
                        PLCcalibrationTAGAddress = row["address"].ToString();
                        OperateResult<int> r = await plc.ReadInt32Async(PLCcalibrationTAGAddress).ConfigureAwait(false);

                        if (r != null && r.IsSuccess)
                        {
                            // 上位机和 PLC 都认可时才进入校检模式
                            calibrationTAG = calibration > 0 && calibration < 10 && r.Content == calibration ? 1 : 0;
                        }
                    }
                }
            }
            catch
            {
                // 任何异常都退出校检模式，防止误操作
                calibrationTAG = 0;
            }
        }

        /// <summary>
        /// 异步发送计划数据到 PLC（最多重试 3 次）。
        /// 使用独立新建的连接（不占用 PLCService 的共享连接）。
        /// </summary>
        private async Task PlanDataSendAsync(int parameternum)
        {
            try
            {
                await Task.Run(async () =>
                {
                    const int maxRetries = 3;
                    int retryCount = 0;

                    // 入参过滤：只有 parameternum==1 才执行
                    if (parameternum != 1) return;

                    // 重试循环
                    while (retryCount < maxRetries)
                    {
                        Simatic_NET tempPlc = new Simatic_NET();
                        OperateResult tempConnect = new OperateResult();

                        try
                        {
                            if (tempConnect.IsSuccess) tempPlc.ConnectClose();

                            // 建立临时连接
                            tempConnect = PLC_connect(tempPlc, tempConnect);

                            if (!tempConnect.IsSuccess)
                            {
                                // 连接失败：记录日志并重试
                                publicAddress.AddLog($":线程【{ThreadNum}】PLC站号【{PLCstationNum}】计划写入第【{retryCount + 1}】次失败，原因【{tempConnect.Message}】");
                                retryCount++;
                                if (retryCount < maxRetries) await Task.Delay(500);
                                continue;
                            }

                            // 执行写入
                            await WritePlanAsync(tempPlc).ConfigureAwait(false);

                            publicAddress.AddLog($"线程：{ThreadNum} PLC站：{PLCstationNum} 设备：{PLCname} 计划写入成功！");
                            parameternum = 0;
                            break;
                        }
                        catch (Exception ex)
                        {
                            // 单次写异常不继续重试
                            publicAddress.AddLog($":线程【{ThreadNum}】PLC站号【{PLCstationNum}】计划写入异常【{ex.Message}】");
                            break;
                        }
                        finally
                        {
                            // 无论成功失败都关闭临时连接
                            if (tempConnect.IsSuccess) tempPlc.ConnectClose();
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                publicAddress.AddLog($"计划写入定义变量异常:{ex.Message}");
            }
        }

        /// <summary>
        /// 异步写入机种参数到 PLC（最多重试 3 次）。
        /// 使用独立新建的连接。
        /// </summary>
        private async Task ParameterWriteAsync(int parameternum)
        {
            // ★ 新增：拿不到锁说明上一次写还没结束，直接跳过，避免并发
            if (!await _paramWriteLock.WaitAsync(0).ConfigureAwait(false))
            {
                publicAddress.AddLog($"线程：{ThreadNum} PLC站：{PLCstationNum} 设备：{PLCname} 上一次参数写入尚未完成，本次跳过");
                return;
            }
            try
            {
                await Task.Run(async () =>
                {
                    SQLToolsYY sqltoolsyy = new SQLToolsYY();
                    DataTable dt1 = new DataTable();
                    Hint hint = new Hint();
                    string intoerr = "";
                    int retryCount = 0;
                    const int maxRetries = 3;

                    // 重试循环
                    while (parameternum == 1 && retryCount < maxRetries)
                    {
                        Simatic_NET tempPlc = new Simatic_NET();
                        OperateResult tempConnect = new OperateResult();

                        try
                        {
                            if (tempConnect.IsSuccess) tempPlc.ConnectClose();

                            // 建立临时连接
                            tempConnect = PLC_connect(tempPlc, tempConnect);

                            if (!tempConnect.IsSuccess)
                            {
                                publicAddress.AddLog($"线程：{ThreadNum} PLC站：{PLCstationNum} 设备：{PLCname} 参数写入处理:连接PLC失败 {tempConnect.Message}");
                                retryCount++;
                                continue;
                            }

                            // 调 sendParameterAsync（异步版）
                            OperateResult<int> send = await _plcReadWrite.SendParameterAsync(
                                tempPlc,
                                PLCstationNum,
                                AddressTable,
                                GetCRCAddress(),
                                GetSignalAddress()).ConfigureAwait(false);

                            // 非 3 且成功 → 正常返回值；否则走错误码分支
                            if (send.Content != 3 && send.IsSuccess)
                            {
                                switch (send.Content)
                                {
                                    case 1:
                                        // 成功
                                        publicAddress.AddLog($"线程：{ThreadNum} PLC站：{PLCstationNum} 设备：{PLCname} 参数写入成功！");
                                        parameternum = 0;
                                        retryCount = 0;
                                        break;
                                    case 2:
                                        // 失败，重试
                                        publicAddress.AddLog($"线程：{ThreadNum} PLC站：{PLCstationNum} 设备：{PLCname} 第【{retryCount + 1}】次参数写入失败！");
                                        retryCount++;
                                        break;
                                }
                            }
                            else
                            {
                                // 不可恢复的错误码：记错误日志并终止
                                switch (send.Content)
                                {
                                    case -1:
                                        parameternum = -1;
                                        intoerr = "insert into fynT_errorDescribeRecord (PLCstandNanme,errCode,errDescribe,relieve) values (" + PLCstationNum.ToString() + "," + 10002.ToString() + ",'该计划当前无机种参数！',1)";
                                        hint = sqltoolsyy.GetDataTable("", intoerr, true, out dt1);
                                        publicAddress.AddLog($"线程：{ThreadNum} PLC站：{PLCstationNum} 设备：{PLCname} 该计划当前无机种参数！");
                                        retryCount = maxRetries;
                                        break;
                                    case -2:
                                        parameternum = -2;
                                        intoerr = "insert into fynT_errorDescribeRecord (PLCstandNanme,errCode,errDescribe,relieve) values (" + PLCstationNum.ToString() + "," + 10003.ToString() + ",'当前无生产计划！',1)";
                                        hint = sqltoolsyy.GetDataTable("", intoerr, true, out dt1);
                                        publicAddress.AddLog($"线程：{ThreadNum} PLC站：{PLCstationNum} 设备：{PLCname} 当前无生产计划！");
                                        retryCount = maxRetries;
                                        break;
                                    case -3:
                                        parameternum = -3;
                                        intoerr = "insert into fynT_errorDescribeRecord (PLCstandNanme,errCode,errDescribe,relieve) values (" + PLCstationNum.ToString() + "," + 10004.ToString() + ",'参数发送失败！',1)";
                                        hint = sqltoolsyy.GetDataTable("", intoerr, true, out dt1);
                                        publicAddress.AddLog($"线程：{ThreadNum} PLC站：{PLCstationNum} 设备：{PLCname} 参数发送失败！");
                                        retryCount = maxRetries;
                                        break;
                                    case -4:
                                        parameternum = -4;
                                        intoerr = "insert into fynT_errorDescribeRecord (PLCstandNanme,errCode,errDescribe,relieve) values (" + PLCstationNum.ToString() + "," + 10005.ToString() + ",'通讯地址获取错误！',1)";
                                        hint = sqltoolsyy.GetDataTable("", intoerr, true, out dt1);
                                        publicAddress.AddLog($"线程：{ThreadNum} PLC站：{PLCstationNum} 设备：{PLCname} 通讯地址获取错误！");
                                        retryCount = maxRetries;
                                        break;
                                    // ★ 新增：写后未反馈（finishKey 已置 1，下一轮只重读信号不会重写参数，可安全重试）
                                    case 3:
                                        retryCount++;
                                        publicAddress.AddLog($"线程：{ThreadNum} PLC站：{PLCstationNum} 设备：{PLCname} 第【{retryCount}】次参数写入未反馈（Content=3）");
                                        if (retryCount >= maxRetries)
                                        {
                                            publicAddress.AddLog($"线程：{ThreadNum} PLC站：{PLCstationNum} 设备：{PLCname} 参数写入 {maxRetries} 次未反馈，放弃");
                                            parameternum = 0;
                                        }
                                        break;

                                    // ★ 新增：兜底，避免未知 Content 导致 while 空转死循环
                                    default:
                                        retryCount++;
                                        publicAddress.AddLog($"线程：{ThreadNum} PLC站：{PLCstationNum} 设备：{PLCname} 参数写入未知返回 Content={send.Content}, Msg={send.Message}");
                                        if (retryCount >= maxRetries)
                                        {
                                            publicAddress.AddLog($"线程：{ThreadNum} PLC站：{PLCstationNum} 设备：{PLCname} 参数写入 {maxRetries} 次均失败（未知错误），放弃");
                                            parameternum = 0;
                                        }
                                        break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // 单次异常：记录后终止重试
                            publicAddress.AddLog($"线程：{ThreadNum} PLC站：{PLCstationNum} 设备：{PLCname} 参数写入处理:{ex.Message}");
                            parameternum = 0;
                            break;
                        }
                        finally
                        {
                            if (tempConnect.IsSuccess) tempPlc.ConnectClose();
                        }

                        // 重试间隔
                        if (parameternum == 1 && retryCount < maxRetries) await Task.Delay(500);
                    }

                    // 超过最大重试次数仍未成功
                    if (retryCount >= maxRetries && parameternum != 0)
                        publicAddress.AddLog($"线程：{ThreadNum} PLC站：{PLCstationNum} 设备：{PLCname} 参数写入失败次数超过 {maxRetries} 次，放弃写入！");
                });
            }
            catch (Exception ex)
            {
                publicAddress.AddLog($"参数写入定义变量异常:{ex.Message}");
            }
            finally
            {
                _paramWriteLock.Release();   // ★ 新增：无论成功失败都释放锁
            }
        }

        /// <summary>
        /// 首检状态改变，写入检验计划（最多重试 3 次）。
        /// 使用独立新建的连接。
        /// </summary>
        private async Task CheckoutAsync(int sate)
        {
            try
            {
                
                await Task.Run(async () =>
                {
                    // ★ 加在这里：无工序配置直接返回，不建临时连接
                    if (IntOprNoXML == null || IntOprNoXML.Rows.Count == 0)
                    {
                        publicAddress.AddLog($"线程：{ThreadNum} PLC站：{PLCstationNum} 无工序配置，跳过首检状态写入");
                        return;
                    }
                    const int maxRetries = 3;
                    int retryCount = 0;

                    // 重试循环
                    while (retryCount < maxRetries)
                    {
                        Simatic_NET tempPlc = null;
                        OperateResult tempConnect = new OperateResult();
                        bool needRetry = false;

                        try
                        {
                            if (tempConnect.IsSuccess && tempPlc != null) tempPlc.ConnectClose();

                            tempPlc = new Simatic_NET();
                            tempConnect = PLC_connect(tempPlc, tempConnect);

                            if (!tempConnect.IsSuccess)
                            {
                                // 连接失败：重试
                                publicAddress.AddLog($"线程：{ThreadNum} PLC站：{PLCstationNum} 设备：{PLCname} 首检状态改变:连接PLC失败 {tempConnect.Message}");
                                retryCount++;
                                if (retryCount < maxRetries) await Task.Delay(500);
                                continue;
                            }

                            SQLToolsYY sqlToolsYY = new SQLToolsYY();
                            Hint hint = new Hint();
                            DataTable dt = new DataTable();
                            string checkoutResultAddress = "";
                            string checkoutAddress = "";
                            string checkoutQty = "";

                            for (int i = 0; i < IntOprNoXML.Rows.Count; i++)
                            {
                                // 取首检地址（09/10/11）
                                DataTable addressDt = DsTable.Tables[0];
                                DataRow[] addressRow = addressDt.Select("(category='09' or category='10' or category='11') and workNumber='" + IntOprNoXML.Rows[i]["workNumber"].ToString() + "' and  PLCstationNum ='" + IntOprNoXML.Rows[i]["PLCstationNum"].ToString() + "'");

                                foreach (DataRow row in addressRow)
                                {
                                    switch (row["category"].ToString())
                                    {
                                        case "09": checkoutAddress = row["address"].ToString(); break;
                                        case "10": checkoutQty = row["address"].ToString(); break;
                                        case "11": checkoutResultAddress = row["address"].ToString(); break;
                                    }
                                }

                                hint = sqlToolsYY.GetDataTable("", "SELECT * FROM MesV_CheckoutXMLNEW where intOprNo='" + IntOprNoXML.Rows[i]["intOprNo"].ToString() + "'", true, out dt);

                                if (hint.Valid && dt.Rows.Count > 0)
                                {
                                    DataRow cfgRow = dt.Rows[0];
                                    bool[] checkoutSet = _convertType.IntToBoolArray(Convert.ToInt32(cfgRow["chechkoutSet"]));

                                    switch (Convert.ToInt32(cfgRow["chechkoutType"]))
                                    {
                                        case 1:
                                        case 3:
                                            // 首检：对应状态位为真才触发
                                            if (checkoutSet[sate])
                                            {
                                                // 写触发 + 允许数量
                                                await tempPlc.WriteInt32Async(checkoutAddress, 1).ConfigureAwait(false);
                                                await tempPlc.WriteInt32Async(checkoutQty, Convert.ToInt32(cfgRow["allowqty"])).ConfigureAwait(false);

                                                string SQLstr = @"exec fynP_saveCheckoutPlan '" + IntOprNoXML.Rows[i]["intOprNo"].ToString() + "',1";
                                                hint = sqlToolsYY.GetDataTable("", SQLstr, true, out dt);

                                                if (hint.Valid)
                                                    await tempPlc.WriteInt32Async(checkoutResultAddress, 0).ConfigureAwait(false);
                                                else
                                                {
                                                    // 执行 SP 失败：记录并需要重试
                                                    publicAddress.AddLog($"{hint.Msg}:首巡检执行错误");
                                                    needRetry = true;
                                                }
                                            }
                                            break;
                                        default:
                                            // 非首检类型清零
                                            await tempPlc.WriteInt32Async(checkoutAddress, 0).ConfigureAwait(false);
                                            await tempPlc.WriteInt32Async(checkoutQty, 0).ConfigureAwait(false);
                                            await tempPlc.WriteInt32Async(checkoutResultAddress, 0).ConfigureAwait(false);
                                            break;
                                    }
                                }
                                else
                                {
                                    // 无首检配置：清零
                                    await tempPlc.WriteInt32Async(checkoutAddress, 0).ConfigureAwait(false);
                                    await tempPlc.WriteInt32Async(checkoutQty, 0).ConfigureAwait(false);
                                    await tempPlc.WriteInt32Async(checkoutResultAddress, 0).ConfigureAwait(false);
                                }
                            }

                            if (!needRetry)
                            {
                                publicAddress.AddLog($"线程：{ThreadNum} PLC站：{PLCstationNum} 设备：{PLCname} 首检状态改变写入成功！");
                                break;
                            }
                            else
                            {
                                retryCount++;
                                if (retryCount < maxRetries) await Task.Delay(500);
                                continue;
                            }
                        }
                        catch (Exception ex)
                        {
                            publicAddress.AddLog($"PLC站：{PLCstationNum} 设备：{PLCname} 首检状态改变异常:{ex.Message}");
                            break;
                        }
                        finally
                        {
                            // 关闭临时连接
                            if (tempConnect.IsSuccess && tempPlc != null) tempPlc.ConnectClose();
                        }
                    }

                    if (retryCount >= maxRetries)
                        publicAddress.AddLog($"线程：{ThreadNum} PLC站：{PLCstationNum} 设备：{PLCname} 首检状态改变写入失败，已重试 {maxRetries} 次，放弃！");
                });
            }
            catch (Exception ex)
            {
                publicAddress.AddLog($"首检状态改变定义变量异常:{ex.Message}");
            }
        }

        /// <summary>
        /// 写入计划数据到 PLC（异步版）。
        ///
        /// 【写入内容】
        ///   1. 计划数量（category=13）
        ///   2. 工艺路线（writeProcessRouteAsync）
        ///   3. 条码信息（writeBarCodeAsync）
        ///   4. 零件 ID / 物料编码（writePartIDAsync）
        /// </summary>
        private async Task WritePlanAsync(Simatic_NET tempPlc)
        {
            try
            {
                if (IntOprNoXML == null || IntOprNoXML.Rows.Count == 0) return;
                SQLToolsYY sqltoolsyy = new SQLToolsYY();
                DataTable dt1 = new DataTable();
                Hint hint = new Hint();

                // 查激活计划（状态=1）
                hint = sqltoolsyy.GetDataTable("", "select top 1 * from fynV_choicePlan where 状态=1", true, out dt1);

                if (hint.Valid == true && dt1.Rows.Count > 0)
                {
                    // 1) 计划数量
                    int qty = Convert.ToInt32(dt1.Rows[0]["数量"]);
                    string planAddress = GetAddressByCategory("13");
                    if (!string.IsNullOrEmpty(planAddress))
                        await tempPlc.WriteInt32Async(planAddress.ToUpper(), qty).ConfigureAwait(false);

                    // 2) 工艺路线
                    string cinvcode = dt1.Rows[0]["物料编码"].ToString();
                    await _plcReadWrite.WriteProcessRouteAsync(tempPlc, IntOprNoXML, AddressTable, cinvcode).ConfigureAwait(false);

                    // 3) 条码信息（ignoreStartFilter=true，处理所有工序）
                    await _plcReadWrite.WriteBarCodeAsync(tempPlc, IntOprNoXML, AddressTable, true).ConfigureAwait(false);

                    // 4) 零件 ID / 物料编码
                    await _plcReadWrite.WritePartIDAsync(tempPlc, IntOprNoXML, AddressTable).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                publicAddress.AddLog($"PLC站：{PLCstationNum} 设备：{PLCname} 计划写入:{ex.Message}");
            }
        }

        /// <summary>从数据库加载当前站点的工序 XML 数据到 IntOprNoXML。</summary>
        private DataTable GetRoutingToIntOprNoXML(int PLCstationNum)
        {
            // 按 PLCstationNum 过滤
            string sql = @" select * from dbo.fynV_intOprNoXML where PLCstationNum = " + PLCstationNum.ToString();

            SQLToolsYY sqlToolsYY = new SQLToolsYY();
            DataTable dt = new DataTable();
            Hint hint = sqlToolsYY.GetDataTable("", sql, true, out dt);

            // 查询成功且有数据才返回
            if (hint.Valid && dt.Rows.Count > 0) return dt;
            return null;
        }

        /// <summary>连接西门子 PLC（S7-1200）。使用 publicAddress.plcConnectLocker 串行化连接操作。</summary>
        private OperateResult PLC_connect(Simatic_NET plc_net_5, OperateResult connect_5)
        {
            // 锁的粒度只到 initializeConnect（真正确立 TCP 连接的部分）
            lock (publicAddress.plcConnectLocker)
            {
                connect_5 = plc_net_5.initializeConnect("S7-1200", GetIPFromStation(), 0, 1);
            }
            return connect_5;
        }

        // ==================== 辅助方法（地址/IP获取） ====================

        /// <summary>从 AddressTable 中获取指定 category 的第一个地址（工位号固定为1）。</summary>
        private string GetAddressByCategory(string category)
        {
            DataRow[] rows = AddressTable.Select($"category='{category}' and workNumber='1' and PLCstationNum='{PLCstationNum}'");
            return rows.Length > 0 ? rows[0]["address"].ToString() : "";
        }

        /// <summary>获取 CRC 校验地址（category=04）。</summary>
        private string GetCRCAddress()
        {
            DataRow[] rows = AddressTable.Select($"category='04' and PLCstationNum='{PLCstationNum}'");
            return rows.Length > 0 ? rows[0]["address"].ToString() : "";
        }

        /// <summary>获取信号地址（category=05）。</summary>
        private string GetSignalAddress()
        {
            DataRow[] rows = AddressTable.Select($"category='05' and PLCstationNum='{PLCstationNum}'");
            return rows.Length > 0 ? rows[0]["address"].ToString() : "";
        }

        /// <summary>从数据库获取当前站点的 PLC IP 地址。</summary>
        private string GetIPFromStation()
        {
            string sql = $"select top 1 IPAddress from fynT_PLCaddress where PLCstationNum={PLCstationNum}";
            SQLToolsYY sqlTools = new SQLToolsYY();
            DataTable dt = new DataTable();
            Hint hint = sqlTools.GetDataTable("", sql, true, out dt);

            return (hint.Valid && dt.Rows.Count > 0) ? dt.Rows[0]["IPAddress"].ToString() : "0.0.0.0";
        }

        /// <summary>从数据库获取当前站点的 PLC 端口号。</summary>
        private int GetPortFromStation()
        {
            string sql = $"select top 1 Prot from fynT_PLCaddress where PLCstationNum={PLCstationNum}";
            SQLToolsYY sqlTools = new SQLToolsYY();
            DataTable dt = new DataTable();
            Hint hint = sqlTools.GetDataTable("", sql, true, out dt);

            return (hint.Valid && dt.Rows.Count > 0) ? Convert.ToInt32(dt.Rows[0]["Prot"]) : 8501;
        }
    }

    /// <summary>
    /// 物理 PLC 连接服务（每个 IP:Port 一个实例）。
    ///
    /// ============================================================
    /// 【本次改动汇总】
    /// ============================================================
    /// 1. 心跳独立线程（HeartbeatLoopSync / EnsureHeartbeatConnectionSync）
    /// 2. 全文 publicAddress.errtext += → AddLog
    /// 3. Dispose 用 Thread.Join 而非 Task.Wait
    /// 4. BusinessLoop1/2/3 调用改为 await ExecuteXxxAsync（异步硬超时）
    /// 5. EnsureBusiness1/2/3ConnectionAsync 增加 IsConnectionBrokenByTimeout 检测
    /// 6. 4 套离线标志独立管理（心跳/业务1/业务2/业务3）
    /// 7. 探针地址：每次 EnsureXxxConnection 时读一次，验证连接是否真正活着
    /// ============================================================
    /// </summary>
    public class PLCService : IDisposable
    {
        // ==================== 基础字段 ====================

        /// <summary>当前 PLCService 下所有站的描述信息（用于日志）</summary>
        private string _stationInfo;

        /// <summary>PLC IP 地址</summary>
        private readonly string _ip;

        /// <summary>PLC 端口</summary>
        private readonly int _port;

        /// <summary>心跳监控器（由 HeartbeatManager 按 IP:Port 单例提供）</summary>
        private readonly HeartbeatMonitor _heartbeatMonitor;

        // ==================== 连接1：心跳（独立） ====================

        /// <summary>心跳专用 PLC 连接对象（被 _heartbeatSem 保护）</summary>
        private Simatic_NET _heartbeatPlcNet;

        /// <summary>心跳专用信号量（初始 1、最大 1，等价于互斥锁）</summary>
        private readonly SemaphoreSlim _heartbeatSem = new SemaphoreSlim(1, 1);

        /// <summary>心跳连接当前是否有效</summary>
        private volatile bool _heartbeatConnected = false;

        /// <summary>心跳是否曾成功连接过（用于区分"首次/重连"日志）</summary>
        private volatile bool _heartbeatEverConnected = false;

        /// <summary>心跳连续重连失败次数（用于退避计算）</summary>
        private int _heartbeatReconnectFailCount = 0;

        /// <summary>心跳上次重连尝试时间（用于判断是否满足退避间隔）</summary>
        private DateTime _heartbeatLastReconnectAttempt = DateTime.MinValue;

        // ==================== 连接2：业务1（独立） ====================

        /// <summary>业务1专用 PLC 连接对象</summary>
        private Simatic_NET _business1PlcNet;

        /// <summary>业务1专用信号量</summary>
        private readonly SemaphoreSlim _business1Sem = new SemaphoreSlim(1, 1);

        /// <summary>业务1连接当前是否有效</summary>
        private volatile bool _business1Connected = false;

        /// <summary>业务1是否曾成功连接过</summary>
        private volatile bool _business1EverConnected = false;

        /// <summary>业务1连续重连失败次数</summary>
        private int _business1ReconnectFailCount = 0;

        /// <summary>业务1上次重连尝试时间</summary>
        private DateTime _business1LastReconnectAttempt = DateTime.MinValue;

        // ==================== 连接3：业务2（独立） ====================

        /// <summary>业务2专用 PLC 连接对象</summary>
        private Simatic_NET _business2PlcNet;

        /// <summary>业务2专用信号量</summary>
        private readonly SemaphoreSlim _business2Sem = new SemaphoreSlim(1, 1);

        /// <summary>业务2连接当前是否有效</summary>
        private volatile bool _business2Connected = false;

        /// <summary>业务2是否曾成功连接过</summary>
        private volatile bool _business2EverConnected = false;

        /// <summary>业务2连续重连失败次数</summary>
        private int _business2ReconnectFailCount = 0;

        /// <summary>业务2上次重连尝试时间</summary>
        private DateTime _business2LastReconnectAttempt = DateTime.MinValue;

        // ==================== 连接4：业务3（独立） ====================

        /// <summary>业务3专用 PLC 连接对象</summary>
        private Simatic_NET _business3PlcNet;

        /// <summary>业务3专用信号量</summary>
        private readonly SemaphoreSlim _business3Sem = new SemaphoreSlim(1, 1);

        /// <summary>业务3连接当前是否有效</summary>
        private volatile bool _business3Connected = false;

        /// <summary>业务3是否曾成功连接过</summary>
        private volatile bool _business3EverConnected = false;

        /// <summary>业务3连续重连失败次数</summary>
        private int _business3ReconnectFailCount = 0;

        /// <summary>业务3上次重连尝试时间</summary>
        private DateTime _business3LastReconnectAttempt = DateTime.MinValue;

        // ==================== 探针地址 ====================

        /// <summary>
        /// 探针地址（取第一个注册站的心跳读地址 LowerAddress）。
        /// 每次 EnsureXxxConnection 时读一次，验证连接是否真正活着。
        /// </summary>
        private string _testAddress = null;

        // ==================== 退避时间表 ====================

        /// <summary>
        /// 退避时间表（秒）：失败 1 次等 1s，2 次等 2s，3 次等 4s，...，上限 30s。
        /// </summary>
        private static readonly int[] BackoffSeconds = new int[] { 1, 2, 4, 8, 16, 30 };

        // ==================== 后台线程/任务与取消 ====================

        /// <summary>心跳专用线程（脱离线程池）</summary>
        private Thread _threadHeartbeat;

        /// <summary>业务1后台循环任务</summary>
        private Task _taskBusiness1;

        /// <summary>业务2后台循环任务</summary>
        private Task _taskBusiness2;

        /// <summary>业务3后台循环任务</summary>
        private Task _taskBusiness3;

        /// <summary>用于取消所有后台任务的令牌源（Dispose 时 Cancel）</summary>
        private CancellationTokenSource _cts;

        /// <summary>是否已释放（防重复 Dispose）</summary>
        private bool _disposed;

        // ==================== 注册的逻辑站 ====================

        /// <summary>已注册的逻辑站。键=ThreadNum，值=StationContext</summary>
        private readonly ConcurrentDictionary<int, StationContext> _stations =
            new ConcurrentDictionary<int, StationContext>();

        // ==================== 离线标志（4 套，每套对应一条连接） ====================

        // 心跳连接
        private bool _hasLoggedOfflineHeartbeat = false;                 // 是否已记录过离线日志（防刷屏）
        private bool _isOfflineRecordedHeartbeat = false;                // 是否已开始记录离线
        private DateTime _offlineStartTimeHeartbeat = DateTime.Now;      // 离线起始时间

        // 业务1连接
        private bool _hasLoggedOffline1 = false;
        private bool _isOfflineRecorded1 = false;
        private DateTime _offlineStartTime1 = DateTime.Now;

        // 业务2连接
        private bool _hasLoggedOffline2 = false;
        private bool _isOfflineRecorded2 = false;
        private DateTime _offlineStartTime2 = DateTime.Now;

        // 业务3连接
        private bool _hasLoggedOffline3 = false;
        private bool _isOfflineRecorded3 = false;
        private DateTime _offlineStartTime3 = DateTime.Now;

        /// <summary>离线标志的同步锁</summary>
        private readonly object _offlineLock = new object();

        // ==================== 心跳在线状态（事件驱动） ====================

        /// <summary>心跳当前是否在线（由 OnHeartbeatStatusChanged 驱动）</summary>
        private volatile bool _isHeartbeatOnline = false;

        /// <summary>是否已订阅状态变化事件</summary>
        private bool _isSubscribedToHeartbeat = false;

        /// <summary>是否已订阅心跳接收事件</summary>
        private bool _isSubscribedToHeartbeatReceived = false;

        /// <summary>订阅同步锁</summary>
        private readonly object _subscriptionLock = new object();

        // ==================== 构造函数 ====================

        /// <summary>
        /// 构造函数：初始化 IP、端口、心跳监控器、四条连接、后台线程/任务。
        /// </summary>
        public PLCService(string ip, int port)
        {
            _ip = ip;
            _port = port;

            // 按 IP:Port 拿单例心跳监控器
            _heartbeatMonitor = HeartbeatManager.GetMonitor(ip, port);

            // 订阅心跳事件（状态变化 + 接收）
            SubscribeToHeartbeatEvents();
            SubscribeToHeartbeatReceivedEvents();

            // 4 条独立连接
            _heartbeatPlcNet = new Simatic_NET();
            _business1PlcNet = new Simatic_NET();
            _business2PlcNet = new Simatic_NET();
            _business3PlcNet = new Simatic_NET();

            _cts = new CancellationTokenSource();

            // 启动后台线程
            StartBackgroundTasks();

            // 从数据库读一次当前激活计划的 modID
            UpdateModidFromActivePlan();
        }

        // ==================== 心跳事件订阅与处理 ====================

        /// <summary>订阅心跳状态变化事件（Online/Offline/Unknown）。</summary>
        private void SubscribeToHeartbeatEvents()
        {
            lock (_subscriptionLock)
            {
                // 只订阅一次
                if (!_isSubscribedToHeartbeat && _heartbeatMonitor != null)
                {
                    _heartbeatMonitor.StatusChanged += OnHeartbeatStatusChanged;
                    _isSubscribedToHeartbeat = true;

                    // 同步一次当前状态
                    _isHeartbeatOnline = _heartbeatMonitor.IsOnline;

                    publicAddress.AddLog($"[PLCService] {_ip}:{_port} 已订阅心跳状态变化事件");
                }
            }
        }

        /// <summary>订阅心跳接收事件（每次成功读取到心跳包时触发）。</summary>
        private void SubscribeToHeartbeatReceivedEvents()
        {
            lock (_subscriptionLock)
            {
                if (!_isSubscribedToHeartbeatReceived && _heartbeatMonitor != null)
                {
                    _heartbeatMonitor.HeartbeatReceived += OnHeartbeatReceived;
                    _isSubscribedToHeartbeatReceived = true;
                }
            }
        }

        /// <summary>取消订阅心跳相关事件（Dispose 时调用）。</summary>
        private void UnsubscribeFromHeartbeatEvents()
        {
            lock (_subscriptionLock)
            {
                if (_isSubscribedToHeartbeat && _heartbeatMonitor != null)
                {
                    _heartbeatMonitor.StatusChanged -= OnHeartbeatStatusChanged;
                    _isSubscribedToHeartbeat = false;
                }

                if (_isSubscribedToHeartbeatReceived && _heartbeatMonitor != null)
                {
                    _heartbeatMonitor.HeartbeatReceived -= OnHeartbeatReceived;
                    _isSubscribedToHeartbeatReceived = false;
                }
            }
        }

        /// <summary>
        /// 心跳状态变化事件处理。
        /// 恢复 Online → 重置 4 套离线计时器；切 Offline → 设置标志位。
        /// </summary>
        private void OnHeartbeatStatusChanged(object sender, HeartbeatStatusChangedEventArgs e)
        {
            try
            {
                if (e.NewStatus == HeartbeatStatus.Online)
                {
                    _isHeartbeatOnline = true;

                    // 恢复在线 → 重置 4 套离线计时器
                    lock (_offlineLock)
                    {
                        _offlineStartTimeHeartbeat = DateTime.Now;
                        _isOfflineRecordedHeartbeat = false;
                        _hasLoggedOfflineHeartbeat = false;

                        _offlineStartTime1 = DateTime.Now;
                        _isOfflineRecorded1 = false;
                        _hasLoggedOffline1 = false;

                        _offlineStartTime2 = DateTime.Now;
                        _isOfflineRecorded2 = false;
                        _hasLoggedOffline2 = false;

                        _offlineStartTime3 = DateTime.Now;
                        _isOfflineRecorded3 = false;
                        _hasLoggedOffline3 = false;
                    }

                    Console.WriteLine($"[心跳事件] {_ip}:{_port} 站号【{_stationInfo}】心跳已恢复在线");
                }
                else if (e.NewStatus == HeartbeatStatus.Offline)
                {
                    _isHeartbeatOnline = false;
                    Console.WriteLine($"[心跳事件] {_ip}:{_port} 站号【{_stationInfo}】心跳变为离线");
                }
                else
                {
                    // Unknown 等其他状态也视为离线
                    _isHeartbeatOnline = false;
                }

                // 更新所有站的界面状态
                UpdateAllStationHeartbeatStatus(0, false);
            }
            catch (Exception ex)
            {
                publicAddress.AddLog($"[心跳事件] {_ip}:{_port} 处理异常:{ex.Message}");
            }
        }

        /// <summary>每次成功收到心跳时重置 4 套离线计时器。</summary>
        private void OnHeartbeatReceived()
        {
            try
            {
                lock (_offlineLock)
                {
                    // 只要任一标志置位就重置全部（说明心跳已恢复）
                    if (_isOfflineRecordedHeartbeat || _isOfflineRecorded1
                        || _isOfflineRecorded2 || _isOfflineRecorded3)
                    {
                        _offlineStartTimeHeartbeat = DateTime.Now; _isOfflineRecordedHeartbeat = false; _hasLoggedOfflineHeartbeat = false;
                        _offlineStartTime1 = DateTime.Now; _isOfflineRecorded1 = false; _hasLoggedOffline1 = false;
                        _offlineStartTime2 = DateTime.Now; _isOfflineRecorded2 = false; _hasLoggedOffline2 = false;
                        _offlineStartTime3 = DateTime.Now; _isOfflineRecorded3 = false; _hasLoggedOffline3 = false;
                    }
                }
            }
            catch (Exception ex)
            {
                publicAddress.AddLog($"[心跳接收事件] {_ip}:{_port} 处理异常:{ex.Message}");
            }
        }

        // ==================== 站注册与注销 ====================

        /// <summary>
        /// 注册一个逻辑站。第一个注册的站，取其 LowerAddress 作为探针地址。
        /// </summary>
        public void RegisterStation(int threadNum, StationContext context)
        {
            _stations.TryAdd(threadNum, context);

            // 第一次注册时确定探针地址
            if (_testAddress == null && !string.IsNullOrEmpty(context.LowerAddress))
                _testAddress = context.LowerAddress;

            _stationInfo = GetStationInfoForLog();

            Console.WriteLine($"PLCService [{_ip}:{_port}] 注册站 threadNum={threadNum}, 当前站数={_stations.Count}");
        }

        /// <summary>注销逻辑站。</summary>
        public void UnregisterStation(int threadNum)
        {
            _stations.TryRemove(threadNum, out _);
            _stationInfo = GetStationInfoForLog();
        }

        // ==================== 启动后台线程/任务 ====================

        /// <summary>启动后台循环。</summary>
        private void StartBackgroundTasks()
        {
            // 心跳：专用线程（脱离线程池，不抢业务线程）
            _threadHeartbeat = new Thread(HeartbeatLoopSync)
            {
                IsBackground = true,
                Name = $"Heartbeat-{_ip}:{_port}",
                Priority = ThreadPriority.AboveNormal
            };
            _threadHeartbeat.Start();

            // 业务1/2/3：线程池任务
            _taskBusiness1 = Task.Run(BusinessLoop1_ParameterAndCheck);
            _taskBusiness2 = Task.Run(BusinessLoop2_ResultSave);
            _taskBusiness3 = Task.Run(BusinessLoop3_BarcodeScan);

            publicAddress.AddLog(
                $"[PLCService] {_ip}:{_port} 心跳专用线程已启动 (TID={_threadHeartbeat.ManagedThreadId})");
        }

        /// <summary>从数据库激活计划中读取 Modid 并更新全局变量。</summary>
        private void UpdateModidFromActivePlan()
        {
            try
            {
                SQLToolsYY sqltoolsyy = new SQLToolsYY();

                Hint hintActive = sqltoolsyy.GetDataTable("", "SELECT TOP 1 modID FROM fynV_choicePlan WHERE 状态=1", true, out DataTable dtActive);

                if (hintActive.Valid && dtActive.Rows.Count > 0)
                {
                    object modIdValue = dtActive.Rows[0]["modID"];
                    // 排除 NULL
                    if (modIdValue != DBNull.Value)
                        publicAddress.Modid = Convert.ToInt32(modIdValue);
                }
            }
            catch (Exception ex) { Console.WriteLine($"更新 Modid 失败：{ex.Message}"); }
        }

        /// <summary>计算退避时间（秒）：1s, 2s, 4s, 8s, 16s, 30s 封顶。</summary>
        private int GetBackoffSeconds(int failCount)
        {
            if (failCount <= 0) return 0;
            // 索引封顶到数组末尾
            int index = Math.Min(failCount - 1, BackoffSeconds.Length - 1);
            return BackoffSeconds[index];
        }

        // ==================== 连接1管理（心跳，同步版） ====================

        /// <summary>
        /// 确保心跳连接可用（同步，只在心跳专用线程上调用）。
        ///
        /// 【流程】
        ///   1. 探针验证：读一次 _testAddress，成功则认为连接有效
        ///   2. 双检：抢信号量后再次检查
        ///   3. 退避：上次失败未到下次重试时间就直接返回 false
        ///   4. 重连：关旧连接 → new 新连接 → initializeConnect
        ///   5. 成功：置标志 + 重置 _heartbeatMonitor 计数
        /// </summary>
        private bool EnsureHeartbeatConnectionSync()
        {
            // ========== 第一步：探针验证（快路径） ==========
            if (_heartbeatConnected && _heartbeatPlcNet != null)
            {
                if (!string.IsNullOrEmpty(_testAddress))
                {
                    try
                    {
                        var test = _heartbeatPlcNet.ReadInt32(_testAddress.ToUpper());
                        if (test != null && test.IsSuccess) return true;   // 探针成功，连接有效

                        _heartbeatConnected = false;
                        publicAddress.AddLog($"[心跳] {_ip}:{_port} 连接检测失败，准备重连");
                    }
                    catch { _heartbeatConnected = false; }
                }
                else return true;   // 没有探针地址直接认为可用
            }

            // ========== 第二步：信号量内重连 ==========
            try { _heartbeatSem.Wait(_cts.Token); }
            catch (OperationCanceledException) { return false; }

            try
            {
                // 双检：其他线程可能已经完成重连
                if (_heartbeatConnected && _heartbeatPlcNet != null) return true;

                // 退避时间未到直接返回
                int backoffSec = GetBackoffSeconds(_heartbeatReconnectFailCount);
                if (backoffSec > 0 &&
                    (DateTime.Now - _heartbeatLastReconnectAttempt).TotalSeconds < backoffSec)
                    return false;

                _heartbeatLastReconnectAttempt = DateTime.Now;

                // 先关旧连接，再 new 新的
                try { _heartbeatPlcNet?.ConnectClose(); } catch { }
                _heartbeatPlcNet = new Simatic_NET();

                // 执行连接
                OperateResult result;
                try { result = _heartbeatPlcNet.initializeConnect("S7-1200", _ip, 0, 1); }
                catch (Exception ex) { result = new OperateResult { IsSuccess = false, Message = ex.Message }; }

                if (result != null && result.IsSuccess)
                {
                    _heartbeatConnected = true;
                    _heartbeatReconnectFailCount = 0;

                    // 首次/重连使用不同日志
                    publicAddress.AddLog(_heartbeatEverConnected
                        ? $"[心跳] {_ip}:{_port} 重连成功"
                        : $"[心跳] {_ip}:{_port} 首次连接成功");
                    _heartbeatEverConnected = true;

                    _heartbeatMonitor?.Reset();
                    return true;
                }

                // 连接失败：计入退避
                _heartbeatConnected = false;
                _heartbeatReconnectFailCount++;
                int nextBackoff = GetBackoffSeconds(_heartbeatReconnectFailCount);
                publicAddress.AddLog($"[心跳] {_ip}:{_port} 连接失败: {result?.Message ?? "未知"}，下次重试间隔 {nextBackoff}s");
                return false;
            }
            finally { _heartbeatSem.Release(); }
        }

        // ==================== 连接2管理（业务1，异步） ====================

        /// <summary>
        /// 确保业务1连接可用（异步，非阻塞）。探针验证 + 退避重连。
        /// 【新增】检测 IsConnectionBrokenByTimeout：上一轮硬超时则强制重连。
        /// </summary>
        private async Task<bool> EnsureBusiness1ConnectionAsync(int timeoutMs = 3000)
        {
            if (_business1Connected && _business1PlcNet != null)
            {
                // 【新增】硬超时检测：上次硬超时说明 TcpClient 已被关闭，必须重连
                if (_business1PlcNet.IsConnectionBrokenByTimeout)
                {
                    publicAddress.AddLog($"[业务1] {_ip}:{_port} 上一轮曾发生硬超时，强制关闭连接并重连");
                    try { _business1PlcNet.ConnectClose(); } catch { }
                    _business1Connected = false;
                }
                else if (!string.IsNullOrEmpty(_testAddress))
                {
                    // 探针读一次
                    try
                    {
                        var test = _business1PlcNet.ReadInt32(_testAddress.ToUpper());
                        if (test != null && test.IsSuccess) return true;

                        _business1Connected = false;
                        publicAddress.AddLog($"[业务1] {_ip}:{_port} 连接检测失败，准备重连");
                    }
                    catch { _business1Connected = false; }
                }
                else return true;
            }

            // 抢信号量
            await _business1Sem.WaitAsync().ConfigureAwait(false);
            try
            {
                // 双检
                if (_business1Connected && _business1PlcNet != null) return true;

                // 退避检查
                int backoffSec = GetBackoffSeconds(_business1ReconnectFailCount);
                if (backoffSec > 0 &&
                    (DateTime.Now - _business1LastReconnectAttempt).TotalSeconds < backoffSec)
                    return false;

                _business1LastReconnectAttempt = DateTime.Now;

                // 关旧连接，new 新连接
                try { _business1PlcNet?.ConnectClose(); } catch { }
                _business1PlcNet = new Simatic_NET();

                // 连接放到线程池执行，用 Task.WhenAny 加超时
                var connectTask = Task.Run(() =>
                {
                    try { return _business1PlcNet.initializeConnect("S7-1200", _ip, 0, 1); }
                    catch (Exception ex) { return new OperateResult { IsSuccess = false, Message = ex.Message }; }
                });

                var timeoutTask = Task.Delay(timeoutMs);
                var completed = await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false);

                if (completed == connectTask)
                {
                    var result = await connectTask.ConfigureAwait(false);
                    if (result != null && result.IsSuccess)
                    {
                        _business1Connected = true;
                        _business1ReconnectFailCount = 0;

                        publicAddress.AddLog(_business1EverConnected
                            ? $"[业务1] {_ip}:{_port} 重连成功"
                            : $"[业务1] {_ip}:{_port} 首次连接成功");
                        _business1EverConnected = true;
                        return true;
                    }
                    else
                    {
                        _business1Connected = false;
                        _business1ReconnectFailCount++;
                        int nextBackoff = GetBackoffSeconds(_business1ReconnectFailCount);
                        publicAddress.AddLog($"[业务1] {_ip}:{_port} 连接失败: {result?.Message ?? "未知"}，下次重试间隔 {nextBackoff}s");
                        return false;
                    }
                }
                else
                {
                    // 超时
                    _business1Connected = false;
                    _business1ReconnectFailCount++;
                    int nextBackoff = GetBackoffSeconds(_business1ReconnectFailCount);
                    publicAddress.AddLog($"[业务1] {_ip}:{_port} 连接超时（>{timeoutMs}ms），下次重试间隔 {nextBackoff}s");
                    return false;
                }
            }
            finally { _business1Sem.Release(); }
        }

        // ==================== 连接3管理（业务2，异步） ====================

        /// <summary>
        /// 确保业务2连接可用（异步，非阻塞）。
        /// 【新增】检测 IsConnectionBrokenByTimeout，为 true 时强制重连。
        /// </summary>
        private async Task<bool> EnsureBusiness2ConnectionAsync(int timeoutMs = 3000)
        {
            if (_business2Connected && _business2PlcNet != null)
            {
                // 【关键】硬超时检测
                if (_business2PlcNet.IsConnectionBrokenByTimeout)
                {
                    publicAddress.AddLog($"[业务2] {_ip}:{_port} 上一轮曾发生硬超时，强制关闭连接并重连");
                    try { _business2PlcNet.ConnectClose(); } catch { }
                    _business2Connected = false;
                }
                else if (!string.IsNullOrEmpty(_testAddress))
                {
                    try
                    {
                        var test = _business2PlcNet.ReadInt32(_testAddress.ToUpper());
                        if (test != null && test.IsSuccess) return true;

                        _business2Connected = false;
                        publicAddress.AddLog($"[业务2] {_ip}:{_port} 连接检测失败，准备重连");
                    }
                    catch { _business2Connected = false; }
                }
                else return true;
            }

            await _business2Sem.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_business2Connected && _business2PlcNet != null) return true;

                int backoffSec = GetBackoffSeconds(_business2ReconnectFailCount);
                if (backoffSec > 0 &&
                    (DateTime.Now - _business2LastReconnectAttempt).TotalSeconds < backoffSec)
                    return false;

                _business2LastReconnectAttempt = DateTime.Now;

                try { _business2PlcNet?.ConnectClose(); } catch { }
                _business2PlcNet = new Simatic_NET();

                var connectTask = Task.Run(() =>
                {
                    try { return _business2PlcNet.initializeConnect("S7-1200", _ip, 0, 1); }
                    catch (Exception ex) { return new OperateResult { IsSuccess = false, Message = ex.Message }; }
                });

                var timeoutTask = Task.Delay(timeoutMs);
                var completed = await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false);

                if (completed == connectTask)
                {
                    var result = await connectTask.ConfigureAwait(false);
                    if (result != null && result.IsSuccess)
                    {
                        _business2Connected = true;
                        _business2ReconnectFailCount = 0;

                        publicAddress.AddLog(_business2EverConnected
                            ? $"[业务2] {_ip}:{_port} 重连成功"
                            : $"[业务2] {_ip}:{_port} 首次连接成功");
                        _business2EverConnected = true;
                        return true;
                    }
                    else
                    {
                        _business2Connected = false;
                        _business2ReconnectFailCount++;
                        int nextBackoff = GetBackoffSeconds(_business2ReconnectFailCount);
                        publicAddress.AddLog($"[业务2] {_ip}:{_port} 连接失败: {result?.Message ?? "未知"}，下次重试间隔 {nextBackoff}s");
                        return false;
                    }
                }
                else
                {
                    _business2Connected = false;
                    _business2ReconnectFailCount++;
                    int nextBackoff = GetBackoffSeconds(_business2ReconnectFailCount);
                    publicAddress.AddLog($"[业务2] {_ip}:{_port} 连接超时（>{timeoutMs}ms），下次重试间隔 {nextBackoff}s");
                    return false;
                }
            }
            finally { _business2Sem.Release(); }
        }

        // ==================== 连接4管理（业务3，异步） ====================

        /// <summary>
        /// 确保业务3连接可用（异步，非阻塞）。
        /// 【新增】检测 IsConnectionBrokenByTimeout，为 true 时强制重连。
        /// </summary>
        private async Task<bool> EnsureBusiness3ConnectionAsync(int timeoutMs = 3000)
        {
            if (_business3Connected && _business3PlcNet != null)
            {
                if (_business3PlcNet.IsConnectionBrokenByTimeout)
                {
                    publicAddress.AddLog($"[业务3] {_ip}:{_port} 上一轮曾发生硬超时，强制关闭连接并重连");
                    try { _business3PlcNet.ConnectClose(); } catch { }
                    _business3Connected = false;
                }
                else if (!string.IsNullOrEmpty(_testAddress))
                {
                    try
                    {
                        var test = _business3PlcNet.ReadInt32(_testAddress.ToUpper());
                        if (test != null && test.IsSuccess) return true;

                        _business3Connected = false;
                        publicAddress.AddLog($"[业务3] {_ip}:{_port} 连接检测失败，准备重连");
                    }
                    catch { _business3Connected = false; }
                }
                else return true;
            }

            await _business3Sem.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_business3Connected && _business3PlcNet != null) return true;

                int backoffSec = GetBackoffSeconds(_business3ReconnectFailCount);
                if (backoffSec > 0 &&
                    (DateTime.Now - _business3LastReconnectAttempt).TotalSeconds < backoffSec)
                    return false;

                _business3LastReconnectAttempt = DateTime.Now;

                try { _business3PlcNet?.ConnectClose(); } catch { }
                _business3PlcNet = new Simatic_NET();

                var connectTask = Task.Run(() =>
                {
                    try { return _business3PlcNet.initializeConnect("S7-1200", _ip, 0, 1); }
                    catch (Exception ex) { return new OperateResult { IsSuccess = false, Message = ex.Message }; }
                });

                var timeoutTask = Task.Delay(timeoutMs);
                var completed = await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false);

                if (completed == connectTask)
                {
                    var result = await connectTask.ConfigureAwait(false);
                    if (result != null && result.IsSuccess)
                    {
                        _business3Connected = true;
                        _business3ReconnectFailCount = 0;

                        publicAddress.AddLog(_business3EverConnected
                            ? $"[业务3] {_ip}:{_port} 重连成功"
                            : $"[业务3] {_ip}:{_port} 首次连接成功");
                        _business3EverConnected = true;
                        return true;
                    }
                    else
                    {
                        _business3Connected = false;
                        _business3ReconnectFailCount++;
                        int nextBackoff = GetBackoffSeconds(_business3ReconnectFailCount);
                        publicAddress.AddLog($"[业务3] {_ip}:{_port} 连接失败: {result?.Message ?? "未知"}，下次重试间隔 {nextBackoff}s");
                        return false;
                    }
                }
                else
                {
                    _business3Connected = false;
                    _business3ReconnectFailCount++;
                    int nextBackoff = GetBackoffSeconds(_business3ReconnectFailCount);
                    publicAddress.AddLog($"[业务3] {_ip}:{_port} 连接超时（>{timeoutMs}ms），下次重试间隔 {nextBackoff}s");
                    return false;
                }
            }
            finally { _business3Sem.Release(); }
        }

        // ==================== 心跳循环（连接1，专用线程） ====================

        /// <summary>心跳循环（同步版本，运行在专用 Thread 上，不占线程池）。</summary>
        private void HeartbeatLoopSync()
        {
            var token = _cts.Token;
            int interval = Math.Max(50, HeartbeatConfig.HeartbeatReadIntervalMs);

            // 主循环
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // ========== 离线超时检查 ==========
                    if (!_isHeartbeatOnline)
                    {
                        bool needClose = false;

                        lock (_offlineLock)
                        {
                            // 首次进入离线：记录起始时间
                            if (!_isOfflineRecordedHeartbeat)
                            {
                                _offlineStartTimeHeartbeat = DateTime.Now;
                                _isOfflineRecordedHeartbeat = true;
                            }

                            // 离线时间超过阈值才关闭连接（释放资源）
                            if ((DateTime.Now - _offlineStartTimeHeartbeat).TotalSeconds
                                >= HeartbeatConfig.OfflineCloseBusinessSeconds
                                && !_hasLoggedOfflineHeartbeat)
                            {
                                _hasLoggedOfflineHeartbeat = true;
                                needClose = true;
                            }
                        }

                        if (needClose)
                        {
                            // 抢锁关连接
                            try { _heartbeatSem.Wait(token); }
                            catch (OperationCanceledException) { break; }

                            try
                            {
                                if (_heartbeatConnected || _heartbeatPlcNet != null)
                                {
                                    try { _heartbeatPlcNet?.ConnectClose(); } catch { }
                                    _heartbeatConnected = false;
                                    _heartbeatReconnectFailCount = 0;
                                }
                            }
                            finally { _heartbeatSem.Release(); }

                            publicAddress.AddLog(
                                $"[心跳] {_ip}:{_port} 心跳离线超过{HeartbeatConfig.OfflineCloseBusinessSeconds}秒，主动关闭连接释放资源");
                        }
                    }
                    else
                    {
                        // 在线 → 清离线记录
                        lock (_offlineLock)
                        {
                            if (_isOfflineRecordedHeartbeat)
                            {
                                _isOfflineRecordedHeartbeat = false;
                                _hasLoggedOfflineHeartbeat = false;
                            }
                        }
                    }

                    // 确保连接可用
                    if (!EnsureHeartbeatConnectionSync())
                    {
                        if (token.WaitHandle.WaitOne(500)) break;
                        continue;
                    }

                    // ========== 信号量内心跳读写 ==========
                    try { _heartbeatSem.Wait(token); }
                    catch (OperationCanceledException) { break; }

                    try
                    {
                        try { DoHeartbeatReadWriteInternal(); }
                        catch (Exception ex)
                        {
                            // 读写异常：标记连接失效，下轮重连
                            _heartbeatConnected = false;
                            publicAddress.AddLog(
                                $"PLCService [{_ip}:{_port}] 心跳读写异常:{ex.Message}（标记连接断开，下轮重连）");
                        }
                    }
                    finally { _heartbeatSem.Release(); }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    publicAddress.AddLog($"PLCService [{_ip}:{_port}] 心跳循环异常:{ex.Message}");
                }

                // 按间隔等待；token 被取消立即退出
                if (token.WaitHandle.WaitOne(interval)) break;
            }

            publicAddress.AddLog(
                $"[PLCService] {_ip}:{_port} 心跳专用线程已退出 (TID={Thread.CurrentThread.ManagedThreadId})");
        }

        /// <summary>在心跳连接上执行心跳读写。必须在持有 _heartbeatSem 时调用。</summary>
        private void DoHeartbeatReadWriteInternal()
        {
            // 取第一个注册站作为心跳站
            StationContext firstStation = null;
            foreach (var station in _stations.Values) { firstStation = station; break; }

            if (firstStation == null || string.IsNullOrEmpty(firstStation.LowerAddress))
                return;

            int heartbeatValue = 0;
            bool readSuccess = false;

            try
            {
                // 读下位机心跳值
                var readResult = _heartbeatPlcNet.ReadInt32(firstStation.LowerAddress.ToUpper());

                if (readResult != null && readResult.IsSuccess)
                {
                    heartbeatValue = readResult.Content;
                    readSuccess = true;

                    // 回写 package 计数作为上位机心跳
                    int packageSnapshot = publicAddress.package;
                    var writeResult = _heartbeatPlcNet.WriteInt32(
                        firstStation.UpperAddress.ToUpper(), packageSnapshot);

                    if (writeResult == null || !writeResult.IsSuccess)
                    {
                        _heartbeatConnected = false;
                        publicAddress.AddLog(
                            $"[心跳] {_ip}:{_port} 写心跳失败 upper={firstStation.UpperAddress} value={packageSnapshot} msg={writeResult?.Message}");
                    }
                }
                else
                {
                    readSuccess = false;
                    _heartbeatConnected = false;
                }
            }
            catch (Exception ex)
            {
                publicAddress.AddLog($"PLCService [{_ip}:{_port}] 心跳读写异常:{ex.Message}");
                readSuccess = false;
                _heartbeatConnected = false;
            }

            // 上报监控器
            if (readSuccess) _heartbeatMonitor?.ReportHeartbeatSuccess();
            else _heartbeatMonitor?.ReportHeartbeatFailure();

            // 更新所有站的界面心跳值
            UpdateAllStationHeartbeatStatus(heartbeatValue, readSuccess);
        }

        /// <summary>更新所有注册站的心跳值并刷新界面状态。</summary>
        private void UpdateAllStationHeartbeatStatus(int heartbeatValue, bool success)
        {
            foreach (var station in _stations.Values)
            {
                // 读成功才更新心跳值
                if (success) station.LastHeartbeatValue = heartbeatValue;
                station.UpdateTrafficStatus(_heartbeatMonitor, _ip, _port);
            }
        }

        // ==================== 业务1循环（连接2） ====================

        /// <summary>
        /// 业务循环1：每 200ms 执行一次，处理参数/点检/刷卡/巡检。
        /// 【本次改动】改为 await station.ExecuteParameterAndCheckAsync(...)，
        /// 内部单次读写最多卡 2 秒。
        /// </summary>
        private async Task BusinessLoop1_ParameterAndCheck()
        {
            var token = _cts.Token;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    // 心跳离线：暂停业务，超时后关闭连接
                    if (!_isHeartbeatOnline)
                    {
                        bool needClose = false;

                        lock (_offlineLock)
                        {
                            if (!_isOfflineRecorded1)
                            {
                                _offlineStartTime1 = DateTime.Now;
                                _isOfflineRecorded1 = true;
                            }

                            if ((DateTime.Now - _offlineStartTime1).TotalSeconds >= HeartbeatConfig.OfflineCloseBusinessSeconds
                                && !_hasLoggedOffline1)
                            {
                                _hasLoggedOffline1 = true;
                                needClose = true;
                            }
                        }

                        if (needClose)
                        {
                            await _business1Sem.WaitAsync(token).ConfigureAwait(false);
                            try
                            {
                                if (_business1Connected || _business1PlcNet != null)
                                {
                                    try { _business1PlcNet?.ConnectClose(); } catch { }
                                    _business1Connected = false;
                                    _business1ReconnectFailCount = 0;
                                }
                            }
                            finally { _business1Sem.Release(); }

                            publicAddress.AddLog(
                                $"[业务1] {_ip}:{_port} 心跳离线超过{HeartbeatConfig.OfflineCloseBusinessSeconds}秒，主动关闭连接释放资源");
                        }

                        // 离线期间短睡
                        await Task.Delay(500, token).ConfigureAwait(false);
                        continue;
                    }

                    // 在线：清离线记录
                    lock (_offlineLock)
                    {
                        if (_isOfflineRecorded1)
                        {
                            _isOfflineRecorded1 = false;
                            _hasLoggedOffline1 = false;
                        }
                    }

                    // 确保连接
                    if (!await EnsureBusiness1ConnectionAsync(3000).ConfigureAwait(false))
                    {
                        await Task.Delay(500, token).ConfigureAwait(false);
                        continue;
                    }

                    // 抢锁执行业务
                    await _business1Sem.WaitAsync(token).ConfigureAwait(false);
                    try
                    {
                        try
                        {
                            // 遍历所有站，依次执行
                            foreach (var station in _stations.Values)
                                await station.ExecuteParameterAndCheckAsync(_business1PlcNet).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            // 业务异常：标记连接断开
                            _business1Connected = false;
                            publicAddress.AddLog($"业务1执行异常:{ex.Message}");
                        }
                    }
                    finally { _business1Sem.Release(); }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    publicAddress.AddLog($"业务1循环异常:{ex.Message}");
                }

                // 200ms 周期
                try { await Task.Delay(200, token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }

        // ==================== 业务2循环（连接3） ====================

        /// <summary>
        /// 业务循环2：每 200ms 执行一次，处理检测结果保存。
        /// 【本次改动】改为 await station.QuickCheckResultSaveAsync / ExecuteResultSaveAsync，
        /// 内部单次读写最多卡 2 秒（由 Simatic_NET 硬超时保证）。
        /// </summary>
        private async Task BusinessLoop2_ResultSave()
        {
            var token = _cts.Token;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    // 心跳离线处理
                    if (!_isHeartbeatOnline)
                    {
                        bool needClose = false;

                        lock (_offlineLock)
                        {
                            if (!_isOfflineRecorded2)
                            {
                                _offlineStartTime2 = DateTime.Now;
                                _isOfflineRecorded2 = true;
                            }

                            if ((DateTime.Now - _offlineStartTime2).TotalSeconds >= HeartbeatConfig.OfflineCloseBusinessSeconds
                                && !_hasLoggedOffline2)
                            {
                                _hasLoggedOffline2 = true;
                                needClose = true;
                            }
                        }

                        if (needClose)
                        {
                            await _business2Sem.WaitAsync(token).ConfigureAwait(false);
                            try
                            {
                                if (_business2Connected || _business2PlcNet != null)
                                {
                                    try { _business2PlcNet?.ConnectClose(); } catch { }
                                    _business2Connected = false;
                                    _business2ReconnectFailCount = 0;
                                }
                            }
                            finally { _business2Sem.Release(); }

                            publicAddress.AddLog(
                                $"[业务2] {_ip}:{_port} 站号【{_stationInfo}】心跳离线超过{HeartbeatConfig.OfflineCloseBusinessSeconds}秒，主动关闭连接释放资源");
                        }

                        await Task.Delay(500, token).ConfigureAwait(false);
                        continue;
                    }

                    lock (_offlineLock)
                    {
                        if (_isOfflineRecorded2)
                        {
                            _isOfflineRecorded2 = false;
                            _hasLoggedOffline2 = false;
                        }
                    }

                    // 连接不可用：按退避等级睡眠
                    if (!await EnsureBusiness2ConnectionAsync(3000).ConfigureAwait(false))
                    {
                        int backoffSec = GetBackoffSeconds(_business2ReconnectFailCount);
                        int sleepMs = Math.Max(500, backoffSec * 1000);
                        publicAddress.AddLog($"[业务2] {_ip}:{_port} 连接不可用，退避 {sleepMs}ms 后再试");
                        await Task.Delay(sleepMs, token).ConfigureAwait(false);
                        continue;
                    }

                    // 遍历站点，单次抢锁（预检+执行合并）
                    foreach (var station in _stations.Values)
                    {
                        var __tW = DateTime.Now;
                        await _business2Sem.WaitAsync(token).ConfigureAwait(false);
                        var __wMs = (DateTime.Now - __tW).TotalMilliseconds;

                        // 等锁超过 500ms 记录告警
                        if (__wMs > 500)
                        {
                            publicAddress.AddLog($"[业务2-等锁] {_ip}:{_port} 等锁={__wMs:F0}ms");
                        }

                        try
                        {
                            try
                            {
                                // ★ 改为异步预检
                                if (!await station.QuickCheckResultSaveAsync(_business2PlcNet).ConfigureAwait(false))
                                    continue;   // 无请求，跳过

                                var __tExec = DateTime.Now;
                                try
                                {
                                    // 【关键改动】改为异步版
                                    await station.ExecuteResultSaveAsync(_business2PlcNet).ConfigureAwait(false);
                                }
                                catch (Exception ex)
                                {
                                    _business2Connected = false;
                                    publicAddress.AddLog($"业务2执行异常: {ex.Message}");
                                }
                                finally
                                {
                                    // 单工位执行超过 1s 记录
                                    var __eMs = (DateTime.Now - __tExec).TotalMilliseconds;
                                    if (__eMs > 1000)
                                    {
                                        publicAddress.AddLog(
                                            $"[业务2-执行] {_ip}:{_port} 站号={station.PLCstationNum} 耗时={__eMs:F0}ms");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _business2Connected = false;
                                publicAddress.AddLog($"业务2预检异常: {ex.Message}");
                            }
                        }
                        finally { _business2Sem.Release(); }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    publicAddress.AddLog($"业务2循环异常:{ex.Message}");
                }

                try { await Task.Delay(200, token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }

        // ==================== 业务3循环（连接4） ====================

        /// <summary>
        /// 业务循环3：每 200ms 执行一次，处理条码扫描。
        /// 【本次改动】改为 await station.QuickCheckBarcodeScanAsync / ExecuteBarcodeScanAsync，
        /// 内部单次读写最多卡 2 秒。
        /// </summary>
        private async Task BusinessLoop3_BarcodeScan()
        {
            var token = _cts.Token;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    // 心跳离线处理
                    if (!_isHeartbeatOnline)
                    {
                        bool needClose = false;

                        lock (_offlineLock)
                        {
                            if (!_isOfflineRecorded3)
                            {
                                _offlineStartTime3 = DateTime.Now;
                                _isOfflineRecorded3 = true;
                            }

                            if ((DateTime.Now - _offlineStartTime3).TotalSeconds >= HeartbeatConfig.OfflineCloseBusinessSeconds
                                && !_hasLoggedOffline3)
                            {
                                _hasLoggedOffline3 = true;
                                needClose = true;
                            }
                        }

                        if (needClose)
                        {
                            await _business3Sem.WaitAsync(token).ConfigureAwait(false);
                            try
                            {
                                if (_business3Connected || _business3PlcNet != null)
                                {
                                    try { _business3PlcNet?.ConnectClose(); } catch { }
                                    _business3Connected = false;
                                    _business3ReconnectFailCount = 0;
                                }
                            }
                            finally { _business3Sem.Release(); }

                            publicAddress.AddLog(
                                $"[业务3] {_ip}:{_port} 站号【{_stationInfo}】心跳离线超过{HeartbeatConfig.OfflineCloseBusinessSeconds}秒，主动关闭连接释放资源");
                        }

                        await Task.Delay(500, token).ConfigureAwait(false);
                        continue;
                    }

                    lock (_offlineLock)
                    {
                        if (_isOfflineRecorded3)
                        {
                            _isOfflineRecorded3 = false;
                            _hasLoggedOffline3 = false;
                        }
                    }

                    // 连接不可用：按退避等级睡眠
                    if (!await EnsureBusiness3ConnectionAsync(3000).ConfigureAwait(false))
                    {
                        int backoffSec = GetBackoffSeconds(_business3ReconnectFailCount);
                        int sleepMs = Math.Max(500, backoffSec * 1000);
                        publicAddress.AddLog($"[业务3] {_ip}:{_port} 连接不可用，退避 {sleepMs}ms 后再试");
                        await Task.Delay(sleepMs, token).ConfigureAwait(false);
                        continue;
                    }

                    // 遍历站点，单次抢锁
                    foreach (var station in _stations.Values)
                    {
                        var __tW = DateTime.Now;
                        await _business3Sem.WaitAsync(token).ConfigureAwait(false);
                        var __wMs = (DateTime.Now - __tW).TotalMilliseconds;

                        if (__wMs > 500)
                        {
                            publicAddress.AddLog($"[业务3-等锁] {_ip}:{_port} 等锁={__wMs:F0}ms");
                        }

                        try
                        {
                            try
                            {
                                // ★ 改为异步预检
                                if (!await station.QuickCheckBarcodeScanAsync(_business3PlcNet).ConfigureAwait(false))
                                    continue;

                                var __tExec = DateTime.Now;
                                try
                                {
                                    // 【关键改动】改为异步版
                                    await station.ExecuteBarcodeScanAsync(_business3PlcNet).ConfigureAwait(false);
                                }
                                catch (Exception ex)
                                {
                                    _business3Connected = false;
                                    publicAddress.AddLog($"业务3执行异常: {ex.Message}");
                                }
                                finally
                                {
                                    var __eMs = (DateTime.Now - __tExec).TotalMilliseconds;
                                    if (__eMs > 1000)
                                    {
                                        publicAddress.AddLog(
                                            $"[业务3-执行] {_ip}:{_port} 站号={station.PLCstationNum} 耗时={__eMs:F0}ms");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _business3Connected = false;
                                publicAddress.AddLog($"业务3预检异常: {ex.Message}");
                            }
                        }
                        finally { _business3Sem.Release(); }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    publicAddress.AddLog($"业务3循环异常:{ex.Message}");
                }

                try { await Task.Delay(200, token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }

        /// <summary>获取当前 PLCService 下所有注册站的信息（用于日志）。</summary>
        private string GetStationInfoForLog()
        {
            if (_stations == null || _stations.Count == 0) return "无注册站";

            var list = new System.Collections.Generic.List<string>();
            foreach (var station in _stations.Values)
                list.Add($"{station.PLCstationNum}:{station.PLCname}");

            return string.Join(",", list);
        }

        // ==================== 释放资源 ====================

        /// <summary>
        /// 释放资源：停止所有后台线程/任务，关闭 4 条连接，取消事件订阅，重置所有状态。
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            // 先取消事件订阅，避免 Dispose 过程中还收到事件
            UnsubscribeFromHeartbeatEvents();

            // 取消后台任务
            try { _cts?.Cancel(); } catch { }

            // 等心跳线程退出（最多 2 秒）
            try
            {
                if (_threadHeartbeat != null && _threadHeartbeat.IsAlive)
                {
                    if (!_threadHeartbeat.Join(2000))
                    {
                        publicAddress.AddLog(
                            $"[PLCService] {_ip}:{_port} 心跳线程退出超时(>2s)，将在下次 socket 超时后自行退出");
                    }
                }
            }
            catch { }

            // 等业务任务退出
            try { _taskBusiness1?.Wait(2000); } catch { }
            try { _taskBusiness2?.Wait(2000); } catch { }
            try { _taskBusiness3?.Wait(2000); } catch { }

            // 关闭 4 条连接
            try { _heartbeatPlcNet?.ConnectClose(); } catch { }
            _heartbeatConnected = false;

            try { _business1PlcNet?.ConnectClose(); } catch { }
            _business1Connected = false;

            try { _business2PlcNet?.ConnectClose(); } catch { }
            _business2Connected = false;

            try { _business3PlcNet?.ConnectClose(); } catch { }
            _business3Connected = false;

            // 释放资源
            try { _cts?.Dispose(); } catch { }

            try { _heartbeatSem?.Dispose(); } catch { }
            try { _business1Sem?.Dispose(); } catch { }
            try { _business2Sem?.Dispose(); } catch { }
            try { _business3Sem?.Dispose(); } catch { }

            _isHeartbeatOnline = false;
            lock (_offlineLock)
            {
                _isOfflineRecordedHeartbeat = false; _hasLoggedOfflineHeartbeat = false;
                _isOfflineRecorded1 = false; _hasLoggedOffline1 = false;
                _isOfflineRecorded2 = false; _hasLoggedOffline2 = false;
                _isOfflineRecorded3 = false; _hasLoggedOffline3 = false;
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// PLC 服务工厂：按 "IP:Port" 缓存 PLCService 实例。
    ///
    /// 【修复】ConcurrentDictionary.GetOrAdd 的 valueFactory 可能并发执行多次，
    /// 被丢弃的 PLCService 构造函数已 StartBackgroundTasks()，会泄漏心跳线程、
    /// 3 个业务循环和 4 条连接。改用 Lazy&lt;T&gt;(ExecutionAndPublication) 保证只构造一次。
    /// </summary>
    public static class PLCServiceFactory
    {
        // 值是 Lazy 盒子，真正的 PLCService 推迟到 .Value 时构造
        private static readonly ConcurrentDictionary<string, Lazy<PLCService>> _services =
            new ConcurrentDictionary<string, Lazy<PLCService>>();

        /// <summary>获取或创建指定 IP:Port 的 PLCService（同一 key 最多构造一次）。</summary>
        public static PLCService GetOrCreate(string ip, int port)
        {
            string key = $"{ip}:{port}";

            // valueFactory 只造 Lazy 盒子（轻量无副作用），即使并发多执行几次也无害；
            // 真正的 new PLCService(...) 由 Lazy 保证最多执行一次。
            var lazy = _services.GetOrAdd(
                key,
                k => new Lazy<PLCService>(
                    () => new PLCService(ip, port),
                    // 保证多线程并发访问 .Value 时只有一个线程执行工厂，其他等待
                    LazyThreadSafetyMode.ExecutionAndPublication));

            // 触发懒加载：首次访问构造实例，后续直接返回已创建实例
            return lazy.Value;
        }

        /// <summary>移除并释放指定 IP:Port 的 PLCService。当前代码无调用点，预留接口。</summary>
        public static void Remove(string ip, int port)
        {
            if (_services.TryRemove($"{ip}:{port}", out var lazy))
            {
                // 未构造过就不 Dispose，避免触发构造白白启动后台线程
                if (lazy.IsValueCreated)
                    lazy.Value.Dispose();
            }
        }
    }
}