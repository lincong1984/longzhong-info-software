using System;
using System.Data;
using System.Threading.Tasks;
using MES_SCAF;

namespace generaControlProcedure
{
    /// <summary>
    /// PLC 通讯核心类，每个实例对应一个数据库中的逻辑站（threadNum）。
    /// 不再自己管理后台线程，而是注册到对应 IP:Port 的 PLCService 中。
    /// 所有业务逻辑方法（inspection, patrolCheckout, verification 等）移到 StationContext 中。
    /// </summary>
    class receiveClass : IDisposable
    {
        // 站点的上下文对象，保存该站的所有配置和运行时数据
        private StationContext _context;
        // PLC 服务实例，负责与特定 IP:Port 的 PLC 进行底层通信
        private PLCService _service;
        // 释放标志，防止重复释放
        private bool _disposed = false;

        // 私有构造函数，禁止外部直接 new，必须通过工厂方法创建
        private receiveClass() { }

        /// <summary>
        /// 异步工厂方法，创建 receiveClass 实例并完成初始化
        /// </summary>
        /// <param name="plcstationNum">PLC 逻辑站号（数据库配置标识）</param>
        /// <param name="receive">接收线程编号（用于标识当前实例）</param>
        /// <returns>初始化完成的 receiveClass 实例</returns>
        public static async Task<receiveClass> CreateAsync(int plcstationNum, int receive)
        {
            var instance = new receiveClass();
            await instance.InitializeAsync(plcstationNum, receive);
            return instance;
        }

