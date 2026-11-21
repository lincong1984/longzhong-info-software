using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using TCPconnectSimaticPLC;
using System.Data;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;   // 【新增】异步方法需要
using System.Linq;

namespace generaControlProcedure
{
    /// <summary>
    /// PLC 读写业务封装类。
    ///
    /// ============================================================
    /// 【职责】
    /// ============================================================
    /// - 地址封包（GetAddPackage）：把连续地址合并成一次批量读/写
    /// - 参数读写（readParameter / writeParameter / saveParameter / sendParameter）
    /// - 结果保存（saveResults / calibrationSaveResults）
    /// - 条码扫描（scanBarCcode / calibrationScanBarCcode）
    /// - 刷卡、写零件ID、写打印数据、写工序号、写计划数等
    ///
    /// ============================================================
    /// 【本次改动】
    /// ============================================================
    /// 1. sendfinish 从共享 int 改为 ConcurrentDictionary，按 key 隔离
    /// 2. 新增异步版本（Async 后缀），内部用 Simatic_NET 的硬超时 API
    /// 3. String 批量读优化（TryBulkReadStringsInResults / TryBulkReadPartCodes）
    /// ============================================================
    /// </summary>
    class PLCRead_WriteClass
    {

        #region 参数下发状态：sendFinish（按 key 隔离）
        // ============================================================
        // sendfinish 改为 ConcurrentDictionary（按 key 隔离）
        // 说明：原来的 sendFinish 是普通 int 字段，多个工位/多站并发下发参数时
        //       会互相覆盖。改为按 key（"sp_站点号" / "scp_站点号_工位号"）
        //       隔离，互不影响。
        // ============================================================
        private readonly ConcurrentDictionary<string, int> _sendFinishMap =
            new ConcurrentDictionary<string, int>();

        /// <summary>读取指定 key 的 sendFinish 标志（不存在返回 0）。</summary>
        private int GetSendFinish(string key) => _sendFinishMap.TryGetValue(key, out int v) ? v : 0;

        /// <summary>设置指定 key 的 sendFinish 标志。</summary>
        private void SetSendFinish(string key, int value) => _sendFinishMap[key] = value;

        #endregion


        #region 异步版本核心业务方法

        /// <summary>
        /// 异步版本：SaveResults（结果保存）。
        /// 所有 PLC 读写均为异步，单次硬超时由 Simatic_NET 内部保证。
        ///
        /// 【流程】
        ///   1. 过滤本站，取最大工位
        ///   2. 逐工位读 saveAddress（category=08）
        ///   3. 若值为 3 才进入保存流程
        ///   4. delete 旧临时结果
        ///   5. 尝试批量读 String，命中则用缓存
        ///   6. 逐包读 DInt/Real/String → 拼 SQL 写临时表
        ///   7. 调 fynP_saveResults 落正式表
        ///   8. 若成功则执行业务（writeBarCode）、写 1 / 2 到 saveAddress
        ///   9. 最多 5 轮写并重读，直到 PLC 清零
        /// </summary>
        public async Task<bool> SaveResultsAsync(Simatic_NET PLC_NET, int PLCstationNum, DataTable addressTable, string ip, int port, DataTable intOprNoXML)
        {
            var __tMethod = DateTime.Now;

            // 重试 / 等待参数常量
            const int MAX_SAVE_ROUND = 5;          // 最多重试轮次
            const int MAX_WRITE_RETRY = 2;         // 单轮写重试次数
            const int WRITE_RETRY_SLEEP = 200;     // 写重试间隔(ms)
            const int ROUND_READ_SLEEP = 500;      // 每轮写完等待 PLC 处理的间隔(ms)

            try
            {
                bool returns = false;
                SQLToolsYY sqltoolsyy = new SQLToolsYY();
                DataTable udt = new DataTable("tablevalue");
                string sqlstr = "";
                Hint hint = new Hint();
                DataTable dt = new DataTable();
                DataTable dt1 = new DataTable();
                string saveAddress = "";
                int worknum = 0;
                DataTable adddt = new DataTable("address");
                DataView dv = new DataView(addressTable);   // ★ 独立视图，不污染 addressTable.DefaultView
                string str = "";

                if (dv.Count == 0) return false;

                // 过滤本站
                dv.RowFilter = "PLCstationNum = " + PLCstationNum.ToString();
                adddt = dv.ToTable();
                if (adddt.Rows.Count == 0) return false;

                // 计算最大工位号
                worknum = Convert.ToInt32(adddt.Compute("max(workNumber)", "isnull(workNumber,0)>0"));
                publicAddress.AddLog($"[saveResultsAsync-入口] 站{PLCstationNum} 工位数={worknum}");

                // 逐工位处理
                for (int j = 1; j <= worknum; j++)
                {
                    var __tWork = DateTime.Now;

                    // 查找本工位的 saveAddress（category=08）
                    dv.RowFilter = "category = '08' and PLCstationNum = " + PLCstationNum.ToString() + " and workNumber = " + j.ToString();
                    adddt.Clear();
                    adddt = dv.ToTable();
                    if (adddt.Rows.Count == 0) continue;

                    saveAddress = adddt.Rows[0]["address"].ToString();

                    // ★ 关键改动1：读标志改为异步
                    var __tReadFlag = DateTime.Now;
                    OperateResult<int> saveSignal = await PLC_NET.ReadInt32Async(saveAddress).ConfigureAwait(false);
                    var __readFlagMs = (DateTime.Now - __tReadFlag).TotalMilliseconds;
                    if (__readFlagMs > 200)
                        publicAddress.AddLog($"[saveResultsAsync-读标志] 站{PLCstationNum} 工位{j} 地址={saveAddress} 耗时={__readFlagMs:F0}ms success={saveSignal?.IsSuccess}");

                    // 不是 3 说明 PLC 没请求保存
                    if (saveSignal == null || !saveSignal.IsSuccess || saveSignal.Content != 3)
                    {
                        // 单工位耗时日志
                        var __workMsSkip = (DateTime.Now - __tWork).TotalMilliseconds;
                        if (__workMsSkip > 500)
                            publicAddress.AddLog($"[saveResultsAsync-单工位] 站{PLCstationNum} 工位{j} 总耗时={__workMsSkip:F0}ms");
                        continue;
                    }

                    // 清空旧临时结果
                    var __tDel = DateTime.Now;
                    hint = sqltoolsyy.GetDataTable("", "delete fynT_TemporaryResult where PLCstationNum=" + PLCstationNum.ToString() + "and workNumber =" + j.ToString(), true, out dt);
                    var __delMs = (DateTime.Now - __tDel).TotalMilliseconds;
                    if (__delMs > 200)
                        publicAddress.AddLog($"[saveResultsAsync-delete] 站{PLCstationNum} 工位{j} delete耗时={__delMs:F0}ms");

                    // delete 失败：跳过本工位
                    if (hint.Valid != true)
                    {
                        var __workMs = (DateTime.Now - __tWork).TotalMilliseconds;
                        if (__workMs > 500)
                            publicAddress.AddLog($"[saveResultsAsync-单工位] 站{PLCstationNum} 工位{j} 总耗时={__workMs:F0}ms");
                        continue;
                    }

                    // 查询本工位要保存的地址
                    dv.RowFilter = "isnull(dataType,'')<>''and PLCstationNum=" + PLCstationNum.ToString() + " and category>=0201 and category<210 and len(category)<=4 and workNumber=" + j.ToString();
                    DataTable readData = dv.ToTable();
                    udt = GetAddPackage(readData);

                    // 尝试批量读 String
                    var __tBulk = DateTime.Now;
                    Dictionary<string, string> batchStringCache = await TryBulkReadStringsInResultsAsync(PLC_NET, readData, dv).ConfigureAwait(false);
                    var __bulkMs = (DateTime.Now - __tBulk).TotalMilliseconds;
                    bool bulkOk = batchStringCache.Count > 0;

                    var __tReadLoop = DateTime.Now;
                    int __readLoopOk = 0, __readLoopFail = 0, __readLoopBatchHit = 0;

                    // 逐包读 → 拼 SQL 写临时表
                    for (int jj = 0; jj < udt.Rows.Count; jj++)
                    {
                        OperateResult<int[]> readInt = new OperateResult<int[]>();
                        OperateResult<float[]> readReal = new OperateResult<float[]>();
                        OperateResult<string> readString = new OperateResult<string>();
                        str = udt.Rows[jj]["add"].ToString();
                        // 起始 SQL 前缀，每个分支会拼接 VALUES
                        sqlstr = "insert into fynT_TemporaryResult ([PLCstationNum],[PLCAddress],[parameterValues],[workNumber]) values ";

                        // 按数据类型分发
                        switch (udt.Rows[jj]["datatype"].ToString())
                        {
                            case ("DInt"):
                                // ★ count 参数显式传 0，走 Array 超时配置
                                readInt = await PLC_NET.ReadInt32Async(str.ToUpper(), Convert.ToInt32(udt.Rows[jj]["nul"]), 0).ConfigureAwait(false);
                                goto readInt;
                            case ("Real"):
                                readReal = await PLC_NET.ReadRealAsync(str.ToUpper(), Convert.ToInt32(udt.Rows[jj]["nul"])).ConfigureAwait(false);
                                goto readReal;
                            case ("String"):
                                // 批量命中则直接用缓存，否则单个读
                                if (bulkOk && batchStringCache.TryGetValue(str.ToUpper(), out string cachedVal))
                                {
                                    readString = new OperateResult<string> { IsSuccess = true, Content = cachedVal };
                                    __readLoopBatchHit++;
                                }
                                else
                                {
                                    readString = await PLC_NET.ReadStringAsync(str.ToUpper(), 50).ConfigureAwait(false);
                                }
                                goto readString;
                        }
                        continue;

                        // ---- DInt 分支 ----
                        readInt:
                        if (readInt.IsSuccess)
                        {
                            __readLoopOk++;
                            // 逐元素拼地址（每个元素 +4 偏移）
                            for (int i = 0; i < readInt.Content.Length; i++)
                            {
                                string add = str;
                                string add_1 = Regex.Replace(str, "[0-9]", "");
                                string add_2 = add_1.Substring(add_1.Length - 1, 1);
                                int adds = add.LastIndexOf(add_2);
                                string addstr = add.Substring(0, adds + 1)
                                              + Convert.ToString(Convert.ToInt32(add.Substring(adds + 1, add.Length - adds - 1)) + (i * 4));
                                sqlstr = sqlstr + "(" + PLCstationNum.ToString() + ",'" + addstr + "','" + readInt.Content[i].ToString() + "'," + j.ToString() + ") , ";
                            }
                            // 去掉末尾多余逗号
                            hint = sqltoolsyy.GetDataTable("", sqlstr.Substring(0, sqlstr.Length - 2), true, out dt1);
                            if (hint.Valid == false)
                                publicAddress.AddLog($"错误：报错信息:【{hint.Msg}】");
                        }
                        else __readLoopFail++;
                        continue;

                        // ---- Real 分支 ----
                        readReal:
                        if (readReal.IsSuccess)
                        {
                            __readLoopOk++;
                            for (int i = 0; i < readReal.Content.Length; i++)
                            {
                                string add = str;
                                string add_1 = Regex.Replace(str, "[0-9]", "");
                                string add_2 = add_1.Substring(add_1.Length - 1, 1);
                                int adds = add.LastIndexOf(add_2);
                                string addstr = add.Substring(0, adds + 1)
                                              + Convert.ToString(Convert.ToInt32(add.Substring(adds + 1, add.Length - adds - 1)) + (i * 4));
                                sqlstr = sqlstr + "(" + PLCstationNum.ToString() + ",'" + addstr + "','" + readReal.Content[i].ToString() + "'," + j.ToString() + ") , ";
                            }
                            hint = sqltoolsyy.GetDataTable("", sqlstr.Substring(0, sqlstr.Length - 2), true, out dt1);
                            if (hint.Valid == false)
                                publicAddress.AddLog($"错误：报错信息:【{hint.Msg}】");
                        }
                        else __readLoopFail++;
                        continue;

                        // ---- String 分支 ----
                        readString:
                        if (readString.IsSuccess)
                        {
                            __readLoopOk++;
                            // 单引号转义，防止 SQL 语法错误
                            sqlstr = sqlstr + "(" + PLCstationNum.ToString() + ",'" + str + "','" + readString.Content.ToString().Replace("'", "''") + "'," + j.ToString() + ")";
                            hint = sqltoolsyy.GetDataTable("", sqlstr, true, out dt1);
                            if (hint.Valid == false)
                                publicAddress.AddLog($"错误：报错信息:【{hint.Msg}】");
                        }
                        else __readLoopFail++;
                        continue;
                    }

                    // 读循环耗时日志
                    var __readLoopMs = (DateTime.Now - __tReadLoop).TotalMilliseconds;
                    if (__readLoopMs > 500)
                        publicAddress.AddLog($"[saveResultsAsync-读循环] 站{PLCstationNum} 工位{j} 包数={udt.Rows.Count} 成功={__readLoopOk} 失败={__readLoopFail} 批量命中={__readLoopBatchHit} 批量耗时={__bulkMs:F0}ms 总耗时={__readLoopMs:F0}ms");

                    // 落正式表
                    var __tSp = DateTime.Now;
                    hint = sqltoolsyy.GetDataTable("", "exec fynP_saveResults " + j.ToString() + "," + PLCstationNum.ToString(), true, out dt1);
                    var __spMs = (DateTime.Now - __tSp).TotalMilliseconds;
                    if (__spMs > 500)
                        publicAddress.AddLog($"[saveResultsAsync-SP] 站{PLCstationNum} 工位{j} fynP_saveResults耗时={__spMs:F0}ms 行数={dt1.Rows.Count} valid={hint?.Valid}");

                    if (hint.Valid != true || dt1.Rows.Count == 0)
                    {
                        // 落正式表失败
                        if (hint.Valid != true)
                        {
                            string sqlStr = "exec fynP_saveResults " + j.ToString() + "," + PLCstationNum.ToString();
                            publicAddress.AddLog($"错误：PLC站号【{PLCstationNum}】工位号【{j}】执行数据库语句【{sqlStr}】失败，失败原因【{hint.Msg}】");
                        }
                        if (dt1.Rows.Count <= 0)
                            publicAddress.AddLog($"错误：PLC站号【{PLCstationNum}】工位号【{j}】执行数据库语句【exec fynP_saveResults {j},{PLCstationNum}】成功，但语句无返回值");

                        var __workMsErr = (DateTime.Now - __tWork).TotalMilliseconds;
                        if (__workMsErr > 500)
                            publicAddress.AddLog($"[saveResultsAsync-单工位] 站{PLCstationNum} 工位{j} 总耗时={__workMsErr:F0}ms");
                        continue;
                    }

                    // 决定写 1 还是 2
                    int finalWriteValue;
                    if (Convert.ToInt16(dt1.Rows[0][0]) == 1)
                    {
                        try
                        {
                            // 有 start=1 的工序才写条码（改为异步版）
                            var __tBiz = DateTime.Now;
                            DataRow[] datarow = intOprNoXML.Select("PLCstationNum=" + PLCstationNum.ToString() + " and workNumber=" + j.ToString() + " and start=1");
                            if (datarow.Length > 0)
                                await WriteBarCodeAsync(PLC_NET, intOprNoXML, addressTable).ConfigureAwait(false);
                            var __bizMs = (DateTime.Now - __tBiz).TotalMilliseconds;
                            if (__bizMs > 500)
                                publicAddress.AddLog($"[saveResultsAsync-业务] 站{PLCstationNum} 工位{j} writeBarCode耗时={__bizMs:F0}ms");

                            finalWriteValue = 1;
                        }
                        catch (Exception ex)
                        {
                            // 业务执行异常：写 2 表示失败
                            finalWriteValue = 2;
                            publicAddress.AddLog($"错误：PLC站号【{PLCstationNum}】工号【{j}】业务执行异常【{ex.Message}】");
                        }
                    }
                    else
                    {
                        finalWriteValue = 2;
                        publicAddress.AddLog($"错误：PLC站号【{PLCstationNum}】工位号【{j}】参数保存失败错误原因【{dt1.Rows[0][1]}】");
                    }

                    // 写 1/2 到 PLC 并等待清零
                    var __tWhile = DateTime.Now;
                    int __round = 0;
                    string __exitReason = "unknown";

                    // 最多 MAX_SAVE_ROUND 轮写并重读
                    for (__round = 0; __round < MAX_SAVE_ROUND; __round++)
                    {
                        // PLC 已清零则退出
                        if (saveSignal == null || !saveSignal.IsSuccess || saveSignal.Content != 3)
                        {
                            __exitReason = "PLC已清零";
                            break;
                        }

                        bool writeOk = false;
                        // 单轮写最多 MAX_WRITE_RETRY 次
                        for (int retry = 0; retry < MAX_WRITE_RETRY; retry++)
                        {
                            var __tW = DateTime.Now;
                            // ★ 关键改动2：写标志改为异步
                            var r = await PLC_NET.WriteInt32Async(saveAddress, finalWriteValue).ConfigureAwait(false);
                            var __wMs = (DateTime.Now - __tW).TotalMilliseconds;

                            if (r != null && r.IsSuccess)
                            {
                                writeOk = true;
                                if (__wMs > 200)
                                    publicAddress.AddLog($"[saveResultsAsync-写{finalWriteValue}] 站{PLCstationNum} 工位{j} 地址={saveAddress} 耗时={__wMs:F0}ms");
                                break;
                            }

                            if (retry < MAX_WRITE_RETRY - 1)
                            {
                                if (__wMs > 200)
                                    publicAddress.AddLog($"[saveResultsAsync-写{finalWriteValue}重试] 站{PLCstationNum} 工位{j} 第{retry + 1}次失败 耗时={__wMs:F0}ms msg={r?.Message}");
                                Thread.Sleep(WRITE_RETRY_SLEEP);
                            }
                            else
                            {
                                publicAddress.AddLog($"[saveResultsAsync-写{finalWriteValue}失败] 站{PLCstationNum} 工位{j} 共{MAX_WRITE_RETRY}次均失败 msg={r?.Message}");
                            }
                        }

                        if (!writeOk)
                        {
                            __exitReason = "写操作失败";
                            break;
                        }

                        // 等 PLC 处理
                        Thread.Sleep(ROUND_READ_SLEEP);
                        // ★ 关键改动3：重读标志改为异步
                        saveSignal = await PLC_NET.ReadInt32Async(saveAddress).ConfigureAwait(false);

                        if (saveSignal == null || !saveSignal.IsSuccess)
                        {
                            __exitReason = "重读失败";
                            break;
                        }

                        if (saveSignal.Content != 3)
                        {
                            __exitReason = "PLC已清零";
                            break;
                        }
                    }

                    // 写循环耗时日志
                    var __whileMs = (DateTime.Now - __tWhile).TotalMilliseconds;
                    if (__whileMs > 500)
                        publicAddress.AddLog($"[saveResultsAsync-写{finalWriteValue}循环] 站{PLCstationNum} 工位{j} 轮次={__round} 耗时={__whileMs:F0}ms 退出原因={__exitReason}");

                    if (__round >= MAX_SAVE_ROUND && saveSignal != null && saveSignal.IsSuccess && saveSignal.Content == 3)
                    {
                        publicAddress.AddLog($"警告：站{PLCstationNum} 工位{j} 重试{MAX_SAVE_ROUND}轮后PLC仍未清零，放弃本轮");
                    }

                    // 单工位总耗时
                    var __workMs2 = (DateTime.Now - __tWork).TotalMilliseconds;
                    if (__workMs2 > 500)
                        publicAddress.AddLog($"[saveResultsAsync-单工位] 站{PLCstationNum} 工位{j} 总耗时={__workMs2:F0}ms");
                }

                returns = true;
                return returns;
            }
            catch (Exception ex)
            {
                publicAddress.AddLog($"错误：saveResultsAsync 报错信息:【{ex.Message}】");
                return false;
            }
            finally
            {
                // 总耗时超过 2s 记录
                var __mMs = (DateTime.Now - __tMethod).TotalMilliseconds;
                if (__mMs > 2000)
                    publicAddress.AddLog($"[saveResultsAsync-总耗时] 站{PLCstationNum} 耗时={__mMs:F0}ms");
            }
        }

