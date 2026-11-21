using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;

namespace generaControlProcedure
{
    class publicAddress
    {
        /// <summary>
        /// 登录名
        /// </summary>
        public static string loginName;//登录名 
        /// <summary>
        /// 上位机心跳包
        /// </summary>
        private static int packages;
        /// <summary>
        /// 上位机心跳包
        /// </summary>
        public static int package//上位机心跳包
        {
            get
            {
                return packages;
            }
            set
            {
                if (value != packages)
                {
                    packagesChanged();
                }
                packages = value;
            }
        }

        /// <summary>
        /// 上位机心跳包改变事件
        /// </summary>
        public static void packagesChanged()
        {
            if (communicationForm.f2!=null)
            {
                communicationForm.f2.HeartBeat.Text = packages.ToString();
            }
            
        }
        /// <summary>
        /// 原因
        /// </summary>
        public static string reasonText;
        /// <summary>
        /// 线程间隔时间
        /// </summary>
        public static int scantime = 50;//间隔时间
        /// <summary>
        /// 创建锁
        /// </summary>
        public volatile static object locker = new object();

        /// <summary>
        /// 创建锁
        /// </summary>
        public volatile static object plcLocker = new object();

        /// <summary>
        /// 创建锁
        /// </summary>
        public volatile static object plcConnectLocker = new object();
        /// <summary>
        /// 报错信息
        /// </summary>
        public static string errtext;

        /// <summary>
        /// 监控联机状态信息
        /// </summary>
        public static string[,] TrafficStatus = new string[100, 5];
        /// <summary>
        /// 参数上载标识
        /// </summary>
        public static int[] readParameter = new int[100];
        public static DataSet dataset = new DataSet();
        public static string Cinvaddcode{get;set;}
        public static string Cinvcode { get; set; }
        public static string Batch { get; set; }
        public static int Modid { get; set; }
        public static int jobNumber { get; set;}
        public static int planQty { get; set; }
        public static DataTable loginData { get; set; }
        public static string lineNumber { get; set; }
        public static string lineType { get; set; }
        public static string Gxbm { get; set; }
        public static string Gylx { get; set; }
        public static string Zjno { get; set; }
        /// <summary>
        /// 线程安全日志写入入口（替代 errtext += "..."）。
        ///
        /// ============================================================
        /// 【为什么不能继续用 errtext += ？】
        /// ============================================================
        /// errtext 是 string（不可变），每次 += 都要复制整个字符串。
        /// 长期运行后 errtext 累积到几十 MB，一次 += 就是几十 MB 的内存拷贝，
        /// 几十次/秒的调用直接拖垮 CPU、触发频繁 GC —— 这是"多站同时卡顿"的头号根因。
        ///
        /// ============================================================
        /// 【本方法的实现】
        /// ============================================================
        /// 直接转调 LogCollector.Write：
        ///   - O(1) 入队，调用方立即返回
        ///   - 后台每秒批量落盘，一次写 100KB 而不是一秒写 50 次
        ///
        /// ============================================================
        /// 【替换规则】
        /// ============================================================
        /// - 所有 `publicAddress.errtext += "xxx\r\n"`  →  `publicAddress.AddLog("xxx")`
        ///   （注意：去掉末尾的 \r\n，WriteReadLog 会自动换行）
        /// - 所有 `publicAddress.errtext += $"xxx"`      →  `publicAddress.AddLog($"xxx")`
        /// - errtext 字段本身保留（避免 StationContext.LogErrtext 等旧代码编译报错），
        ///   但不应再有人往它 += 了。
        /// ============================================================
        /// </summary>
        /// <param name="message">日志内容（不需要带时间戳，不需要末尾换行）</param>
        public static void AddLog(string message)
        {
            // 转调 LogCollector，真正实现"入队 + 批量落盘"
            // 注意：如果 publicAddress 已在 MES_SCAF 命名空间内，直接 LogCollector.Write 即可；
            //      如果不在，请用 MES_SCAF.LogCollector.Write 或顶部加 using MES_SCAF;
            MES_SCAF.LogCollector.Write(message);
        }

    }
}