        /// <summary>
        /// 初始化：加载数据库配置，创建 StationContext，并注册到 PLCService。
        ///
        /// ============================================================
        /// 【本次改动：AddressTable 按 addnum 数字升序重排】
        /// ===========================================================
        ///   临时加一列 _sortKey = db号 * 1e9 + 偏移（long 类型），
        ///   按 _sortKey 升序排序，得到 798, 850, 902, ..., 1786 的正确顺序，
        ///   然后删除临时列。排序失败不阻断启动（退回原顺序）。
        /// ============================================================
        /// </summary>
        private async Task InitializeAsync(int plcstationNum, int receive)
        {
            // 将数据库操作和配置加载放入线程池，避免阻塞调用线程
            await Task.Run(() =>
            {
                try
                {
                    // ========== 以下为原 Initialize 方法中的配置加载逻辑 ==========
                    // 默认 PLC 连接参数（后续会被数据库配置覆盖）
                    string IP = "10.0.6.215";
                    int prot = 8501;
                    string PLCname = "";
                    int PLCstationNum = plcstationNum;
                    string ipProt = "";
                    string LowerAddress = "";      // 下限地址（category=15）
                    string UpperAddress = "";      // 上限地址（category=16）
                    string CRC_address = "";       // CRC 校验地址（category=04）
                    string signalAddress = "";     // 信号地址（category=05）
                    string planAddress = "";       // 计划地址（category=13）
                    string CardAddress = "";       // 卡号地址（category=18）
                    string jobNumberAddress = "";  // 工号地址（category=19）
                    string jurisdictionAddress = ""; // 权限地址（category=21）
                    string powerOnAddress = "";    // 上电地址（category=32）
                    int maxworknum = 0;
                    DataTable addressTable = new DataTable();
                    DataTable intOprNoXML = new DataTable();
                    DataSet dsTable = new DataSet();

                    // 查询 PLC 基础配置：IP、端口、名称
                    string sqlstr = "select top 1 * from fynT_PLCaddress where PLCstationNum=" + PLCstationNum.ToString();
                    DataTable dt = new DataTable();
                    SQLToolsYY sqlToolsYY = new SQLToolsYY();
                    Hint hint = sqlToolsYY.GetDataTable("", sqlstr, true, out dt);
                    if (dt.Rows.Count > 0)
                    {
                        IP = dt.Rows[0]["IPAddress"].ToString();
                        prot = Convert.ToInt32(dt.Rows[0]["Prot"].ToString());
                        ipProt = IP + ":" + prot.ToString();
                        PLCname = dt.Rows[0]["PLCname"].ToString();

                        // 批量查询：地址表、检验表、工单XML映射表
                        // 注意：这里的 "order by address" 是字符串排序，会导致 addnum 乱序
                        // 我们拿到数据后会重新排序，所以 SQL 不用改
                        string sql = @"SELECT * FROM fynV_PLCcommunicateAdd where PLCstationNum = " + PLCstationNum.ToString() + " order by address " +
                                    "SELECT * FROM fynV_inspection where PLCstationNum = " + PLCstationNum.ToString() + " " +
                                    "select * from dbo.fynV_intOprNoXML where PLCstationNum = " + PLCstationNum.ToString();
                        hint = sqlToolsYY.GetDataSet("", sql, true, out dsTable);
                        if (dsTable.Tables[0].Rows.Count > 0)
                        {
                            addressTable = dsTable.Tables[0];
                            intOprNoXML = dsTable.Tables[2];

                            // ============================================================
                            // ★★★ 核心修复：AddressTable 按 addnum 数字升序重排 ★★★
                            // ============================================================
                            // 【为什么必须排？】
                            //   SQL 是 order by address（字符串排序），
                            //   "DB200.DBD1006" 会排在 "DB200.DBD798" 前面（"1" < "7"），
                            //   导致 addnum 顺序：1006, 1058, ..., 1786, 798, 850, 902, 954
                            //   第 16 行从 1786 跳回 798（-988）：
                            //     - TryBulkReadPartCodes 的 stride 校验失败 → 批量读永远 False
                            //     - GetAddPackage 对 DInt/Real 的合并失效（负差值）
                            //
                            // 【排序方式】
                            //   临时列 _sortKey = db号 * 1e9 + 偏移（long 类型）
                            //   db号最大 65535，×1e9 约 6.5e13，远小于 long 上限，不溢出
                            //   排序后删除临时列，不影响后续 DataTable 结构
                            // ============================================================
                            try
                            {
                                // 添加临时排序键列
                                addressTable.Columns.Add("_sortKey", typeof(long));

                                // 计算每行的排序键
                                foreach (DataRow row in addressTable.Rows)
                                {
                                    string addnum = row["addnum"]?.ToString() ?? "";
                                    string[] parts = addnum.Split('.');

                                    if (parts.Length == 2
                                        && long.TryParse(parts[0], out long dbPart)
                                        && long.TryParse(parts[1], out long offPart))
                                    {
                                        // db号 * 1e9 + 偏移 → 先按 DB 号，再按偏移
                                        row["_sortKey"] = dbPart * 1000000000L + offPart;
                                    }
                                    else
                                    {
                                        // addnum 格式异常 → 排到最后，不影响其他行
                                        row["_sortKey"] = long.MaxValue;
                                    }
                                }

                                // 按 _sortKey 升序排序，ToTable() 生成新表
                                addressTable.DefaultView.Sort = "_sortKey ASC";
                                addressTable = addressTable.DefaultView.ToTable();

                                // 删除临时列
                                addressTable.Columns.Remove("_sortKey");

                                // 记录日志，便于现场核对
                                publicAddress.AddLog(
                                    $"[receiveClass] 站{PLCstationNum} 地址表已按 addnum 数字升序重排，共 {addressTable.Rows.Count} 行");
                            }
                            catch (Exception ex)
                            {
                                // 排序失败不阻断启动，只记录日志，退回原始顺序
                                publicAddress.AddLog(
                                    $"[receiveClass] 站{PLCstationNum} 地址表排序失败（不影响运行）: {ex.Message}");
                            }

                            // 复制地址表并添加 "value" 列，用于存储实时值，存入全局 publicAddress.dataset
                            // 注意：此时 addressTable 已经排序，temporaryTable 也是升序（界面显示会更合理）
                            DataTable temporaryTable = addressTable.Copy();
                            temporaryTable.TableName = "PLC_" + receive.ToString();
                            temporaryTable.Columns.Add("value", typeof(string));
                            publicAddress.dataset.Tables.Add(temporaryTable);

                            // 从地址表中提取各种特定类别的地址（用独立 DataView，不污染 addressTable.DefaultView）
                            DataView dv = new DataView(addressTable);

                            dv.RowFilter = "category='15' and workNumber='1' and PLCstationNum='" + PLCstationNum.ToString() + "'";
                            DataTable filterdt = dv.ToTable();
                            if (filterdt.Rows.Count > 0) LowerAddress = filterdt.Rows[0]["address"].ToString();

                            // ★ 原来这里误用 addressTable.DefaultView，会把共享表的过滤条件永久锁成 category='16'，已改为统一走 dv
                            dv.RowFilter = "category='16' and workNumber='1' and PLCstationNum='" + PLCstationNum.ToString() + "'";
                            filterdt = dv.ToTable();
                            if (filterdt.Rows.Count > 0) UpperAddress = filterdt.Rows[0]["address"].ToString();

                            dv.RowFilter = "category='04' and PLCstationNum='" + PLCstationNum.ToString() + "'";
                            filterdt = dv.ToTable();
                            if (filterdt.Rows.Count > 0) CRC_address = filterdt.Rows[0]["address"].ToString();

                            dv.RowFilter = "category='05' and PLCstationNum='" + PLCstationNum.ToString() + "'";
                            filterdt = dv.ToTable();
                            if (filterdt.Rows.Count > 0) signalAddress = filterdt.Rows[0]["address"].ToString();

                            dv.RowFilter = "PLCstationNum ='" + PLCstationNum.ToString() + "'";
                            filterdt = dv.ToTable();
                            if (filterdt.Rows.Count > 0) maxworknum = Convert.ToInt32(filterdt.Compute("max(workNumber)", "isnull(workNumber,0)>0"));

                            dv.RowFilter = "category='13' and workNumber='1' and PLCstationNum ='" + PLCstationNum.ToString() + "'";
                            filterdt = dv.ToTable();
                            if (filterdt.Rows.Count > 0) planAddress = filterdt.Rows[0]["address"].ToString();

                            dv.RowFilter = "category='18' and workNumber='1' and PLCstationNum ='" + PLCstationNum.ToString() + "'";
                            filterdt = dv.ToTable();
                            if (filterdt.Rows.Count > 0) CardAddress = filterdt.Rows[0]["address"].ToString();

                            dv.RowFilter = "category='19' and workNumber='1' and PLCstationNum ='" + PLCstationNum.ToString() + "'";
                            filterdt = dv.ToTable();
                            if (filterdt.Rows.Count > 0) jobNumberAddress = filterdt.Rows[0]["address"].ToString();

                            dv.RowFilter = "category='21' and workNumber='1' and PLCstationNum ='" + PLCstationNum.ToString() + "'";
                            filterdt = dv.ToTable();
                            if (filterdt.Rows.Count > 0) jurisdictionAddress = filterdt.Rows[0]["address"].ToString();

                            dv.RowFilter = "category='32' and workNumber='1' and PLCstationNum ='" + PLCstationNum.ToString() + "'";
                            filterdt = dv.ToTable();
                            if (filterdt.Rows.Count > 0) powerOnAddress = filterdt.Rows[0]["address"].ToString();
                        }
                    }

                    // 创建 StationContext 并填充所有配置属性
                    _context = new StationContext
                    {
                        ThreadNum = receive,
                        PLCstationNum = PLCstationNum,
                        LowerAddress = LowerAddress,
                        UpperAddress = UpperAddress,
                        AddressTable = addressTable,
                        IntOprNoXML = intOprNoXML,
                        DsTable = dsTable,
                        PLCname = PLCname,
                        IpProt = ipProt,
                        LastHeartbeatValue = 0      // 心跳初始值
                    };

                    // 获取或创建对应 IP:Port 的 PLCService（工厂模式），并将当前站点注册到该服务中
                    _service = PLCServiceFactory.GetOrCreate(IP, prot);
                    _service.RegisterStation(receive, _context);
                }
                catch (Exception ex)
                {
                    publicAddress.AddLog($"[receiveClass] 初始化站 {plcstationNum} (threadNum={receive}) 失败: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 关闭当前站点：从 PLCService 中注销，停止该站的数据刷新
        /// </summary>
        public void Close()
        {
            if (_service != null && _context != null)
                _service.UnregisterStation(_context.ThreadNum);
        }

        /// <summary>
        /// 实现 IDisposable 接口，释放资源时自动调用 Close
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                Close();
                _disposed = true;
            }
        }
    }
}