        /// <summary>
        /// 异步版本：ScanBarCcode（条码扫描）。
        /// - 原版在每工位、每包前检查总耗时超过 10 秒就 break，
        ///   会导致剩余字段不读，fynT_TemporaryPartCode 表缺数据。
        /// - 现在移除所有方法级截断，保证所有字段都会被处理到。
        /// - 单次异步调用仍由 Simatic_NET 硬超时保护（默认 2000ms），
        ///   不会出现单个字段卡 39 秒这种长时间阻塞。
        ///
        /// 【行为对比】
        /// - 新版：单字段卡死 2000ms → 该字段失败 → 继续下一个字段
        ///         20 个字段最坏 20×2000=40 秒返回，但每段都是"2 秒超时后继续"，
        ///         不是"线程死等 40 秒"，且数据不丢。
        /// </summary>
        public async Task<bool> ScanBarCcodeAsync(Simatic_NET PLC_NET, int PLCstationNum, DataTable addressTable, DataTable intOprNoXML)
        {
            bool returns = false;
            var __tMethod = DateTime.Now;

            try
            {
                SQLToolsYY sqltoolsyy = new SQLToolsYY();
                DataTable udt = new DataTable("tablevalue");
                string intostring = "";
                Hint hint = new Hint();
                DataTable dt = new DataTable();
                DataTable dt1 = new DataTable();
                string scanAddress = "";
                int worknum = 0;
                DataTable adddt = new DataTable("address");
                DataView dv = new DataView(addressTable);   // ★ 独立视图
                string str;

                if (dv.Count == 0) return false;

                // 过滤本站
                dv.RowFilter = "PLCstationNum = " + PLCstationNum.ToString();
                adddt = dv.ToTable();
                if (adddt.Rows.Count == 0) return false;

                // 计算最大工位号
                worknum = Convert.ToInt32(adddt.Compute("max(workNumber)", "isnull(workNumber,0)>0"));

                // 逐工位处理
                for (int j = 1; j <= worknum; j++)
                {
                    // 找扫码触发地址（category=19）
                    dv.RowFilter = "category = '19' and PLCstationNum = " + PLCstationNum.ToString() + " and workNumber = " + j.ToString();
                    adddt.Clear();
                    adddt = dv.ToTable();
                    if (adddt.Rows.Count == 0) continue;

                    scanAddress = adddt.Rows[0]["address"].ToString();

                    var __tReadFlag = DateTime.Now;
                    OperateResult<int> saveSignal = await PLC_NET.ReadInt32Async(scanAddress).ConfigureAwait(false);
                    var __msReadFlag = (DateTime.Now - __tReadFlag).TotalMilliseconds;
                    if (__msReadFlag > 200)
                        publicAddress.AddLog($"[scanBarCcodeAsync-读标志] 站{PLCstationNum} 工位{j} 耗时={__msReadFlag:F0}ms success={saveSignal?.IsSuccess}");

                    if (saveSignal == null || !saveSignal.IsSuccess) continue;
                    if (saveSignal.Content != 3) continue;

                    // 清空临时条码表
                    hint = sqltoolsyy.GetDataTable("", "delete fynT_TemporaryBarCode where PLCstationNum=" + PLCstationNum.ToString() + " and workNumber =" + j.ToString(), true, out dt);
                    if (hint.Valid != true) continue;

                    // 取扫码地址集合（category=0201）
                    dv.RowFilter = "category = '0201' and PLCstationNum = " + PLCstationNum.ToString() + " and workNumber = " + j.ToString();
                    udt = GetAddPackage(dv.ToTable());

                    var __tReadLoop = DateTime.Now;
                    int __readLoopOk = 0, __readLoopFail = 0;

                    // 读条码字段
                    for (int jj = 0; jj < udt.Rows.Count; jj++)
                    {
                        OperateResult<int[]> readInt = new OperateResult<int[]>();
                        OperateResult<string> readString = new OperateResult<string>();
                        str = udt.Rows[jj]["add"].ToString();
                        intostring = "insert into fynT_TemporaryBarCode ([PLCstationNum],[PLCAddress],[Values],[workNumber]) values ";

                        switch (udt.Rows[jj]["datatype"].ToString())
                        {
                            case ("DInt"):
                                // 显式传 0 作为 timeoutMs，避免与单值重载歧义
                                readInt = await PLC_NET.ReadInt32Async(str.ToUpper(), Convert.ToInt32(udt.Rows[jj]["nul"]), 0).ConfigureAwait(false);
                                goto readInt;
                            case ("String"):
                                readString = await PLC_NET.ReadStringAsync(str.ToUpper(), 50).ConfigureAwait(false);
                                goto readString;
                        }

                        // ---- DInt 分支 ----
                        readInt:
                        if (readInt.IsSuccess)
                        {
                            __readLoopOk++;
                            for (int i = 0; i < readInt.Content.Length; i++)
                            {
                                string add = str;
                                string add_1 = Regex.Replace(str, "[0-9]", "");
                                string add_2 = add_1.Substring(add_1.Length - 1, 1);
                                int adds = add.LastIndexOf(add_2);
                                string addstr = add.Substring(0, adds + 1)
                                              + Convert.ToString(Convert.ToInt32(add.Substring(adds + 1, add.Length - adds - 1)) + (i * 4));
                                intostring = intostring + "(" + PLCstationNum.ToString() + ",'" + addstr + "','" + readInt.Content[i].ToString() + "'," + j.ToString() + ") , ";
                            }
                            hint = sqltoolsyy.GetDataTable("", intostring.Substring(0, intostring.Length - 2), true, out dt1);
                            if (hint.Valid == false)
                                publicAddress.AddLog($"错误【{hint.Msg}】");
                        }
                        else __readLoopFail++;
                        continue;

                        // ---- String 分支 ----
                        readString:
                        if (readString.IsSuccess)
                        {
                            __readLoopOk++;
                            intostring = intostring + "(" + PLCstationNum.ToString() + ",'" + str + "','" + readString.Content.Replace("'", "''") + "'," + j.ToString() + ")";
                            hint = sqltoolsyy.GetDataTable("", intostring, true, out dt1);
                            if (hint.Valid == false)
                                publicAddress.AddLog($"错误【{hint.Msg}】");
                        }
                        else __readLoopFail++;
                        continue;
                    }

                    // 读循环耗时日志
                    var __msReadLoop = (DateTime.Now - __tReadLoop).TotalMilliseconds;
                    if (__msReadLoop > 500)
                        publicAddress.AddLog($"[scanBarCcodeAsync-读循环] 站{PLCstationNum} 工位{j} 包数={udt.Rows.Count} 成功={__readLoopOk} 失败={__readLoopFail} 耗时={__msReadLoop:F0}ms");

                    // 判码
                    var __tJudge = DateTime.Now;
                    hint = sqltoolsyy.GetDataTable("", "exec fynP_judgeBarCode " + j.ToString() + "," + PLCstationNum.ToString(), true, out dt1);
                    var __msJudge = (DateTime.Now - __tJudge).TotalMilliseconds;
                    if (__msJudge > 500)
                        publicAddress.AddLog($"[scanBarCcodeAsync-判码SP] 站{PLCstationNum} 工位{j} 耗时={__msJudge:F0}ms 行数={dt1.Rows.Count} valid={hint?.Valid}");

                    if (hint.Valid != true || dt1.Rows.Count == 0) continue;

                    // dt1.Rows[0][2]==1 表示判码 OK
                    if (Convert.ToInt16(dt1.Rows[0][2]) == 1)
                    {
                        // 合格分支：写 1、打印、读零件条码
                        var __tWrite1 = DateTime.Now;
                        var __r1 = await PLC_NET.WriteInt32Async(scanAddress, 1).ConfigureAwait(false);
                        var __msWrite1 = (DateTime.Now - __tWrite1).TotalMilliseconds;
                        if (__msWrite1 > 200 || __r1 == null || !__r1.IsSuccess)
                            publicAddress.AddLog($"[scanBarCcodeAsync-写1] 站{PLCstationNum} 工位{j} 耗时={__msWrite1:F0}ms 结果={__r1?.IsSuccess}");

                        // ★ 去掉方法级截断：无条件执行 WritePrintData
                        var __tPrint = DateTime.Now;
                        await WritePrintDataAsync(PLC_NET, intOprNoXML, addressTable).ConfigureAwait(false);
                        var __msPrint = (DateTime.Now - __tPrint).TotalMilliseconds;
                        if (__msPrint > 500)
                            publicAddress.AddLog($"[scanBarCcodeAsync-WritePrintData] 站{PLCstationNum} 工位{j} 耗时={__msPrint:F0}ms");

                        // ★ 去掉方法级截断：无条件读零件条码
                        var __tPart = DateTime.Now;

                        // 清空临时零件条码表
                        hint = sqltoolsyy.GetDataTable("", "delete fynT_TemporaryPartCode where PLCstationNum=" + PLCstationNum.ToString() + " and workNumber =" + j.ToString(), true, out dt);

                        // 取零件条码地址（210~229）
                        dv.RowFilter = "isnull(dataType,'')<>''and PLCstationNum=" + PLCstationNum.ToString() + " and category>=210 and category<=229 and len(category)<=4 and workNumber=" + j.ToString();
                        DataTable partAddrTable = dv.ToTable();

                        List<string> valueRows = new List<string>();
                        // 优先批量读
                        bool bulkOk = await TryBulkReadPartCodesAsync(PLC_NET, partAddrTable, PLCstationNum, j, valueRows).ConfigureAwait(false);

                        // 批量读失败 → 回退单个读
                        if (!bulkOk)
                        {
                            udt = GetAddPackage(partAddrTable);
                            for (int jj = 0; jj < udt.Rows.Count; jj++)
                            {
                                OperateResult<int[]> readInt = new OperateResult<int[]>();
                                OperateResult<string> readString = new OperateResult<string>();
                                str = udt.Rows[jj]["add"].ToString();

                                switch (udt.Rows[jj]["datatype"].ToString())
                                {
                                    case ("DInt"):
                                        readInt = await PLC_NET.ReadInt32Async(str.ToUpper(), Convert.ToInt32(udt.Rows[jj]["nul"]), 0).ConfigureAwait(false);
                                        goto readInt;
                                    case ("String"):
                                        readString = await PLC_NET.ReadStringAsync(str.ToUpper(), 50).ConfigureAwait(false);
                                        goto readString;
                                }

                                readInt:
                                if (readInt.IsSuccess)
                                {
                                    for (int i = 0; i < readInt.Content.Length; i++)
                                    {
                                        string add = str;
                                        string add_1 = Regex.Replace(str, "[0-9]", "");
                                        string add_2 = add_1.Substring(add_1.Length - 1, 1);
                                        int adds = add.LastIndexOf(add_2);
                                        string addstr = add.Substring(0, adds + 1)
                                                      + Convert.ToString(Convert.ToInt32(add.Substring(adds + 1, add.Length - adds - 1)) + (i * 4));
                                        valueRows.Add($"({PLCstationNum},'{addstr}','{readInt.Content[i]}',{j})");
                                    }
                                }
                                continue;

                                readString:
                                if (readString.IsSuccess)
                                {
                                    valueRows.Add($"({PLCstationNum},'{str}','{readString.Content.Replace("'", "''")}',{j})");
                                }
                                continue;
                            }
                        }

                        // 批量写入临时零件条码表
                        if (valueRows.Count > 0)
                        {
                            string bulkSql = "insert into fynT_TemporaryPartCode ([PLCstationNum],[PLCAddress],[Values],[workNumber]) values " + string.Join(",", valueRows);
                            hint = sqltoolsyy.GetDataTable("", bulkSql, true, out dt1);
                            if (hint.Valid == false)
                                publicAddress.AddLog($"错误：批量写入零件条码失败【{hint.Msg}】");
                        }

                        var __msPart = (DateTime.Now - __tPart).TotalMilliseconds;
                        if (__msPart > 500)
                            publicAddress.AddLog($"[scanBarCcodeAsync-读零件条码] 站{PLCstationNum} 工位{j} 耗时={__msPart:F0}ms 个数={valueRows.Count} 批量={bulkOk}");
                    }
                    else
                    {
                        // NG 分支：写判码结果、读零件条码
                        await PLC_NET.WriteInt32Async(scanAddress, Convert.ToInt16(dt1.Rows[0][2])).ConfigureAwait(false);

                        // ★ 去掉方法级截断：无条件读 NG 零件条码
                        var __tPartNg = DateTime.Now;

                        DataTable dt2 = new DataTable();
                        hint = sqltoolsyy.GetDataTable("", "delete fynT_TemporaryPartCode where PLCstationNum=" + PLCstationNum.ToString() + " and workNumber =" + j.ToString(), true, out dt2);

                        dv.RowFilter = "isnull(dataType,'')<>''and PLCstationNum=" + PLCstationNum.ToString() + " and category>=210 and category<=229 and len(category)<=4 and workNumber=" + j.ToString();
                        DataTable partAddrTableNg = dv.ToTable();

                        List<string> valueRowsNg = new List<string>();
                        bool bulkOkNg = await TryBulkReadPartCodesAsync(PLC_NET, partAddrTableNg, PLCstationNum, j, valueRowsNg).ConfigureAwait(false);

                        if (!bulkOkNg)
                        {
                            udt = GetAddPackage(partAddrTableNg);
                            for (int jj = 0; jj < udt.Rows.Count; jj++)
                            {
                                OperateResult<int[]> readInt = new OperateResult<int[]>();
                                OperateResult<string> readString = new OperateResult<string>();
                                str = udt.Rows[jj]["add"].ToString();

                                switch (udt.Rows[jj]["datatype"].ToString())
                                {
                                    case ("DInt"):
                                        readInt = await PLC_NET.ReadInt32Async(str.ToUpper(), Convert.ToInt32(udt.Rows[jj]["nul"]), 0).ConfigureAwait(false);
                                        goto readInt;
                                    case ("String"):
                                        readString = await PLC_NET.ReadStringAsync(str.ToUpper(), 50).ConfigureAwait(false);
                                        goto readString;
                                }

                                readInt:
                                if (readInt.IsSuccess)
                                {
                                    for (int i = 0; i < readInt.Content.Length; i++)
                                    {
                                        string add = str;
                                        string add_1 = Regex.Replace(str, "[0-9]", "");
                                        string add_2 = add_1.Substring(add_1.Length - 1, 1);
                                        int adds = add.LastIndexOf(add_2);
                                        string addstr = add.Substring(0, adds + 1)
                                                      + Convert.ToString(Convert.ToInt32(add.Substring(adds + 1, add.Length - adds - 1)) + (i * 4));
                                        valueRowsNg.Add($"({PLCstationNum},'{addstr}','{readInt.Content[i]}',{j})");
                                    }
                                }
                                continue;

                                readString:
                                if (readString.IsSuccess)
                                {
                                    valueRowsNg.Add($"({PLCstationNum},'{str}','{readString.Content.Replace("'", "''")}',{j})");
                                }
                                continue;
                            }
                        }

                        if (valueRowsNg.Count > 0)
                        {
                            string bulkSqlNg = "insert into fynT_TemporaryPartCode ([PLCstationNum],[PLCAddress],[Values],[workNumber]) values " + string.Join(",", valueRowsNg);
                            hint = sqltoolsyy.GetDataTable("", bulkSqlNg, true, out dt2);
                            if (hint.Valid == false)
                                publicAddress.AddLog($"错误：批量写入零件条码(NG)失败【{hint.Msg}】");
                        }

                        var __msPartNg = (DateTime.Now - __tPartNg).TotalMilliseconds;
                        if (__msPartNg > 500)
                            publicAddress.AddLog($"[scanBarCcodeAsync-读零件条码NG] 站{PLCstationNum} 工位{j} 耗时={__msPartNg:F0}ms 个数={valueRowsNg.Count} 批量={bulkOkNg}");
                    }
                }

                returns = true;
                return returns;
            }
            catch (Exception ex)
            {
                publicAddress.AddLog($"错误：scanBarCcodeAsync 异常:【{ex.Message}】");
                return returns;
            }
            finally
            {
                // 总耗时超过 2s 记录
                var __msMethod = (DateTime.Now - __tMethod).TotalMilliseconds;
                if (__msMethod > 2000)
                    publicAddress.AddLog($"[scanBarCcodeAsync-总耗时] 站{PLCstationNum} 耗时={__msMethod:F0}ms");
            }
        }

        /// <summary>
        /// 异步版本：WritePrintData（写打印数据）。
        /// 逐工序查询打印参数并写 PLC，内部调用 WriteDataAsync。
        /// </summary>
        public async Task WritePrintDataAsync(Simatic_NET PLC_NET, DataTable intOprNoXML, DataTable addressTable)
        {
            // 参数守卫
            if (PLC_NET == null || intOprNoXML == null || addressTable == null)
                return;

            if (intOprNoXML.Rows.Count == 0)
                return;

            try
            {
                SQLToolsYY sqlToolsYY = new SQLToolsYY();

                // 取站号（所有行一致）
                string plcStationNum = intOprNoXML.Rows[0]["PLCstationNum"]?.ToString();
                if (string.IsNullOrEmpty(plcStationNum))
                    return;

                // 遍历所有工序
                for (int i = 0; i < intOprNoXML.Rows.Count; i++)
                {
                    // 只处理 printCode==1 的行
                    string printCode = intOprNoXML.Rows[i]["printCode"]?.ToString();
                    if (printCode != "1") continue;

                    string workNumber = intOprNoXML.Rows[i]["workNumber"]?.ToString();
                    if (string.IsNullOrEmpty(workNumber))
                        continue;

                    string sqlStr = $@"fynP_getPrintData {plcStationNum},{workNumber}";

                    Hint hint = null;
                    DataTable dt = null;
                    try
                    {
                        dt = new DataTable();
                        hint = sqlToolsYY.GetDataTable("", sqlStr, true, out dt);
                    }
                    catch (Exception ex)
                    {
                        publicAddress.AddLog($"错误:【工序{workNumber}数据库查询异常:{ex.Message}】");
                        continue;
                    }

                    if (hint != null && hint.Valid && dt != null && dt.Rows.Count > 0)
                    {
                        try
                        {
                            // ★ 改异步版本
                            await WriteDataAsync(PLC_NET, dt).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            publicAddress.AddLog($"错误:【工序{workNumber}写入PLC失败:{ex.Message}】");
                        }
                    }
                    else if (dt != null && dt.Rows.Count == 0)
                    {
                        publicAddress.AddLog($"错误:【当前无打印参数，请联系技术员添加打印参数，工序:{workNumber}】");
                    }
                    else
                    {
                        publicAddress.AddLog($"错误:【工序{workNumber}数据库查询失败:{hint?.Msg ?? "未知错误"}】");
                    }
                }
            }
            catch (Exception ex)
            {
                publicAddress.AddLog($"WritePrintDataAsync 异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 异步版本：WriteData（批量写 PLC）。
        ///
        /// 【批量合并规则】
        ///   - String 不参与合并，立即单个写
        ///   - 相邻两行同 dataType、同 DB、offset 差 == 4，可合并
        ///   - 连续 50 个强制 flush，避免单次写太大
        ///   - 遇到不可合并 / 50 满 / 最后一行，触发 flush
        ///
        /// 【短路策略】
        ///   首个 String 失败即抛异常，避免剩余 N 个字段继续做无效重试。
        /// </summary>
        public async Task WriteDataAsync(Simatic_NET PLC_NET, DataTable dt)
        {
            if (PLC_NET == null || dt == null || dt.Rows.Count == 0)
                return;

            const int STRING_MAX_RETRY = 1;
            const int STRING_RETRY_SLEEP_MS = 200;

            try
            {
                List<int> intBuffer = new List<int>();
                List<float> floatBuffer = new List<float>();
                string batchDataType = null;   // 当前批次的数据类型
                int batchStartIndex = 0;       // 当前批次在 dt 中的起始行
                int consecutiveCount = 0;      // 当前批次已累计元素数

                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    bool isLastRow = (i == dt.Rows.Count - 1);
                    DataRow currentRow = dt.Rows[i];
                    string currentDataType = currentRow["dataType"].ToString();
                    string currentAddress = currentRow["addnum"].ToString();

                    // ---- String 特殊处理：先把已有批次 flush，再单写 ----
                    if (currentDataType == "String")
                    {
                        if (batchDataType != null && (intBuffer.Count > 0 || floatBuffer.Count > 0))
                        {
                            await FlushBatchAsync(PLC_NET, dt, batchStartIndex, batchDataType, intBuffer, floatBuffer).ConfigureAwait(false);
                            intBuffer.Clear();
                            floatBuffer.Clear();
                            batchDataType = null;
                            consecutiveCount = 0;
                        }

                        string writeAddress = currentRow["address"].ToString().ToUpper();
                        string writeValue = currentRow["sendValues"].ToString();

                        OperateResult<int> writeResult = null;
                        for (int retry = 0; retry < STRING_MAX_RETRY; retry++)
                        {
                            writeResult = await PLC_NET.WriteStringAsync(writeAddress, writeValue).ConfigureAwait(false);
                            if (writeResult != null && writeResult.IsSuccess) break;
                            if (retry < STRING_MAX_RETRY - 1)
                                await Task.Delay(STRING_RETRY_SLEEP_MS).ConfigureAwait(false);
                        }
                        if (writeResult == null || !writeResult.IsSuccess)
                        {
                            publicAddress.AddLog($"错误：异步写入String失败（地址:{writeAddress}）: {writeResult?.Message ?? "未知错误"}");
                            // ★ P1 短路：首个 String 失败即抛，避免剩余 N 个字段继续做无效重试
                            throw new Exception($"String写入失败 addr={writeAddress} msg={writeResult?.Message ?? "未知错误"}");
                        }
                        continue;
                    }

                    // ---- 判断能否与下一行合并 ----
                    bool canMergeWithNext = false;
                    if (!isLastRow)
                    {
                        DataRow nextRow = dt.Rows[i + 1];
                        string nextDataType = nextRow["dataType"].ToString();

                        if (currentDataType != "String" && currentDataType == nextDataType)
                        {
                            string[] currentParts = currentAddress.Split('.');
                            string[] nextParts = nextRow["addnum"].ToString().Split('.');

                            // 同 DB 且偏移差 4 → 可合并
                            if (currentParts.Length == 2 && nextParts.Length == 2 && currentParts[0] == nextParts[0])
                            {
                                int currentOffset = Convert.ToInt32(currentParts[1]);
                                int nextOffset = Convert.ToInt32(nextParts[1]);
                                if (nextOffset - currentOffset == 4)
                                    canMergeWithNext = true;
                            }
                        }
                    }

                    // ---- 新批次开始 ----
                    if (batchDataType == null)
                    {
                        batchDataType = currentDataType;
                        batchStartIndex = i;
                        consecutiveCount = 0;
                    }

                    // ---- 把当前值加入缓冲 ----
                    if (currentDataType == "DInt")
                    {
                        if (int.TryParse(currentRow["sendValues"].ToString(), out int intVal))
                            intBuffer.Add(intVal);
                        else
                            intBuffer.Add(0);
                    }
                    else if (currentDataType == "Real")
                    {
                        if (float.TryParse(currentRow["sendValues"].ToString(), out float floatVal))
                            floatBuffer.Add(floatVal);
                        else
                            floatBuffer.Add(0f);
                    }
                    consecutiveCount++;

                    // ---- 判断是否该 flush ----
                    bool shouldWrite = !canMergeWithNext || consecutiveCount >= 50 || isLastRow;
                    if (shouldWrite && batchDataType != null)
                    {
                        await FlushBatchAsync(PLC_NET, dt, batchStartIndex, batchDataType, intBuffer, floatBuffer).ConfigureAwait(false);
                        intBuffer.Clear();
                        floatBuffer.Clear();
                        batchDataType = null;
                        consecutiveCount = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                publicAddress.AddLog($"WriteDataAsync 异常: {ex.Message}");
                throw;   // 抛出给上层调用（WritePrintDataAsync 会 catch）
            }
        }

        /// <summary>异步版本的批量写入（DInt/Real 数组一次写）。
        /// </summary>
        private async Task FlushBatchAsync(Simatic_NET PLC_NET, DataTable dt, int startIndex, string dataType, List<int> intBuffer, List<float> floatBuffer)
        {
            if (startIndex < 0 || startIndex >= dt.Rows.Count)
                return;

            // 批次起始地址（用首行的 address）
            string startAddress = dt.Rows[startIndex]["address"].ToString().ToUpper();

            const int BATCH_MAX_RETRY = 2;
            const int BATCH_RETRY_SLEEP_MS = 200;

            if (dataType == "DInt" && intBuffer.Count > 0)
            {
                OperateResult<int> result = null;
                for (int retry = 0; retry < BATCH_MAX_RETRY; retry++)
                {
                    result = await PLC_NET.WriteInt32Async(startAddress, intBuffer.Count, intBuffer.ToArray()).ConfigureAwait(false);
                    if (result != null && result.IsSuccess) break;
                    if (retry < BATCH_MAX_RETRY - 1)
                        await Task.Delay(BATCH_RETRY_SLEEP_MS).ConfigureAwait(false);
                }
                if (result == null || !result.IsSuccess)
                {
                    publicAddress.AddLog($"错误：异步批量写入DInt失败（起始地址:{startAddress}, 数量:{intBuffer.Count}）: {result?.Message ?? "未知错误"}");
                    throw new Exception($"批量写入DInt失败 addr={startAddress} msg={result?.Message ?? "未知错误"}");
                }
            }
            else if (dataType == "Real" && floatBuffer.Count > 0)
            {
                OperateResult<int> result = null;
                for (int retry = 0; retry < BATCH_MAX_RETRY; retry++)
                {
                    result = await PLC_NET.WriteRealAsync(startAddress, floatBuffer.Count, floatBuffer.ToArray()).ConfigureAwait(false);
                    if (result != null && result.IsSuccess) break;
                    if (retry < BATCH_MAX_RETRY - 1)
                        await Task.Delay(BATCH_RETRY_SLEEP_MS).ConfigureAwait(false);
                }
                if (result == null || !result.IsSuccess)
                {
                    publicAddress.AddLog($"错误：异步批量写入Real失败（起始地址:{startAddress}, 数量:{floatBuffer.Count}）: {result?.Message ?? "未知错误"}");
                    throw new Exception($"批量写入Real失败 addr={startAddress} msg={result?.Message ?? "未知错误"}");
                }
            }
        }

        /// <summary>
        /// 异步版本：WriteParameter（参数写入）。
        ///
        /// 【流程】
        ///   1. 先执行 fynP_parameter 拿到本站计划参数
        ///   2. 遍历参数表，判断相邻行是否可合并
        ///   3. 可合并则累积到 intbeffs/realbeffs，不可合并则一次性批量写入
        ///   4. 每 50 个元素也强制 flush
        ///   5. 全部写完后写 CRC=0、信号=3
        /// </summary>
        public async Task<bool> WriteParameterAsync(Simatic_NET PLC_NET, int PLCstationNumber, int programNumber,
            DataTable addressTable, string CRC_address, string signalAddress)
        {
            try
            {
                #region 定义变量
                SQLToolsYY sqlToolsYY = new SQLToolsYY();
                bool returns = false;
                List<int> intbeffs = new List<int>();     // DInt 累积缓冲
                List<float> realbeffs = new List<float>();// Real 累积缓冲
                int[] intarray = new int[500];
                float[] realarray = new float[500];
                string add = "";
                int intnum = 0;
                float realnum = 0;
                int qty = 0;   // 当前累积的连续元素数
                #endregion

                if (CRC_address != "" && signalAddress != "")
                {
                    // 查询本站指定程序的参数明细
                    string sqlstr = "exec fynP_parameter " + PLCstationNumber.ToString() + "," + programNumber.ToString();
                    DataTable dt = new DataTable();
                    Hint hint = sqlToolsYY.GetDataTable("", sqlstr, true, out dt);

                    if (hint.Valid == false)
                    {
                        publicAddress.AddLog($"错误【{hint.Msg}】*******");
                    }
                    else
                    {
                        if (dt.Rows.Count > 0)
                        {
                            // <= 同样是为了收尾
                            for (int ii = 0; ii <= dt.Rows.Count; ii++)
                            {
                                if (ii > 0)
                                {
                                    Decimal a = 0;
                                    decimal bbb = 0;
                                    add = "";

                                    if (ii == dt.Rows.Count)
                                    {
                                        // 收尾：强制 flush
                                        a = -1;
                                        bbb = -1;
                                    }
                                    else
                                    {
                                        bbb = -1;
                                        // 非 String 才判合并
                                        if (dt.Rows[ii]["dataType"].ToString() != "String")
                                        {
                                            if (dt.Rows[ii]["dataType"].ToString() == dt.Rows[ii - 1]["dataType"].ToString())
                                            {
                                                string[] address_1 = dt.Rows[ii]["addnum"].ToString().Split('.');
                                                string[] address_2 = dt.Rows[ii - 1]["addnum"].ToString().Split('.');
                                                if (address_1[0] == address_2[0])
                                                {
                                                    a = Convert.ToInt32(address_1[1]) - Convert.ToInt32(address_2[1]);
                                                    bbb = a / 4;   // ==1 表示连续
                                                }
                                            }
                                        }
                                    }

                                    // bbb != 1：不可合并 / 收尾；或每 50 个强制 flush
                                    if (bbb != 1 || (ii % 50) == 0)
                                    {
                                        add = dt.Rows[ii - 1]["dataType"].ToString();
                                        switch (add)
                                        {
                                            case "DInt":
                                                // 当前元素先放进缓冲
                                                intnum = Convert.ToInt32(dt.Rows[ii - 1]["sendparameterValues"]);
                                                intbeffs.Add(intnum);
                                                intarray = intbeffs.ToArray();
                                                // 从"区间起始行"的地址开始批量写
                                                await PLC_NET.WriteInt32Async(
                                                    dt.Rows[ii - (qty + 1)]["address"].ToString().ToUpper(),
                                                    intarray.Length, intarray).ConfigureAwait(false);
                                                break;
                                            case "Real":
                                                realnum = float.Parse(dt.Rows[ii - 1]["sendparameterValues"].ToString());
                                                realbeffs.Add(realnum);
                                                realarray = realbeffs.ToArray();
                                                await PLC_NET.WriteRealAsync(
                                                    dt.Rows[ii - (qty + 1)]["address"].ToString().ToUpper(),
                                                    realarray.Length, realarray).ConfigureAwait(false);
                                                break;
                                            case "String":
                                                // String 单独写，不参与批量
                                                await PLC_NET.WriteStringAsync(
                                                    dt.Rows[ii - (qty + 1)]["address"].ToString().ToUpper(),
                                                    dt.Rows[ii - 1]["sendparameterValues"].ToString()).ConfigureAwait(false);
                                                break;
                                        }
                                        // 清空缓冲，重置计数
                                        intbeffs.Clear();
                                        realbeffs.Clear();
                                        qty = 0;
                                    }
                                    else
                                    {
                                        // 可合并：仅累积，不写
                                        add = dt.Rows[ii - 1]["dataType"].ToString();
                                        switch (add)
                                        {
                                            case "DInt":
                                                intnum = Convert.ToInt32(dt.Rows[ii - 1]["sendparameterValues"]);
                                                intbeffs.Add(intnum);
                                                break;
                                            case "Real":
                                                realnum = float.Parse(dt.Rows[ii - 1]["sendparameterValues"].ToString());
                                                realbeffs.Add(realnum);
                                                break;
                                        }
                                        qty++;
                                    }
                                }
                            }
                        }

                        // 全部参数写完，写 CRC=0、信号=3
                        // ★ 改动：检查两次写返回值，避免网络抖动/PLC 忙导致误判成功
                        var w1 = await PLC_NET.WriteInt32Async(CRC_address.ToUpper(), 0).ConfigureAwait(false);
                        if (w1 == null || !w1.IsSuccess)
                        {
                            publicAddress.AddLog($"[WriteParameterAsync] CRC 清零写失败 addr={CRC_address} msg={w1?.Message}");
                            return false;
                        }

                        var w2 = await PLC_NET.WriteInt32Async(signalAddress.ToUpper(), 3).ConfigureAwait(false);
                        if (w2 == null || !w2.IsSuccess)
                        {
                            publicAddress.AddLog($"[WriteParameterAsync] 信号=3 写失败 addr={signalAddress} msg={w2?.Message}");
                            return false;
                        }

                        returns = true;
                    }
                }
                else
                {
                    publicAddress.AddLog($"错误:【通讯地址获取失败!】*******");
                }
                return returns;
            }
            catch (Exception ex)
            {
                publicAddress.AddLog($"writeParameterAsync 异常:{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 异步版本：SendParameter（普通参数下发）。
        /// </summary>
        public async Task<OperateResult<int>> SendParameterAsync(Simatic_NET PLC_NET, int PLCstationNum,
            DataTable addressTable, string CRC_address, string signalAddress)
        {
            OperateResult<int> operateResult = new OperateResult<int>();
            string finishKey = "sp_" + PLCstationNum;   // 按站隔离的 finish 键

            try
            {
                #region 定义变量
                SQLToolsYY sqltoolsyy = new SQLToolsYY();
                int programNumber = 0;
                #endregion

                Hint hint;
                DataTable dt1 = new DataTable();

                // 查当前激活计划
                hint = sqltoolsyy.GetDataTable("", "select top 1 * from fynV_choicePlan where 状态=1", true, out dt1);
                if (hint.Valid == true && dt1.Rows.Count > 0)
                {
                    // 查该站当前计划对应的程序号
                    hint = sqltoolsyy.GetDataTable("", "exec fynP_getParameter " + PLCstationNum.ToString(), true, out dt1);
                    if (hint.Valid == true && dt1.Rows.Count > 0)
                    {
                        programNumber = Convert.ToInt16(dt1.Rows[0]["programNumber"]);
                        goto label;
                    }
                    else
                    {
                        // 无程序号：-1 错误
                        operateResult.Message = "该计划当前无机种参数！";
                        operateResult.Content = -1;
                        operateResult.IsSuccess = false;
                        goto finish;
                    }
                }
                else
                {
                    // 无计划：-2 错误
                    operateResult.Message = "当前无生产计划！";
                    operateResult.Content = -2;
                    operateResult.IsSuccess = false;
                    goto finish;
                }

                label:
                if (CRC_address != "" && signalAddress != "")
                {
                    // 未完成时先 writeParameter
                    if (GetSendFinish(finishKey) != 1)
                    {
                        bool writeFinish = await WriteParameterAsync(PLC_NET, PLCstationNum, programNumber,
                            addressTable, CRC_address, signalAddress).ConfigureAwait(false);
                        if (!writeFinish)
                        {
                            SetSendFinish(finishKey, 0);
                            operateResult.Message = "参数发送失败！";
                            operateResult.Content = -3;
                            operateResult.IsSuccess = false;
                            goto finish;
                        }
                        SetSendFinish(finishKey, 1);
                    }

                    // ★ 改动：固定 500ms 改为轮询，最多等 2000ms，每 200ms 读一次
                    //         只要 PLC 在 2s 内清零就判成功，避免 PLC 稍慢就误报"未反馈"
                    OperateResult<int> read_DM51998 = null;
                    var __sw = System.Diagnostics.Stopwatch.StartNew();
                    while (__sw.ElapsedMilliseconds < 2000)
                    {
                        await Task.Delay(200).ConfigureAwait(false);
                        read_DM51998 = await PLC_NET.ReadInt32Async(signalAddress.ToUpper()).ConfigureAwait(false);
                        if (read_DM51998 == null || !read_DM51998.IsSuccess) break;   // 读失败跳出，走下面失败分支
                        if (read_DM51998.Content != 3) break;                          // PLC 已处理
                    }

                    if (read_DM51998 == null || !read_DM51998.IsSuccess)
                    {
                        SetSendFinish(finishKey, 0);
                        operateResult.Message = "读取通讯状态信息失败！";
                        operateResult.Content = -5;
                        operateResult.IsSuccess = false;
                        goto finish;
                    }

                    if (read_DM51998.Content == 3)
                    {
                        // 写后未反馈
                        SetSendFinish(finishKey, 1);
                        operateResult.Message = "参数写入后未反馈！";
                        operateResult.Content = 3;
                        operateResult.IsSuccess = false;
                        goto finish;
                    }

                    SetSendFinish(finishKey, 0);
                    operateResult.Content = read_DM51998.Content;
                    operateResult.IsSuccess = true;
                }
                else
                {
                    // 记一条通讯地址错误
                    string intoerr = "insert into fynT_errorDescribeRecord (PLCstandNanme,errCode,errDescribe,relieve) values (" + PLCstationNum.ToString() + "," + 10003.ToString() + ",'通讯地址获取错误！',1)";
                    hint = sqltoolsyy.GetDataTable("", intoerr, true, out dt1);
                    operateResult.Message = "通讯地址获取错误！";
                    operateResult.Content = -4;
                    operateResult.IsSuccess = false;
                    goto finish;
                }

                finish:
                return operateResult;
            }
            catch (Exception ex)
            {
                SetSendFinish(finishKey, 0);
                publicAddress.AddLog($"sendParameterAsync 异常:站{PLCstationNum} {ex.Message}");
                operateResult.Message = "sendParameterAsync 异常：" + ex.Message;
                operateResult.Content = -99;
                operateResult.IsSuccess = false;
                return operateResult;
            }
        }

        /// <summary>
        /// 异步版本：SendCalibrationParameter（标样参数下发）。
        /// </summary>
        public async Task<OperateResult<int>> SendCalibrationParameterAsync(Simatic_NET PLC_NET, int PLCstationNum,
            DataTable addressTable, DataTable intOprNoXML, int workNumber)
        {
            #region 定义变量
            OperateResult<int> operateResult = new OperateResult<int>();
            SQLToolsYY sqltoolsyy = new SQLToolsYY();
            int programNumber = 0;
            string CRC_address = "";
            string signalAddress = "";

            // 独立 key：站号 + 工位号
            string finishKey = "scp_" + PLCstationNum + "_" + workNumber;
            #endregion

            try
            {
                Hint hint;
                DataTable dt = new DataTable();

                // 找 CRC 地址（category=04）
                DataRow[] CRC_address_datarow = addressTable.Select(
                    "PLCstationNum=" + PLCstationNum.ToString() + " and workNumber=" + workNumber + " and category='04'");
                if (CRC_address_datarow.Length == 0)
                {
                    operateResult.Message = string.Format(@"错误：PLC站号【{0}】工位号【{1}】未找到CRC校验地址(category=04)",
                        PLCstationNum.ToString(), workNumber.ToString());
                    operateResult.Content = -4;
                    operateResult.IsSuccess = false;
                    return operateResult;
                }
                CRC_address = CRC_address_datarow[0]["address"].ToString();

                // 找信号地址（category=05）
                DataRow[] signalAddress_datarow = addressTable.Select(
                    "PLCstationNum=" + PLCstationNum.ToString() + " and workNumber=" + workNumber + " and category='05'");
                if (signalAddress_datarow.Length == 0)
                {
                    operateResult.Message = string.Format(@"错误：PLC站号【{0}】工位号【{1}】未找到参数标识地址(category=05)",
                        PLCstationNum.ToString(), workNumber.ToString());
                    operateResult.Content = -4;
                    operateResult.IsSuccess = false;
                    return operateResult;
                }
                signalAddress = signalAddress_datarow[0]["address"].ToString();

                // 取标样程序号
                hint = sqltoolsyy.GetDataTable("", "fynP_getCalibrationParameter " + PLCstationNum.ToString() + "," + workNumber.ToString(), true, out dt);
                if (hint.Valid == true && dt.Rows.Count > 0)
                {
                    programNumber = Convert.ToInt16(dt.Rows[0]["programNumber"]);
                    goto label;
                }
                else
                {
                    if (!hint.Valid)
                    {
                        operateResult.Message = string.Format(@"错误：PLC站号【{0}】工位号【{1}】获取当前程序号执行数据库语句【{2}】失败，失败原因【{3}】",
                            PLCstationNum.ToString(), workNumber.ToString(),
                            "exec fynP_getParameter " + PLCstationNum.ToString() + "," + workNumber.ToString(), hint.Msg);
                        operateResult.Content = -1;
                        operateResult.IsSuccess = false;
                        return operateResult;
                    }
                    if (hint.Valid && dt.Rows.Count <= 0)
                    {
                        operateResult.Message = string.Format(@"错误：PLC站号【{0}】工位号【{1}】当前标样在本地程序库中未获取到程序号",
                            PLCstationNum.ToString(), workNumber.ToString());
                        operateResult.Content = -1;
                        operateResult.IsSuccess = false;
                        return operateResult;
                    }
                }

                label:

                // 未完成时先 writeParameter
                if (GetSendFinish(finishKey) != 1)
                {
                    bool writeFinish = await WriteParameterAsync(PLC_NET, PLCstationNum, programNumber,
                        addressTable, CRC_address, signalAddress).ConfigureAwait(false);
                    if (!writeFinish)
                    {
                        SetSendFinish(finishKey, 0);
                        operateResult.Message = string.Format(@"错误：PLC站号【{0}】工位号【{1}】参数发送失败！",
                            PLCstationNum.ToString(), workNumber.ToString());
                        operateResult.Content = -3;
                        operateResult.IsSuccess = false;
                        return operateResult;
                    }
                    SetSendFinish(finishKey, 1);
                }

                // ★ 改动：轮询替代固定 500ms 等待
                OperateResult<int> read_DM51998 = null;
                var __sw = System.Diagnostics.Stopwatch.StartNew();
                while (__sw.ElapsedMilliseconds < 2000)
                {
                    await Task.Delay(200).ConfigureAwait(false);
                    read_DM51998 = await PLC_NET.ReadInt32Async(signalAddress.ToUpper()).ConfigureAwait(false);
                    if (read_DM51998 == null || !read_DM51998.IsSuccess) break;
                    if (read_DM51998.Content != 3) break;
                }

                if (read_DM51998 == null || !read_DM51998.IsSuccess)
                {
                    SetSendFinish(finishKey, 0);
                    operateResult.Message = string.Format(@"错误：PLC站号【{0}】工位号【{1}】读取参数标识失败！",
                        PLCstationNum.ToString(), workNumber.ToString());
                    operateResult.Content = -5;
                    operateResult.IsSuccess = false;
                    return operateResult;
                }

                if (read_DM51998.Content == 3)
                {
                    SetSendFinish(finishKey, 1);
                    operateResult.Message = string.Format(@"错误：PLC站号【{0}】工位号【{1}】参数写入后未反馈！",
                        PLCstationNum.ToString(), workNumber.ToString());
                    operateResult.Content = 3;
                    operateResult.IsSuccess = false;
                    return operateResult;
                }

                SetSendFinish(finishKey, 0);
                operateResult.Content = read_DM51998.Content;
                operateResult.IsSuccess = true;
                return operateResult;
            }
            catch (Exception ex)
            {
                SetSendFinish(finishKey, 0);
                publicAddress.AddLog($"sendCalibrationParameterAsync 异常:站{PLCstationNum}工位{workNumber} {ex.Message}");
                operateResult.Message = string.Format(@"错误：PLC站号【{0}】工位号【{1}】发送参数方法执行错误【{2}】",
                    PLCstationNum.ToString(), workNumber.ToString(), ex.Message);
                operateResult.Content = -99;
                operateResult.IsSuccess = false;
                return operateResult;
            }
        }

        /// <summary>
        /// 异步版本：CalibrationSaveResults（标样检测结果保存）。
        /// </summary>
        public async Task<bool> CalibrationSaveResultsAsync(Simatic_NET PLC_NET, int PLCstationNum,
            DataTable addressTable, string ip, int port, DataTable intOprNoXML)
        {
            try
            {
                #region 定义变量
                bool returns = false;
                SQLToolsYY sqltoolsyy = new SQLToolsYY();
                DataTable udt = new DataTable("tablevalue");
                string sqlstr = "";
                Hint hint = new Hint();
                DataTable dt = new DataTable();
                DataTable dt1 = new DataTable();
                string saveAddress = "";
                int worknum = 0;
                DataTable adddt = new DataTable("address");
                DataView dv = new DataView(addressTable);   // ★ 独立视图
                string str = "";
                #endregion

                if (dv.Count > 0)
                {
                    // 过滤本站
                    dv.RowFilter = "PLCstationNum = " + PLCstationNum.ToString();
                    adddt.Clear();
                    adddt = dv.ToTable();
                    if (adddt.Rows.Count > 0)
                    {
                        worknum = Convert.ToInt32(adddt.Compute("max(workNumber)", "isnull(workNumber,0)>0"));
                        for (int j = 1; j <= worknum; j++)
                        {
                            // 取保存标志地址（category=08）
                            dv.RowFilter = "category = '08' and PLCstationNum = " + PLCstationNum.ToString() + " and workNumber = " + j.ToString();
                            adddt.Clear();
                            adddt = dv.ToTable();
                            if (adddt.Rows.Count > 0)
                            {
                                saveAddress = adddt.Rows[0]["address"].ToString();

                                // 读标志
                                OperateResult<int> saveSignal = await PLC_NET.ReadInt32Async(saveAddress).ConfigureAwait(false);
                                if (saveSignal != null && saveSignal.IsSuccess)
                                {
                                    // 值为 3 表示 PLC 请求保存
                                    if (saveSignal.Content == 3)
                                    {
                                        // 清空临时结果
                                        hint = sqltoolsyy.GetDataTable("", "delete fynT_TemporaryResult where PLCstationNum=" + PLCstationNum.ToString() + "and workNumber =" + j.ToString(), true, out dt);
                                        if (hint.Valid == true)
                                        {
                                            // 取地址表
                                            dv.RowFilter = "isnull(dataType,'')<>''and PLCstationNum=" + PLCstationNum.ToString() + " and category>=0201 and category<210 and len(category)<=4 and workNumber=" + j.ToString();
                                            udt = GetAddPackage(dv.ToTable());

                                            // 逐包读 → 写临时表
                                            for (int jj = 0; jj < udt.Rows.Count; jj++)
                                            {
                                                OperateResult<int[]> readInt = new OperateResult<int[]>();
                                                OperateResult<float[]> readReal = new OperateResult<float[]>();
                                                OperateResult<string> readString = new OperateResult<string>();
                                                str = udt.Rows[jj]["add"].ToString();
                                                sqlstr = "insert into fynT_TemporaryResult ([PLCstationNum],[PLCAddress],[parameterValues],[workNumber]) values ";
                                                switch (udt.Rows[jj]["datatype"].ToString())
                                                {
                                                    case ("DInt"):
                                                        readInt = await PLC_NET.ReadInt32Async(str.ToUpper(), Convert.ToInt32(udt.Rows[jj]["nul"]), 0).ConfigureAwait(false);
                                                        goto readInt;
                                                    case ("Real"):
                                                        readReal = await PLC_NET.ReadRealAsync(str.ToUpper(), Convert.ToInt32(udt.Rows[jj]["nul"])).ConfigureAwait(false);
                                                        goto readReal;
                                                    case ("String"):
                                                        readString = await PLC_NET.ReadStringAsync(str.ToUpper(), 50).ConfigureAwait(false);
                                                        goto readString;
                                                }
                                                continue;

                                                // ---- DInt 分支 ----
                                                readInt:
                                                if (readInt != null && readInt.IsSuccess)
                                                {
                                                    for (int i = 0; i < readInt.Content.Length; i++)
                                                    {
                                                        // 逐元素拼地址
                                                        string add = str;
                                                        string add_1 = Regex.Replace(str, "[0-9]", "");
                                                        string add_2 = add_1.Substring(add_1.Length - 1, 1);
                                                        int adds = add.LastIndexOf(add_2);
                                                        string addstr = add.Substring(0, adds + 1)
                                                                      + Convert.ToString(Convert.ToInt32(add.Substring(adds + 1, add.Length - adds - 1)) + (i * 4));
                                                        sqlstr = sqlstr + "(" + PLCstationNum.ToString() + ",'" + addstr + "','" + readInt.Content[i].ToString() + "'," + j.ToString() + ") , ";
                                                    }
                                                    hint = sqltoolsyy.GetDataTable("", sqlstr.Substring(0, sqlstr.Length - 2), true, out dt1);
                                                    if (hint.Valid == false) publicAddress.AddLog($"错误：报错信息:【{hint.Msg}】");
                                                }
                                                continue;

                                                // ---- Real 分支 ----
                                                readReal:
                                                if (readReal != null && readReal.IsSuccess)
                                                {
                                                    for (int i = 0; i < readReal.Content.Length; i++)
                                                    {
                                                        string add = str;
                                                        string add_1 = Regex.Replace(str, "[0-9]", "");
                                                        string add_2 = add_1.Substring(add_1.Length - 1, 1);
                                                        int adds = add.LastIndexOf(add_2);
                                                        string addstr = add.Substring(0, adds + 1)
                                                                      + Convert.ToString(Convert.ToInt32(add.Substring(adds + 1, add.Length - adds - 1)) + (i * 4));
                                                        sqlstr = sqlstr + "(" + PLCstationNum.ToString() + ",'" + addstr + "','" + readReal.Content[i].ToString() + "'," + j.ToString() + ") , ";
                                                    }
                                                    hint = sqltoolsyy.GetDataTable("", sqlstr.Substring(0, sqlstr.Length - 2), true, out dt1);
                                                    if (hint.Valid == false) publicAddress.AddLog($"错误：报错信息:【{hint.Msg}】");
                                                }
                                                continue;

                                                // ---- String 分支 ----
                                                readString:
                                                if (readString != null && readString.IsSuccess)
                                                {
                                                    sqlstr = sqlstr + "(" + PLCstationNum.ToString() + ",'" + str + "','" + readString.Content.ToString() + "'," + j.ToString() + ")";
                                                    hint = sqltoolsyy.GetDataTable("", sqlstr, true, out dt1);
                                                    if (hint.Valid == false) publicAddress.AddLog($"错误：报错信息:【{hint.Msg}】");
                                                }
                                                continue;
                                            }

                                            // 落正式表（标样版 SP）
                                            hint = sqltoolsyy.GetDataTable("", "exec fynP_saveCalibrationResults " + j.ToString() + "," + PLCstationNum.ToString(), true, out dt1);
                                            if (hint.Valid == true && dt1.Rows.Count > 0)
                                            {
                                                const int MAX_OUTER_LOOP = 3;
                                                const int MAX_PARAM_RETRY = 3;
                                                int outerLoop = 0;

                                                // 最多 3 轮，直到 PLC 清零
                                                while (saveSignal != null && saveSignal.IsSuccess
                                                       && saveSignal.Content == 3
                                                       && outerLoop < MAX_OUTER_LOOP)
                                                {
                                                    outerLoop++;

                                                    if (Convert.ToInt16(dt1.Rows[0][0]) == 1)
                                                    {
                                                        // 找 CRC / 信号地址
                                                        DataRow[] CRC_address_datarow = addressTable.Select(
                                                            "PLCstationNum=" + PLCstationNum.ToString() +
                                                            " and workNumber=" + j.ToString() +
                                                            " and category='04'");
                                                        DataRow[] signalAddress_datarow = addressTable.Select(
                                                            "PLCstationNum=" + PLCstationNum.ToString() +
                                                            " and workNumber=" + j.ToString() +
                                                            " and category='05'");

                                                        if (CRC_address_datarow.Length == 0 || signalAddress_datarow.Length == 0)
                                                        {
                                                            // 缺地址：写 2 表示失败，退出
                                                            await PLC_NET.WriteInt32Async(saveAddress, 2).ConfigureAwait(false);
                                                            publicAddress.AddLog($"错误：PLC站号【{PLCstationNum}】工位号【{j}】未找到CRC或信号地址（category=04/05）");
                                                            break;
                                                        }

                                                        string CRC_address = CRC_address_datarow[0]["address"].ToString();
                                                        string signalAddress = signalAddress_datarow[0]["address"].ToString();

                                                        // 发参数（最多 3 次）
                                                        bool paramSent = false;
                                                        int paramRetry = 0;

                                                        while (paramRetry < MAX_PARAM_RETRY)
                                                        {
                                                            OperateResult<int> send = await SendParameterAsync(
                                                                PLC_NET, PLCstationNum, addressTable, CRC_address, signalAddress).ConfigureAwait(false);

                                                            if (send != null && send.IsSuccess && send.Content == 1)
                                                            {
                                                                paramSent = true;
                                                                break;
                                                            }

                                                            // 不可重试的错误码直接退出
                                                            if (send != null && !send.IsSuccess
                                                                && (send.Content == -1 || send.Content == -2
                                                                    || send.Content == -3 || send.Content == -4))
                                                            {
                                                                publicAddress.AddLog($"错误：PLC站号【{PLCstationNum}】工位号【{j}】参数下发失败，代码【{send.Content}】，原因【{send.Message}】");
                                                                break;
                                                            }

                                                            paramRetry++;
                                                            if (paramRetry < MAX_PARAM_RETRY)
                                                                await Task.Delay(200).ConfigureAwait(false);
                                                        }

                                                        // 写 1（成功）/ 2（失败）到 saveAddress
                                                        if (paramSent)
                                                        {
                                                            await PLC_NET.WriteInt32Async(saveAddress, 1).ConfigureAwait(false);
                                                        }
                                                        else
                                                        {
                                                            await PLC_NET.WriteInt32Async(saveAddress, 2).ConfigureAwait(false);
                                                            publicAddress.AddLog($"错误：PLC站号【{PLCstationNum}】工位号【{j}】标样参数下发 {MAX_PARAM_RETRY} 次失败，已写失败标志");
                                                        }

                                                        break;
                                                    }
                                                    else
                                                    {
                                                        // 参数保存失败 → 写 2
                                                        await PLC_NET.WriteInt32Async(saveAddress, 2).ConfigureAwait(false);
                                                        publicAddress.AddLog($"错误：PLC站号【{PLCstationNum}】工位号【{j}】参数保存失败错误原因【{dt1.Rows[0][1]}】");
                                                        break;
                                                    }
                                                }

                                                if (outerLoop >= MAX_OUTER_LOOP)
                                                {
                                                    publicAddress.AddLog($"错误：PLC站号【{PLCstationNum}】工位号【{j}】保存循环超过 {MAX_OUTER_LOOP} 次仍未结束，强制退出");
                                                }
                                            }
                                            else
                                            {
                                                // 落正式表失败
                                                if (hint.Valid != true)
                                                {
                                                    publicAddress.AddLog($"错误：PLC站号【{PLCstationNum}】工位号【{j}】执行数据库语句【exec fynP_saveResults {j},{PLCstationNum}】失败，失败原因【{hint.Msg}】");
                                                }
                                                if (dt1.Rows.Count <= 0)
                                                {
                                                    publicAddress.AddLog($"错误：PLC站号【{PLCstationNum}】工位号【{j}】执行数据库语句【exec fynP_saveResults {j},{PLCstationNum}】成功，但语句无返回值");
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    returns = true;
                }
                return returns;
            }
            catch (Exception ex)
            {
                publicAddress.AddLog($"错误：calibrationSaveResultsAsync 报错信息:【{ex.Message}】");
                return false;
            }
        }

        /// <summary>
        /// 异步版本：CalibrationScanBarCcode（标样条码扫描）。
        /// </summary>
        public async Task<bool> CalibrationScanBarCcodeAsync(Simatic_NET PLC_NET, int PLCstationNum,
            DataTable addressTable, DataTable intOprNoXML)
        {
            bool returns = false;
            try
            {
                #region 定义变量
                SQLToolsYY sqltoolsyy = new SQLToolsYY();
                DataTable udt = new DataTable("tablevalue");
                string intostring = "";
                Hint hint = new Hint();
                DataTable dt = new DataTable();
                DataTable dt1 = new DataTable();
                string scanAddress = "";
                int worknum = 0;
                DataTable adddt = new DataTable("address");
                DataView dv = new DataView(addressTable);   // ★ 独立视图
                string str;
                #endregion

                if (dv.Count > 0)
                {
                    dv.RowFilter = "PLCstationNum = " + PLCstationNum.ToString();
                    adddt.Clear();
                    adddt = dv.ToTable();
                    if (adddt.Rows.Count > 0)
                    {
                        worknum = Convert.ToInt32(adddt.Compute("max(workNumber)", "isnull(workNumber,0)>0"));
                        for (int j = 1; j <= worknum; j++)
                        {
                            // 找扫码触发地址（category=19）
                            dv.RowFilter = "category = '19' and PLCstationNum = " + PLCstationNum.ToString() + " and workNumber = " + j.ToString();
                            adddt.Clear();
                            adddt = dv.ToTable();
                            if (adddt.Rows.Count > 0)
                            {
                                scanAddress = adddt.Rows[0]["address"].ToString();
                                OperateResult<int> saveSignal = await PLC_NET.ReadInt32Async(scanAddress).ConfigureAwait(false);
                                if (saveSignal != null && saveSignal.IsSuccess)
                                {
                                    if (saveSignal.Content == 3)
                                    {
                                        // 清空临时条码表
                                        hint = sqltoolsyy.GetDataTable("", "delete fynT_TemporaryBarCode where PLCstationNum=" + PLCstationNum.ToString() + "and workNumber =" + j.ToString(), true, out dt);
                                        if (hint.Valid == true)
                                        {
                                            // 取扫码地址集合（category=0201）
                                            dv.RowFilter = "category = '0201' and PLCstationNum = " + PLCstationNum.ToString() + " and workNumber = " + j.ToString();
                                            udt = GetAddPackage(dv.ToTable());

                                            // 读条码并写临时表
                                            for (int jj = 0; jj < udt.Rows.Count; jj++)
                                            {
                                                OperateResult<int[]> readInt = new OperateResult<int[]>();
                                                OperateResult<string> readString = new OperateResult<string>();
                                                str = udt.Rows[jj]["add"].ToString();
                                                intostring = "insert into fynT_TemporaryBarCode ([PLCstationNum],[PLCAddress],[Values],[workNumber]) values ";
                                                switch (udt.Rows[jj]["datatype"].ToString())
                                                {
                                                    case ("DInt"):
                                                        readInt = await PLC_NET.ReadInt32Async(str.ToUpper(), Convert.ToInt32(udt.Rows[jj]["nul"]), 0).ConfigureAwait(false);
                                                        goto readInt;
                                                    case ("String"):
                                                        readString = await PLC_NET.ReadStringAsync(str.ToUpper(), 50).ConfigureAwait(false);
                                                        goto readString;
                                                }
                                                readInt:
                                                if (readInt != null && readInt.IsSuccess)
                                                {
                                                    for (int i = 0; i < readInt.Content.Length; i++)
                                                    {
                                                        // 逐元素拼地址
                                                        string add = str;
                                                        string add_1 = Regex.Replace(str, "[0-9]", "");
                                                        string add_2 = add_1.Substring(add_1.Length - 1, 1);
                                                        int adds = add.LastIndexOf(add_2);
                                                        string addstr = add.Substring(0, adds + 1)
                                                                      + Convert.ToString(Convert.ToInt32(add.Substring(adds + 1, add.Length - adds - 1)) + (i * 4));
                                                        intostring = intostring + "(" + PLCstationNum.ToString() + ",'" + addstr + "','" + readInt.Content[i].ToString() + "'," + j.ToString() + ") , ";
                                                    }
                                                    hint = sqltoolsyy.GetDataTable("", intostring.Substring(0, intostring.Length - 2), true, out dt1);
                                                    if (hint.Valid == false) publicAddress.AddLog($"错误【{hint.Msg}】*******");
                                                }
                                                continue;
                                                readString:
                                                if (readString != null && readString.IsSuccess)
                                                {
                                                    intostring = intostring + "(" + PLCstationNum.ToString() + ",'" + str + "','" + readString.Content.ToString() + "'," + j.ToString() + ")";
                                                    hint = sqltoolsyy.GetDataTable("", intostring, true, out dt1);
                                                    if (hint.Valid == false) publicAddress.AddLog($"错误【{hint.Msg}】*******");
                                                }
                                                continue;
                                            }

                                            // 判码（标样版 SP）
                                            hint = sqltoolsyy.GetDataTable("", "exec fynP_judgeCalibrationBarCode " + j.ToString() + "," + PLCstationNum.ToString(), true, out dt1);
                                            if (hint.Valid == true && dt1.Rows.Count > 0)
                                            {
                                                if (Convert.ToInt16(dt1.Rows[0][2]) == 1)
                                                {
                                                    // 判码成功，发标样参数（最多 3 次）
                                                    const int MAX_PARAM_RETRY = 3;
                                                    bool paramSent = false;
                                                    int paramRetry = 0;

                                                    while (paramRetry < MAX_PARAM_RETRY)
                                                    {
                                                        OperateResult<int> send = await SendCalibrationParameterAsync(
                                                            PLC_NET, PLCstationNum, addressTable, intOprNoXML, j).ConfigureAwait(false);

                                                        if (send != null && send.IsSuccess && send.Content == 1)
                                                        {
                                                            paramSent = true;
                                                            break;
                                                        }

                                                        // 不可重试的错误码
                                                        if (send != null && !send.IsSuccess
                                                            && (send.Content == -1 || send.Content == -2
                                                                || send.Content == -3 || send.Content == -4))
                                                        {
                                                            publicAddress.AddLog($"错误：PLC站号【{PLCstationNum}】工位号【{j}】标样参数下发失败，代码【{send.Content}】");
                                                            break;
                                                        }

                                                        paramRetry++;
                                                        if (paramRetry < MAX_PARAM_RETRY)
                                                            await Task.Delay(200).ConfigureAwait(false);
                                                    }

                                                    if (paramSent)
                                                    {
                                                        // 写判码结果（1）
                                                        await PLC_NET.WriteInt32Async(scanAddress, Convert.ToInt16(dt1.Rows[0][2])).ConfigureAwait(false);
                                                    }
                                                    else
                                                    {
                                                        // 失败写 12
                                                        await PLC_NET.WriteInt32Async(scanAddress, 12).ConfigureAwait(false);
                                                        publicAddress.AddLog($"错误：PLC站号【{PLCstationNum}】;工位号【{j}】标样条码扫描，下发标样参数 {MAX_PARAM_RETRY} 次失败！");
                                                    }
                                                }
                                                else
                                                {
                                                    // 判码失败，写原值
                                                    await PLC_NET.WriteInt32Async(scanAddress, Convert.ToInt16(dt1.Rows[0][2])).ConfigureAwait(false);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    returns = true;
                }
                return returns;
            }
            catch (Exception ex)
            {
                publicAddress.AddLog($"错误：calibrationScanBarCcodeAsync 异常:【{ex.Message}】");
                return returns;
            }
        }

        /// <summary>
        /// 起始工序写入条码信息（异步版）。
        ///
        /// 【参数】
        ///   ignoreStartFilter = false 时只处理 start=1 的行；
        ///   ignoreStartFilter = true 时处理所有行（计划写入时用）。
        /// </summary>
        public async Task WriteBarCodeAsync(Simatic_NET plcNet, DataTable intOprNoXML,
            DataTable addressTable, bool ignoreStartFilter = false)
        {
            /******* 显式声明：addressTable 当前为预留参数，有意不使用（避免未使用警告）********
                    _ = addressTable;
                    │       │
                    │       └── 右侧：你要"处理"的值（这里是方法参数）
                    └────────── 左侧：discard 运算符（只写一个下划线）
            *************************************************************************/
            _ = addressTable;
            const string flagStart = "1";
            try
            {
                var sqlTools = new SQLToolsYY();

                // 遍历所有工序行
                foreach (DataRow row in intOprNoXML.Rows)
                {
                    // 不忽略过滤器时，只处理 start==1 的行
                    if (!ignoreStartFilter && row["start"].ToString() != flagStart)
                        continue;

                    string stationNum = row["PLCstationNum"].ToString();
                    string workNumber = row["workNumber"].ToString();
                    string sql = $"exec fynP_getMaxOrderNum {stationNum}, {workNumber}";

                    // 查询要写入的条码数据
                    Hint hint = sqlTools.GetDataTable("", sql, true, out DataTable dt);
                    if (hint.Valid)
                    {
                        if (dt.Rows.Count > 0)
                            await WriteDataAsync(plcNet, dt).ConfigureAwait(false);
                    }
                    else
                    {
                        publicAddress.AddLog($"错误：PLC站号【{stationNum}】工位号【{workNumber}】执行数据库语句【{sql}】失败原因【{hint.Msg}】！");
                    }
                }
            }
            catch (Exception ex)
            {
                publicAddress.AddLog($"writeBarCodeAsync 异常：{ex.Message}");
            }
        }

        /// <summary>
        /// 写入计划数、投产数（异步版）。
        /// 起始工序（start=1）或结束工序（tagEnd=1）都写。
        /// </summary>
        public async Task WritePlanBatchChargeQtyAsync(Simatic_NET PLC_NET, DataTable intOprNoXML, DataTable addressTable)
        {
            try
            {
                SQLToolsYY sqlToolsYY = new SQLToolsYY();
                for (int i = 0; i < intOprNoXML.Rows.Count; i++)
                {
                    // 起始工序或结束工序都写
                    if (intOprNoXML.Rows[i]["start"].ToString() == "1" || intOprNoXML.Rows[i]["tagEnd"].ToString() == "1")
                    {
                        string sqlStr = @"exec fynP_getPlanBatchChargeQty " + intOprNoXML.Rows[i]["PLCstationNum"].ToString()
                                      + " ," + intOprNoXML.Rows[i]["workNumber"].ToString();
                        Hint hint = sqlToolsYY.GetDataTable("", sqlStr, true, out DataTable dt);
                        await WriteDataAsync(PLC_NET, dt).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                publicAddress.AddLog($"writePlanBatchChargeQtyAsync 异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 报警信息保存（异步版）。
        ///
        /// 【流程】
        ///   1. 过滤出本站的地址表
        ///   2. 计算最大工位号
        ///   3. 逐工位封包（category 03 开头）
        ///   4. 逐包读 PLC → 逐元素写 fynP_saveErrorValuv
        ///
        /// 【本次改动】
        ///   - 去掉无意义的 addressTable.Copy()（Copy 后立即被丢弃）
        ///   - 用独立 DataView(addressTable) 过滤，不污染共享地址表的 DefaultView
        /// </summary>
        public async Task<bool> SaveErrorAsync(Simatic_NET PLC_NET, int PLCstationNum, DataTable addressTable, string ip, int prot)
        {
            try
            {
                #region 定义变量
                bool returns = false;
                SQLToolsYY sqltoolsyy = new SQLToolsYY();
                DataTable udt = new DataTable("tablevalue");
                DataTable dt1 = new DataTable();
                int work = 0;
                int value = 0;
                int worknum = 0;
                DataTable dt = new DataTable();
                Hint hint;
                string addstr;
                #endregion

                if (addressTable == null || addressTable.Rows.Count == 0)
                    return returns;

                // ★ 独立 DataView，按本站过滤，不污染 addressTable.DefaultView
                DataView dv = new DataView(addressTable);
                dv.RowFilter = "PLCstationNum = " + PLCstationNum.ToString();
                DataTable adddt = dv.ToTable();

                if (adddt.Rows.Count == 0)
                {
                    // 本站无地址配置，直接返回
                    return true;
                }

                // 最大工位号
                worknum = Convert.ToInt32(adddt.Compute("max(workNumber)", "isnull(workNumber,0)>0"));

                for (int j = 1; j <= worknum; j++)
                {
                    // 封包：本站 + 本工位 + category 03
                    udt = GetAddPackage(PLCstationNum, j, "03", addressTable);

                    for (int i = 0; i < udt.Rows.Count; i++)
                    {
                        // 批量读改为异步，count 参数显式传 0 走 Array 超时
                        OperateResult<int[]> read = await PLC_NET.ReadInt32Async(
                            udt.Rows[i]["add"].ToString(),
                            Convert.ToInt32(udt.Rows[i]["nul"]), 0).ConfigureAwait(false);

                        if (read != null && read.IsSuccess)
                        {
                            for (int a = 0; a < read.Content.Length; a++)
                            {
                                // 计算第 a 个元素的地址
                                string add = udt.Rows[i]["add"].ToString();
                                string add_1 = Regex.Replace(udt.Rows[i]["add"].ToString(), "[0-9]", "");
                                string add_2 = add_1.Substring(add_1.Length - 1, 1);
                                int adds = add.LastIndexOf(add_2);
                                addstr = add.Substring(0, adds + 1)
                                       + Convert.ToString(Convert.ToInt32(add.Substring(adds + 1, add.Length - adds - 1)) + (a * 4));

                                value = read.Content[a];
                                work = Convert.ToInt32(udt.Rows[i]["work"]);

                                // 写数据库（每个元素一条）
                                hint = sqltoolsyy.GetDataTable("", "exec fynP_saveErrorValuv " + PLCstationNum.ToString() + ",'" + addstr + "'," + value.ToString() + "," + work.ToString(), true, out dt1);
                            }
                        }
                    }
                }

                returns = true;
                return returns;
            }
            catch (Exception ex)
            {
                publicAddress.AddLog($"saveErrorAsync 异常:站{PLCstationNum} {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 刷卡（异步版）。
        ///
        /// 【流程】
        ///   读刷卡触发标志（category=38）：
        ///     - 1/2：卡号登录
        ///     - 3：用户名 + 密码登录
        ///   读卡号/用户名/密码 → 查权限 → 回写工号 + 权限位 → 清标志
        /// </summary>
        public async Task SwipingCardAsync(Simatic_NET PLC_NET, DataTable intOprNoXML, DataTable addressTable)
        {
            try
            {
                #region 定义变量
                SQLToolsYY sqlToolsYY = new SQLToolsYY();
                Hint hint = new Hint();
                DataTable dt = new DataTable();
                string sqlStr = "";
                string jurisdictionBin = "";
                string cardNumber = "";
                string strCard = "";
                string jobNumber = "";
                string user = "";
                string password = "";
                string userTagAddress = "";
                string CardAddress = "";
                string jobNumberAddress = "";
                string userAddress = "";
                string passwordAssress = "";
                string intOprNo = "";
                string jurisdictionEncoding = "PZP_1___";
                string jurisdictionAddress = "";
                int userTag;
                #endregion

                // 由 lineNumber 推导权限编码前缀
                string jurisdictionEncoding2 = Regex.IsMatch(publicAddress.lineNumber.Substring(3, 2), @"^[0-9]+$") == true
                    ? Convert.ToInt32(publicAddress.lineNumber.Substring(3, 2)).ToString()
                    : publicAddress.lineNumber.Substring(3, 2);
                jurisdictionEncoding = publicAddress.lineNumber.Substring(0, 3) + "_" + jurisdictionEncoding2 + "___";

                for (int i = 0; i < intOprNoXML.Rows.Count; i++)
                {
                    intOprNo = intOprNoXML.Rows[i]["intOprNo"].ToString();

                    // 读刷卡触发标志（category=38）
                    DataRow[] datarow = addressTable.Select("category='38' and workNumber='" + intOprNoXML.Rows[i]["workNumber"].ToString() + "' and  PLCstationNum ='" + intOprNoXML.Rows[i]["PLCstationNum"].ToString() + "'");
                    userTagAddress = datarow[0]["address"].ToString();

                    // ★ 链式调用拆开，避免 NRE
                    var userTagResult = await PLC_NET.ReadInt32Async(userTagAddress.ToUpper()).ConfigureAwait(false);
                    userTag = userTagResult?.Content ?? 0;

                    switch (userTag)
                    {
                        case 1:   // 卡号登录
                        case 2:   // 卡号登录（无工号回写）
                            // 找卡号 / 工号 / 权限地址
                            datarow = addressTable.Select("category='17' and workNumber='" + intOprNoXML.Rows[i]["workNumber"].ToString() + "' and  PLCstationNum ='" + intOprNoXML.Rows[i]["PLCstationNum"].ToString() + "'");
                            CardAddress = datarow[0]["address"].ToString();
                            datarow = addressTable.Select("category='18' and workNumber='" + intOprNoXML.Rows[i]["workNumber"].ToString() + "' and  PLCstationNum ='" + intOprNoXML.Rows[i]["PLCstationNum"].ToString() + "'");
                            jobNumberAddress = datarow[0]["address"].ToString();
                            datarow = addressTable.Select("category='21' and workNumber='" + intOprNoXML.Rows[i]["workNumber"].ToString() + "' and  PLCstationNum ='" + intOprNoXML.Rows[i]["PLCstationNum"].ToString() + "'");
                            jurisdictionAddress = datarow[0]["address"].ToString();

                            // ★ 读卡号改为异步
                            var cardResult = await PLC_NET.ReadStringAsync(CardAddress.ToUpper(), 15).ConfigureAwait(false);
                            strCard = cardResult?.Content ?? "";
                            cardNumber = strCard.Replace("\r\n", "");

                            if (cardNumber != "")
                            {
                                // 查权限
                                sqlStr = "SELECT * FROM fynV_getUserRole where card='" + cardNumber + "' and PATINDEX('" + jurisdictionEncoding + intOprNo + "', jurisdictionEncoding )>0 order by jurisdictionEncoding desc";
                                hint = sqlToolsYY.GetDataTable("", sqlStr, true, out dt);
                                if (hint.Valid && dt.Rows.Count > 0)
                                {
                                    jobNumber = dt.Rows[0]["jobNumber"].ToString();
                                    // userTag==1 时回写工号
                                    if (userTag == 1)
                                        await PLC_NET.WriteInt32Async(jobNumberAddress.ToUpper(), Convert.ToInt32(jobNumber)).ConfigureAwait(false);

                                    // 拼接二进制权限串（多角色 → 拼串）
                                    for (int j = 0; j < dt.Rows.Count; j++)
                                    {
                                        jurisdictionBin = jurisdictionBin + Convert.ToInt16(dt.Rows[j]["jurisdiction"]).ToString();
                                    }
                                    await PLC_NET.WriteInt32Async(jurisdictionAddress.ToUpper(), Convert.ToInt32(jurisdictionBin, 2)).ConfigureAwait(false);

                                    // 记登录日志
                                    sqlStr = @"exec fynP_saveUserLoginLog '" + jobNumber + "'," + Convert.ToInt32(jurisdictionBin, 2).ToString() + ",'" + userTag.ToString() + "'," + intOprNoXML.Rows[i]["PLCstationNum"].ToString() + "," + intOprNoXML.Rows[i]["workNumber"].ToString();
                                    hint = sqlToolsYY.GetDataTable("", sqlStr, true, out dt);
                                }
                                else
                                {
                                    // 无效卡：清零
                                    if (userTag == 1)
                                        await PLC_NET.WriteInt32Async(jobNumberAddress.ToUpper(), 0).ConfigureAwait(false);
                                    await PLC_NET.WriteInt32Async(jurisdictionAddress.ToUpper(), 0).ConfigureAwait(false);
                                }
                            }
                            else
                            {
                                // 空卡号：清零
                                if (userTag == 1)
                                    await PLC_NET.WriteInt32Async(jobNumberAddress.ToUpper(), 0).ConfigureAwait(false);
                                await PLC_NET.WriteInt32Async(jurisdictionAddress.ToUpper(), 0).ConfigureAwait(false);
                            }
                            break;

                        case 3:   // 用户名+密码登录
                            datarow = addressTable.Select("category='36' and workNumber='" + intOprNoXML.Rows[i]["workNumber"].ToString() + "' and  PLCstationNum ='" + intOprNoXML.Rows[i]["PLCstationNum"].ToString() + "'");
                            userAddress = datarow[0]["address"].ToString();
                            datarow = addressTable.Select("category='37' and workNumber='" + intOprNoXML.Rows[i]["workNumber"].ToString() + "' and  PLCstationNum ='" + intOprNoXML.Rows[i]["PLCstationNum"].ToString() + "'");
                            passwordAssress = datarow[0]["address"].ToString();
                            datarow = addressTable.Select("category='18' and workNumber='" + intOprNoXML.Rows[i]["workNumber"].ToString() + "' and  PLCstationNum ='" + intOprNoXML.Rows[i]["PLCstationNum"].ToString() + "'");
                            jobNumberAddress = datarow[0]["address"].ToString();
                            datarow = addressTable.Select("category='21' and workNumber='" + intOprNoXML.Rows[i]["workNumber"].ToString() + "' and  PLCstationNum ='" + intOprNoXML.Rows[i]["PLCstationNum"].ToString() + "'");
                            jurisdictionAddress = datarow[0]["address"].ToString();

                            // ★ 读用户名 / 密码改为异步
                            var userResult = await PLC_NET.ReadStringAsync(userAddress.ToUpper(), 5).ConfigureAwait(false);
                            var pwdResult = await PLC_NET.ReadStringAsync(passwordAssress.ToUpper(), 10).ConfigureAwait(false);
                            user = userResult?.Content ?? "";
                            password = pwdResult?.Content ?? "";

                            if (user != "")
                            {
                                // 按用户名+密码+权限查
                                sqlStr = "SELECT * FROM fynV_getUserRole where jobNumber='" + user + "' and  password='" + password + "' and PATINDEX('" + jurisdictionEncoding + intOprNo + "', jurisdictionEncoding )>0 order by jurisdictionEncoding desc";
                                hint = sqlToolsYY.GetDataTable("", sqlStr, true, out dt);
                                if (hint.Valid && dt.Rows.Count > 0)
                                {
                                    jobNumber = dt.Rows[0]["jobNumber"].ToString();
                                    await PLC_NET.WriteInt32Async(jobNumberAddress.ToUpper(), Convert.ToInt32(jobNumber)).ConfigureAwait(false);
                                    // 拼权限位
                                    for (int j = 0; j < dt.Rows.Count; j++)
                                    {
                                        jurisdictionBin = jurisdictionBin + Convert.ToInt16(dt.Rows[j]["jurisdiction"]).ToString();
                                    }
                                    await PLC_NET.WriteInt32Async(jurisdictionAddress.ToUpper(), Convert.ToInt32(jurisdictionBin, 2)).ConfigureAwait(false);
                                    // 登录日志
                                    sqlStr = @"exec fynP_saveUserLoginLog '" + jobNumber + "'," + Convert.ToInt32(jurisdictionBin, 2).ToString() + ",'" + userTag.ToString() + "'," + intOprNoXML.Rows[i]["PLCstationNum"].ToString() + "," + intOprNoXML.Rows[i]["workNumber"].ToString();
                                    hint = sqlToolsYY.GetDataTable("", sqlStr, true, out dt);
                                }
                                else
                                {
                                    // 验证失败：清零
                                    await PLC_NET.WriteInt32Async(jobNumberAddress.ToUpper(), 0).ConfigureAwait(false);
                                    await PLC_NET.WriteInt32Async(jurisdictionAddress.ToUpper(), 0).ConfigureAwait(false);
                                }
                            }
                            else
                            {
                                // 空用户名：清零
                                await PLC_NET.WriteInt32Async(jobNumberAddress.ToUpper(), 0).ConfigureAwait(false);
                                await PLC_NET.WriteInt32Async(jurisdictionAddress.ToUpper(), 0).ConfigureAwait(false);
                            }
                            break;
                    }

                    // 清刷卡标志
                    await PLC_NET.WriteInt32Async(userTagAddress.ToUpper(), 0).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                publicAddress.AddLog($"swipingCardAsync 异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 写入零件ID、物料编码（异步版）。
        /// 按工艺参数顺序回填到 PLC 的 category 2401/2501 地址。
        /// </summary>
        public async Task WritePartIDAsync(Simatic_NET PLC_NET, DataTable intOprNoXML, DataTable addressTable)
        {
            try
            {
                Hint hint = new Hint();
                SQLToolsYY sqlToolsYY = new SQLToolsYY();
                DataTable dt = new DataTable();
                for (int i = 0; i < intOprNoXML.Rows.Count; i++)
                {
                    // 查零件
                    hint = sqlToolsYY.GetDataTable("", "select * from fynV_partid where intOprNo='" + intOprNoXML.Rows[i]["intOprNo"].ToString() + "'", true, out dt);
                    DataView dv1 = new DataView(addressTable);   // ★ 改动1：独立视图

                    // 取 partcinvcode 地址（category=2401）
                    dv1.RowFilter = "category='2401' and workNumber='" + intOprNoXML.Rows[i]["workNumber"].ToString() + "' and  PLCstationNum ='" + intOprNoXML.Rows[i]["PLCstationNum"].ToString() + "'";
                    dv1.Sort = "ID ASC";
                    DataTable partcinvcodeTable = dv1.ToTable();
                    partcinvcodeTable.Columns.Add("sendValues", typeof(string));

                    // 取 partid 地址（category=2501）
                    dv1.RowFilter = "category='2501' and workNumber='" + intOprNoXML.Rows[i]["workNumber"].ToString() + "' and  PLCstationNum ='" + intOprNoXML.Rows[i]["PLCstationNum"].ToString() + "'";
                    dv1.Sort = "ID ASC";
                    DataTable partidTable = dv1.ToTable();
                    partidTable.Columns.Add("sendValues", typeof(string));

                    // 按顺序填值
                    for (int j = 0; j < dt.Rows.Count; j++)
                    {
                        partcinvcodeTable.Rows[j]["sendValues"] = dt.Rows[j]["partcinvcode"].ToString();
                        partidTable.Rows[j]["sendValues"] = dt.Rows[j]["partid"].ToString();
                    }

                    // ★ 改动2：两个 WriteDataAsync 各包一层 try-catch，避免一个失败阻断整个 for 循环
                    try
                    {
                        await WriteDataAsync(PLC_NET, partidTable).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        publicAddress.AddLog(
                            $"工位{intOprNoXML.Rows[i]["workNumber"]} 写 partid 失败: {ex.Message}");
                    }

                    try
                    {
                        await WriteDataAsync(PLC_NET, partcinvcodeTable).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        publicAddress.AddLog(
                            $"工位{intOprNoXML.Rows[i]["workNumber"]} 写 partcinvcode 失败: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                publicAddress.AddLog($"writePartIDAsync 异常: {ex.Message}");
            }
        }

        /// <summary>写入工序号（异步版）。
        /// </summary>
        public async Task WriteProcessRouteAsync(Simatic_NET PLC_NET, DataTable intOprNoXML, DataTable addressTable, string cinvcode)
        {
            try
            {
                Hint hint = new Hint();
                SQLToolsYY sqlToolsYY = new SQLToolsYY();
                DataTable dt = new DataTable();
                string sqlStr = "";
                for (int i = 0; i < intOprNoXML.Rows.Count; i++)
                {
                    // 查工艺路线
                    sqlStr = @"select * from fynV_getProcessRoute where PLCstationNum=" + intOprNoXML.Rows[i]["PLCstationNum"].ToString() + " and workNumber=" + intOprNoXML.Rows[i]["workNumber"].ToString() + " and cinvcode='" + cinvcode + "' ";
                    hint = sqlToolsYY.GetDataTable("", sqlStr, true, out dt);
                    if (hint.Valid)
                    {
                        if (dt.Rows.Count > 0)
                            await WriteDataAsync(PLC_NET, dt).ConfigureAwait(false);
                    }
                    else
                    {
                        publicAddress.AddLog($"错误【{hint.Msg}】");
                    }
                }
            }
            catch (Exception ex)
            {
                publicAddress.AddLog($"writeProcessRouteAsync 异常: {ex.Message}");
            }
        }
        #endregion


        #region 批量读优化（私有辅助）
        /// <summary>
        /// 辅助方法：批量读 String（异步版，用于 saveResultsAsync）。
        ///
        /// 【成功条件】
        ///   - 至少 2 个 String
        ///   - 同一 DB
        ///   - 偏移严格等步长（stride）
        ///   - stride*count <= 2000（PDU 上限）
        /// 返回字典：key = address.ToUpper()，value = 读到的字符串。
        /// 任一条件不满足都返回空字典，调用方会自动回退到单个读。
        /// </summary>
        private async Task<Dictionary<string, string>> TryBulkReadStringsInResultsAsync(
            Simatic_NET plc,
            DataTable addressData,
            DataView dv)
        {
            var result = new Dictionary<string, string>();

            try
            {
                if (addressData == null || addressData.Rows.Count == 0) return result;

                // 独立 DataView 过滤出 String（不复制原表，也不污染原表 DefaultView）
                DataView sv = new DataView(addressData);
                sv.RowFilter = "dataType='String'";
                DataTable onlyStringTable = sv.ToTable();

                // 少于 2 个 String 没有批量收益
                if (onlyStringTable.Rows.Count < 2) return result;

                int dbNo = -1;
                int firstOffset = -1;
                int stride = -1;

                // 校验：同 DB、等步长
                for (int k = 0; k < onlyStringTable.Rows.Count; k++)
                {
                    // 地址拆成 DB.offset
                    string[] parts = onlyStringTable.Rows[k]["addnum"].ToString().Split('.');
                    if (parts.Length != 2) return result;

                    int curDb = Convert.ToInt32(parts[0]);
                    int curOff = Convert.ToInt32(parts[1]);

                    if (k == 0)
                    {
                        // 第一行：记录 DB 号和起始偏移
                        dbNo = curDb;
                        firstOffset = curOff;
                    }
                    else
                    {
                        // DB 号必须一致
                        if (curDb != dbNo) return result;

                        if (k == 1)
                        {
                            // 第二行：确定步长
                            stride = curOff - firstOffset;
                            if (stride < 2) return result;
                        }
                        else
                        {
                            // 第三行及以后：每行步长必须一致
                            int prevOff = Convert.ToInt32(onlyStringTable.Rows[k - 1]["addnum"].ToString().Split('.')[1]);
                            if (curOff - prevOff != stride) return result;
                        }
                    }
                }

                // PDU 上限保护
                if (stride <= 0 || stride * onlyStringTable.Rows.Count > 2000) return result;

                // ★ 异步批量读
                var batchResult = await plc.ReadStringsBatchAsync(dbNo, firstOffset, stride, onlyStringTable.Rows.Count).ConfigureAwait(false);

                if (batchResult == null || !batchResult.IsSuccess || batchResult.Content == null)
                {
                    publicAddress.AddLog($"[saveResultsAsync-批量读String失败] msg={batchResult?.Message}，回退到单个读");
                    return result;
                }

                // 按地址填入字典
                for (int k = 0; k < batchResult.Content.Length && k < onlyStringTable.Rows.Count; k++)
                {
                    string addr = onlyStringTable.Rows[k]["address"].ToString().ToUpper();
                    result[addr] = batchResult.Content[k] ?? "";
                }
                return result;
            }
            catch (Exception ex)
            {
                publicAddress.AddLog($"[saveResultsAsync-批量读String异常] {ex.Message}");
                return result;
            }
        }

        /// <summary>辅助方法：批量读零件条码（异步版，用于 scanBarCcodeAsync）。
        /// </summary>
        private async Task<bool> TryBulkReadPartCodesAsync(
            Simatic_NET plc,
            DataTable partAddrTable,
            int plcStationNum,
            int workNumber,
            List<string> valueRows)
        {
            try
            {
                if (partAddrTable == null || partAddrTable.Rows.Count == 0) return false;

                // 必须全是 String
                for (int k = 0; k < partAddrTable.Rows.Count; k++)
                {
                    if (partAddrTable.Rows[k]["dataType"].ToString() != "String") return false;
                }

                int dbNo = -1;
                int firstOffset = -1;
                int stride = -1;

                // 校验：同 DB、等步长
                for (int k = 0; k < partAddrTable.Rows.Count; k++)
                {
                    string[] parts = partAddrTable.Rows[k]["addnum"].ToString().Split('.');
                    if (parts.Length != 2) return false;

                    int curDb = Convert.ToInt32(parts[0]);
                    int curOff = Convert.ToInt32(parts[1]);

                    if (k == 0)
                    {
                        dbNo = curDb;
                        firstOffset = curOff;
                    }
                    else
                    {
                        if (curDb != dbNo) return false;

                        if (k == 1)
                        {
                            stride = curOff - firstOffset;
                            if (stride < 2) return false;
                        }
                        else
                        {
                            int prevOff = Convert.ToInt32(partAddrTable.Rows[k - 1]["addnum"].ToString().Split('.')[1]);
                            if (curOff - prevOff != stride) return false;
                        }
                    }
                }

                // PDU 上限保护
                if (stride <= 0 || stride * partAddrTable.Rows.Count > 2000) return false;

                // ★ 异步批量读
                var batchResult = await plc.ReadStringsBatchAsync(dbNo, firstOffset, stride, partAddrTable.Rows.Count).ConfigureAwait(false);

                if (batchResult == null || !batchResult.IsSuccess || batchResult.Content == null)
                {
                    publicAddress.AddLog(
                        $"[scanBarCcodeAsync-批量读失败] 站{plcStationNum} 工位{workNumber} msg={batchResult?.Message}，回退到单个读");
                    return false;
                }

                // 按地址填入 valueRows（SQL VALUES 片段）
                for (int k = 0; k < batchResult.Content.Length && k < partAddrTable.Rows.Count; k++)
                {
                    string addstr = partAddrTable.Rows[k]["address"].ToString();
                    string val = batchResult.Content[k] ?? "";
                    // 单引号转义，防止 SQL 注入/语法错误
                    valueRows.Add($"({plcStationNum},'{addstr}','{val.Replace("'", "''")}',{workNumber})");
                }

                return true;
            }
            catch (Exception ex)
            {
                publicAddress.AddLog($"[scanBarCcodeAsync-批量读异常] 站{plcStationNum} 工位{workNumber} {ex.Message}");
                return false;
            }
        }
        #endregion


        #region 地址封包（核心业务)

        /// <summary>
        /// 允许参与批量合并的数据类型 → 元素字节数。
        ///
        /// 【为什么只放 4 字节类型？】
        ///   调用方（SaveResultsAsync / ScanBarCcodeAsync / Calibration*）
        ///   在展开批量读结果时，元素地址一律按 `起始地址 + i * 4` 计算。
        ///   若把 Int(2)/Word(2)/Byte(1) 也合并成 nul>1，
        ///   调用方会按 +4 拼地址，导致第 2 个元素地址算错、值写到错误地址。
        ///   因此这里只保留 4 字节类型，其余类型 ElementSize 保持 -1（单独落表 nul=1），
        ///   调用方循环体内 i 只会是 0，`i * 4 = 0`，永远正确。
        /// </summary>
        private static readonly Dictionary<string, int> _mergeableElementSize =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "DInt",  4 },   // 4 字节有符号整数
                { "DWord", 4 },   // 4 字节无符号整数
                { "Real",  4 },   // 4 字节浮点数
                                  // Int / Word / Byte / SInt / String 不加入 → ElementSize = -1 → 单独落表
            };

        /// <summary>
        /// 封包扫描用的内部项：一地址一行。
        /// 只保存封包需要的最小字段集合，避免直接操作 DataRow 反复 ToString。
        /// </summary>
        private sealed class AddPackageItem
        {
            public string Address = "";      // 原始 address 字符串（用于 udt 输出的 add 列）
            public string AddNum = "";       // 原始 addnum 字符串（形如 "200.798"，仅用于日志/排错）
            public string DataType = "";     // 数据类型（DInt/Real/String/...）
            public string WorkNumber = "";   // 工位号（用于合并时保持同工位，避免串工位）
            public string Category = "";     // 地址类别（用于合并时保持同类别，避免混合）
            public long Db = long.MinValue;      // 解析出的 DB 号；解析失败保持 MinValue
            public long Offset = long.MinValue;  // 解析出的偏移量；解析失败保持 MinValue
            public int ElementSize = -1;         // 元素字节数；-1 表示不可合并
            public bool Parsed = false;          // addnum 是否解析成功
        }

        /// <summary>
        /// 防御性解析 addnum（形如 "200.798" → db=200, offset=798）。
        /// 解析失败返回 false，不抛异常。
        ///
        /// 【为什么不用 try/catch + Convert.ToInt32？】
        ///   1. 每行地址都调一次，属热路径，避免异常开销；
        ///   2. "解析成功/失败"是预期内的分支，不是异常；
        ///   3. 用 long 解析，彻底规避 Convert.ToInt32 的 OverflowException。
        /// </summary>
        private static bool TryParseAddNum(string addnum, out long db, out long offset)
        {
            // 先初始化输出参数，保证任何提前 return 时调用方拿到的都是确定值
            db = 0;
            offset = 0;

            // null / 空串 / 纯空白 直接判定失败（"".Split('.') 会得到 [""]，[1] 会越界）
            if (string.IsNullOrWhiteSpace(addnum)) return false;

            // 规范格式只有两段：DB号.偏移；多段或单段都视为异常
            string[] parts = addnum.Trim().Split('.');
            if (parts.Length != 2) return false;

            // 两段都必须是合法数字；任一失败即整体失败
            return long.TryParse(parts[0], out db) && long.TryParse(parts[1], out offset);
        }

        /// <summary>
        /// 从 DataTable 构造封包项列表并按 (db, offset) 稳定升序排序。
        /// 不修改调用方传入的 DataTable，也不污染其 DefaultView。
        /// 解析失败的行排在最后（无法合并，单独落表）。
        /// </summary>
        /// <param name="source">源地址表（至少包含 addnum/address/dataType/workNumber/category 列）</param>
        /// <param name="rowFilter">可选行过滤器；null 表示不过滤</param>
        private List<AddPackageItem> BuildSortedItems(DataTable source, Func<DataRow, bool> rowFilter = null)
        {
            // 预分配容量，减少 List 扩容
            var items = new List<AddPackageItem>(source.Rows.Count);

            // 逐行构造封包项
            for (int i = 0; i < source.Rows.Count; i++)
            {
                DataRow row = source.Rows[i];

                // 调用方传了过滤器且本行不满足 → 跳过
                if (rowFilter != null && !rowFilter(row)) continue;

                // 一次性取出需要的字段，避免后续反复 ToString
                string addnum = row["addnum"]?.ToString() ?? "";
                string dataType = row["dataType"]?.ToString() ?? "";

                var item = new AddPackageItem
                {
                    Address = row["address"]?.ToString() ?? "",
                    AddNum = addnum,
                    DataType = dataType,
                    WorkNumber = row["workNumber"]?.ToString() ?? "",
                    Category = row["category"]?.ToString() ?? "",
                };

                // 尝试解析 addnum；失败则 Parsed 保持 false，后续走“单独落表”降级路径
                if (TryParseAddNum(addnum, out long db, out long off))
                {
                    item.Db = db;
                    item.Offset = off;
                    item.Parsed = true;

                    // 只有 4 字节类型才允许合并；查不到说明不可合并，ElementSize 保持 -1
                    if (_mergeableElementSize.TryGetValue(dataType, out int size))
                        item.ElementSize = size;
                }

                items.Add(item);
            }

            // 稳定排序：按 db、offset 升序；解析失败项排最后，相同 key 保持原相对顺序
            //   - OrderBy / ThenBy 内部是稳定排序，相同 (db,offset) 的相对顺序不变
            //   - 解析失败项统一给 long.MaxValue，排在有效项之后
            return items
                .OrderBy(it => it.Parsed ? it.Db : long.MaxValue)
                .ThenBy(it => it.Parsed ? it.Offset : long.MaxValue)
                .ToList();
        }

        /// <summary>
        /// 核心扫描：把已排序的封包项按“同 DB、同类型、等步长、同工位、同类别”合并成连续区间。
        /// 输出列：add(string) / nul(int) / work(string) / datatype(string) / category(string)。
        ///
        /// 【关键保证】
        ///   1. 输入已按 (db, offset) 稳定升序；
        ///   2. 只有 ElementSize > 0（4 字节类型）才可能合并；其余一律单独落表 nul=1；
        ///   3. 不同 workNumber / category 不会被合并，避免混合表串工位；
        ///   4. addnum 解析失败但 address 有效的行，单独落表（不丢数据）；
        ///   5. address 为空的行跳过并记日志。
        /// </summary>
        private DataTable BuildAddPackageTable(List<AddPackageItem> sortedItems)
        {
            // 构造输出表结构，列与原有实现保持一致
            DataTable udt = new DataTable("tablevalue");
            udt.Columns.Add("add", typeof(string));      // 区间起始地址
            udt.Columns.Add("nul", typeof(int));         // 区间元素个数
            udt.Columns.Add("work", typeof(string));     // 工位号
            udt.Columns.Add("datatype", typeof(string)); // 数据类型
            udt.Columns.Add("category", typeof(string)); // 地址类别

            // 空输入直接返回空表
            if (sortedItems == null || sortedItems.Count == 0) return udt;

            // runStart/runLast 标记当前正在累积的连续区间 [runStart, runLast]
            //   -1 表示当前没有活动区间
            int runStart = -1;
            int runLast = -1;

            for (int i = 0; i < sortedItems.Count; i++)
            {
                AddPackageItem cur = sortedItems[i];

                // 不可合并（String / Int / Word / Byte / 未知类型 / addnum 解析失败）
                if (!cur.Parsed || cur.ElementSize <= 0)
                {
                    // 先把前面已累积的连续区间落表
                    FlushRun(udt, sortedItems, runStart, runLast);
                    runStart = runLast = -1;

                    // address 为空的行无法读写，跳过并记日志
                    if (string.IsNullOrWhiteSpace(cur.Address))
                    {
                        publicAddress.AddLog(
                            $"[GetAddPackage] 跳过无效地址行：addnum='{cur.AddNum}' address 为空");
                    }
                    else
                    {
                        // 单独落表：nul=1，调用方循环 i 只取 0，i*4=0 安全
                        udt.Rows.Add(cur.Address, 1, cur.WorkNumber, cur.DataType, cur.Category);
                    }
                    continue;
                }

                // 可合并类型：若当前没有活动区间，则开启新区间
                if (runStart < 0)
                {
                    runStart = runLast = i;
                    continue;
                }

                // 取出当前活动区间的最后一项，判断能否与 cur 继续合并
                AddPackageItem prev = sortedItems[runLast];

                // 合并条件（全部满足才合并）：
                //   1. 上一项也解析成功；
                //   2. 元素字节数相同（都是 4）；
                //   3. 同一个 DB；
                //   4. 当前偏移 - 上一项偏移 == 元素字节数（严格连续，不能有间隔）；
                //   5. 数据类型相同（DInt/Real 不混读）；
                //   6. 工位号相同（避免跨工位合并）；
                //   7. 类别相同（避免跨类别合并）。
                bool canMerge =
                    prev.Parsed
                    && prev.ElementSize == cur.ElementSize
                    && prev.Db == cur.Db
                    && (cur.Offset - prev.Offset) == cur.ElementSize
                    && string.Equals(prev.DataType, cur.DataType, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(prev.WorkNumber, cur.WorkNumber, StringComparison.Ordinal)
                    && string.Equals(prev.Category, cur.Category, StringComparison.OrdinalIgnoreCase);

                if (canMerge)
                {
                    // 连续：扩展当前区间
                    runLast = i;
                }
                else
                {
                    // 不连续：先把上一段落表，再从当前项开启新区间
                    FlushRun(udt, sortedItems, runStart, runLast);
                    runStart = runLast = i;
                }
            }

            // 收尾：把最后一段未落表的区间落表
            FlushRun(udt, sortedItems, runStart, runLast);
            return udt;
        }

        /// <summary>
        /// 把 [start, last] 区间落成一行；区间为空则什么都不做。
        /// 输出内容以区间首项为准：add=起始地址，nul=区间长度，其余字段取首项。
        /// </summary>
        private static void FlushRun(DataTable udt, List<AddPackageItem> items, int start, int last)
        {
            // 空区间或非法区间直接返回
            if (start < 0 || last < start) return;

            AddPackageItem first = items[start];

            // 一行代表一段连续区间：
            //   add = 区间起始地址（调用方从它开始按 i*4 逐元素拼地址）
            //   nul = 区间元素个数（调用方读 count 个）
            udt.Rows.Add(
                first.Address,
                last - start + 1,
                first.WorkNumber,
                first.DataType,
                first.Category);
        }

        

        /// <summary>
        /// 根据地址表封包地址（无 PLCstationNum 过滤版本）。
        /// 调用方只需保证传入的是“某站某工位”的子表；排序由本方法内部完成。
        /// </summary>
        public DataTable GetAddPackage(DataTable addressTable)
        {
            try
            {
                // 空表直接返回空结果，避免后续无效操作
                if (addressTable == null || addressTable.Rows.Count == 0)
                    return BuildAddPackageTable(null);

                // 构造封包项列表（解析 addnum + 稳定排序）
                List<AddPackageItem> items = BuildSortedItems(addressTable);

                // 扫描合并，生成最终 udt
                return BuildAddPackageTable(items);
            }
            catch (Exception ex)
            {
                // 顶层兜底：即使内部有未预见的异常，也不让调用方拿到 null / 抛异常
                publicAddress.AddLog($"GetAddPackage 异常: {ex.Message}");
                return BuildAddPackageTable(null);
            }
        }

        /// <summary>
        /// 根据 PLC 站号、工位号封包地址（带过滤版本）。
        /// 输出列与原实现保持一致：add(string) / nul(int) / work(int)。
        /// </summary>
        public DataTable GetAddPackage(int plcstation, int work, string category, DataTable addressTable)
        {
            // 先构造输出结构（即使后续失败，调用方拿到的也是空表而非 null）
            DataTable udt = new DataTable("tablevalue");
            udt.Columns.Add("add", typeof(string));
            udt.Columns.Add("nul", typeof(int));
            udt.Columns.Add("work", typeof(int));   // 注意：本重载输出 work 为 int，与原实现一致

            try
            {
                if (addressTable == null || addressTable.Rows.Count == 0)
                    return udt;

                // 预先把过滤条件转成字符串，避免在 lambda 里反复 ToString
                string plcText = plcstation.ToString();
                string workText = work.ToString();
                string catPrefix = category ?? "";

                // 过滤条件与原 RowFilter 等价：
                //   isnull(dataType,'')<>'' and PLCstationNum={plcstation}
                //   and category like '{category}%' and len(category)<=4 and workNumber={work}
                Func<DataRow, bool> filter = row =>
                {
                    string dataType = row["dataType"]?.ToString() ?? "";
                    if (string.IsNullOrWhiteSpace(dataType)) return false;   // 与 isnull(dataType,'')<>'' 等价

                    if ((row["PLCstationNum"]?.ToString() ?? "") != plcText) return false;

                    string cat = row["category"]?.ToString() ?? "";
                    if (cat.Length > 4) return false;                         // 与 len(category)<=4 等价
                    if (!cat.StartsWith(catPrefix, StringComparison.OrdinalIgnoreCase)) return false;

                    return (row["workNumber"]?.ToString() ?? "") == workText;
                };

                // 过滤 + 解析 + 排序
                List<AddPackageItem> items = BuildSortedItems(addressTable, filter);

                // 扫描合并（输出 5 列：add/nul/work/datatype/category）
                DataTable full = BuildAddPackageTable(items);

                // 裁剪为原输出结构：只保留 add/nul/work 三列，且 work 转 int
                foreach (DataRow r in full.Rows)
                {
                    int.TryParse(r["work"]?.ToString(), out int workInt);
                    udt.Rows.Add(
                        r["add"]?.ToString() ?? "",
                        Convert.ToInt32(r["nul"]),
                        workInt);
                }

                return udt;
            }
            catch (Exception ex)
            {
                // 顶层兜底：不抛异常，返回当前已构造的 udt（可能为空表）
                publicAddress.AddLog(
                    $"GetAddPackage(plcstation={plcstation},work={work},cat={category}) 异常:{ex.Message}");
                return udt;
            }
        }

        #endregion


        

    }
}