using MES_SCAF;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TCPconnectSimaticPLC;

namespace generaControlProcedure
{
    public partial class mainform : Form
    {
        int sp_2;
        int sp_1;
        DateTime openTime = DateTime.Now;
        TimeSpan cumulativeTime;
        Form form = null;
        SQLToolsYY sqlToolsYY = new SQLToolsYY();

        public mainform()
        {
            InitializeComponent();
            sp_2 = this.splitContainer2.SplitterDistance;
            this.splitContainer2.Panel1MinSize = 15;
            sp_1 = this.splitContainer1.SplitterDistance;
            this.splitContainer1.Panel1MinSize = 15;
            this.listView1.ItemActivate += ListView1_ItemActivate;
            this.FormClosing += Mainform_FormClosing;
        }

        /// <summary>
        /// 窗体关闭事件。
        ///
        /// 【本次改动】无。保持原有逻辑。
        /// </summary>
        private void Mainform_FormClosing(object sender, FormClosingEventArgs e)
        {
            DialogResult r = MessageBox.Show("程序正在使用中,确认退出?", "确认退出", MessageBoxButtons.YesNo);
            if (r == DialogResult.Yes)//点击"是"按钮
            {
                //更新关机时间
                string sqlstr_1 = "exec fynP_getstartingUpTime 'unattended'";
                DataTable dt1 = new DataTable();
                Hint hint = new Hint();
                hint = sqlToolsYY.GetDataTable("", sqlstr_1, true, out dt1);
                e.Cancel = false;//事件的取消为假，关闭窗体
            }
            else
            {
                e.Cancel = true;//事件的取消为真，不关闭窗体
            }
        }

        /// <summary>
        /// 点击按钮，根据按钮执行打开窗口。
        ///
        /// 【本次改动】无。保持原有逻辑。
        /// </summary>
        private void ListView1_ItemActivate(object sender, EventArgs e)
        {
            if (this.listView1.SelectedIndices.Count > 0 && this.listView1.SelectedIndices != null)
            {
                switch (this.listView1.Items[this.listView1.SelectedIndices[0]].Text)
                {
                    case "计划排配":
                        {
                            PlanForm f = new PlanForm();
                            if (!CheckFormIsOpen(f.Name, out form))
                            {
                                f.TopLevel = false;
                                f.Parent = splitContainer2.Panel2;
                                f.WindowState = FormWindowState.Maximized;
                                f.FormBorderStyle = FormBorderStyle.FixedSingle;
                                form = f;
                                f.Show();
                            }
                            else
                            {
                                form.TopMost = true;
                                form.WindowState = FormWindowState.Maximized;
                                form.Activate();
                            }
                            break;
                        }
                    case "计划选择":
                        {
                            productionPlanForm f = new productionPlanForm();
                            if (!CheckFormIsOpen(f.Name, out form))
                            {
                                f.TopLevel = false;
                                f.Parent = splitContainer2.Panel2;
                                f.WindowState = FormWindowState.Maximized;
                                f.FormBorderStyle = FormBorderStyle.FixedSingle;
                                form = f;
                                f.Show();
                            }
                            else
                            {
                                form.TopMost = true;
                                form.WindowState = FormWindowState.Maximized;
                                form.Activate();
                            }
                            break;
                        }
                    case "过程数据":
                        {
                            Process process = new Process();//声明一个进程类对象
                            process.StartInfo.FileName = "SelectCourseDatatable.exe";
                            process.Start();
                            break;
                        }
                    case "自校数据":
                        {
                            Process process = new Process();//声明一个进程类对象
                            process.StartInfo.FileName = "SelectSelfCorrectingDatatable.exe";
                            process.Start();
                            break;
                        }
                    case "检验单":
                        {
                            MessageBox.Show("程序员正在熬夜开发中，请耐心等待！");
                            break;
                        }
                    case "程序参数":
                        {
                            userLoginForm login = new userLoginForm(publicAddress.lineNumber + "_parameter");
                            if (login.ShowDialog() == DialogResult.OK)
                            {
                                parameterForm f = new parameterForm();
                                if (!CheckFormIsOpen(f.Name, out form))
                                {
                                    f.TopLevel = false;
                                    f.Parent = splitContainer2.Panel2;
                                    f.WindowState = FormWindowState.Maximized;
                                    f.FormBorderStyle = FormBorderStyle.FixedSingle;
                                    form = f;
                                    f.Show();
                                }
                                else
                                {
                                    form.TopMost = true;
                                    form.WindowState = FormWindowState.Maximized;
                                    form.Activate();
                                }
                            }
                            break;
                        }
                    case "设备参数":
                        {
                            userLoginForm login = new userLoginForm(publicAddress.lineNumber + "_parameter");
                            if (login.ShowDialog() == DialogResult.OK)
                            {
                                equipmentParameterForm f = new equipmentParameterForm();
                                if (!CheckFormIsOpen(f.Name, out form))
                                {
                                    f.TopLevel = false;
                                    f.Parent = splitContainer2.Panel2;
                                    f.WindowState = FormWindowState.Maximized;
                                    f.FormBorderStyle = FormBorderStyle.FixedSingle;
                                    form = f;
                                    f.Show();
                                }
                                else
                                {
                                    form.TopMost = true;
                                    form.WindowState = FormWindowState.Maximized;
                                    form.Activate();
                                }
                            }
                            break;
                        }
                    case "程序参数历史版本":
                        {
                            userLoginForm login = new userLoginForm(publicAddress.lineNumber + "_parameter");
                            if (login.ShowDialog() == DialogResult.OK)
                            {
                                parameterVersionsForm f = new parameterVersionsForm();
                                if (!CheckFormIsOpen(f.Name, out form))
                                {
                                    f.TopLevel = false;
                                    f.Parent = splitContainer2.Panel2;
                                    f.WindowState = FormWindowState.Maximized;
                                    f.FormBorderStyle = FormBorderStyle.FixedSingle;
                                    form = f;
                                    f.Show();
                                }
                                else
                                {
                                    form.TopMost = true;
                                    form.WindowState = FormWindowState.Maximized;
                                    form.Activate();
                                }
                            }
                            break;
                        }
                    case "设备参数历史版本":
                        {
                            userLoginForm login = new userLoginForm(publicAddress.lineNumber + "_parameter");
                            if (login.ShowDialog() == DialogResult.OK)
                            {
                                equipmentParameterVersionsForm f = new equipmentParameterVersionsForm();
                                if (!CheckFormIsOpen(f.Name, out form))
                                {
                                    f.TopLevel = false;
                                    f.Parent = splitContainer2.Panel2;
                                    f.WindowState = FormWindowState.Maximized;
                                    f.FormBorderStyle = FormBorderStyle.FixedSingle;
                                    form = f;
                                    f.Show();
                                }
                                else
                                {
                                    form.TopMost = true;
                                    form.WindowState = FormWindowState.Maximized;
                                    form.Activate();
                                }
                            }
                            break;
                        }
                    case "报警信息":
                        {
                            ErrorForm f = new ErrorForm();
                            if (!CheckFormIsOpen(f.Name, out form))
                            {
                                f.TopLevel = false;
                                f.Parent = splitContainer2.Panel2;
                                f.WindowState = FormWindowState.Maximized;
                                f.FormBorderStyle = FormBorderStyle.FixedSingle;
                                form = f;
                                f.Show();
                            }
                            else
                            {
                                form.TopMost = true;
                                form.WindowState = FormWindowState.Maximized;
                                form.Activate();
                            }
                            break;
                        }
                    case "通讯监控":
                        {
                            monitoringForm f = new monitoringForm();
                            if (!CheckFormIsOpen(f.Name, out form))
                            {
                                f.TopLevel = false;
                                f.Parent = splitContainer2.Panel2;
                                f.WindowState = FormWindowState.Maximized;
                                f.FormBorderStyle = FormBorderStyle.FixedSingle;
                                form = f;
                                f.Show();
                            }
                            else
                            {
                                form.TopMost = true;
                                form.WindowState = FormWindowState.Maximized;
                                form.Activate();
                            }
                            break;
                        }
                    case "状态监控":
                        {
                            ShowPlcStatusMessagesForm f = new ShowPlcStatusMessagesForm();
                            if (!CheckFormIsOpen(f.Name, out form))
                            {
                                f.TopLevel = false;
                                f.Parent = splitContainer2.Panel2;
                                f.WindowState = FormWindowState.Maximized;
                                f.FormBorderStyle = FormBorderStyle.FixedSingle;
                                form = f;
                                f.Show();
                            }
                            else
                            {
                                form.TopMost = true;
                                form.WindowState = FormWindowState.Maximized;
                                form.Activate();
                            }
                            break;
                        }
                    case "用户管理":
                        {
                            MessageBox.Show("程序员正在熬夜开发中，请耐心等待！");
                            break;
                        }
                    case "权限管理":
                        {
                            MessageBox.Show("程序员正在熬夜开发中，请耐心等待！");
                            break;
                        }
                    case "检验项目":
                        {
                            MessageBox.Show("程序员正在熬夜开发中，请耐心等待！");
                            break;
                        }
                    case "设备资料":
                        {
                            MessageBox.Show("程序员正在熬夜开发中，请耐心等待！");
                            break;
                        }
                    case "自校录入":
                        {
                            manualInputForm f = new manualInputForm();
                            if (!CheckFormIsOpen(f.Name, out form))
                            {
                                f.TopLevel = false;
                                f.Parent = splitContainer2.Panel2;
                                f.WindowState = FormWindowState.Maximized;
                                f.FormBorderStyle = FormBorderStyle.FixedSingle;
                                form = f;
                                f.Show();
                            }
                            else
                            {
                                form.TopMost = true;
                                form.WindowState = FormWindowState.Maximized;
                                form.Activate();
                            }
                            break;
                        }
                }
            }
        }

        /// <summary>
        /// 检查窗口是否已经打开。
        ///
        /// 【本次改动】无。保持原有逻辑。
        /// </summary>
        private bool CheckFormIsOpen(string asFormName, out Form form)
        {
            bool bResult = false;
            form = null;
            foreach (Form frm in Application.OpenForms)
            {
                if (frm.Name == asFormName)
                {
                    form = frm;
                    bResult = true;
                    break;
                }
            }
            return bResult;
        }

        /// <summary>
        /// 窗体加载。
        ///
        /// 【本次改动】无。保持原有逻辑（心跳配置加载、日志清理、启动 PLC 站初始化）。
        /// </summary>
        private void mainform_Load(object sender, EventArgs e)
        {
            _ = this.BeginInvoke((EventHandler)delegate
            {
                // ⭐ 加载心跳全局配置（必须在创建 PLCService 之前）
                HeartbeatConfig.LoadFromConfig();

                S7TimeoutConfig.LoadFromConfig();

                // ⭐ 程序启动时后台清理超过30天的日志（只执行一次）
                Task.Run(() => new WriteReadLog().DeleteOldLogs(30));

                #region 获取公司名称
                this.manufactureCompanyName.Text = "软件开发：隆中控股集团股份有限公司__信息工程部";
                #endregion
                toolStripStatusLabel1.Text = "程序集版本：" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();

                /*********************************************/
                publicAddress.package = 0;
                //更新开机时间
                string sqlstr_1 = "exec fynP_getstartingUpTime 'startingUp'";
                DataTable dt1 = new DataTable();
                Hint m_hint = new Hint();
                m_hint = sqlToolsYY.GetDataTable("", sqlstr_1, true, out dt1);
                //获取生产线编号
                if (m_hint.Valid)
                {
                    OpenConnect();
                    // 改为调用顺序创建的方法（稳定可靠）
                    _ = InitializeStationsSequentially();
                }
            });
        }

        /// <summary>
        /// 打开连接（初始化 ListView、打开默认窗体）。
        ///
        /// 【本次改动】无。保持原有逻辑。
        /// </summary>
        private void OpenConnect()
        {
            string sqlstr_1 = "SELECT * FROM [dbo].[fynT_lineXML]";
            DataTable dt1 = new DataTable();
            Hint hint = new Hint();
            hint = sqlToolsYY.GetDataTable("", sqlstr_1, true, out dt1);
            if (hint.Valid && dt1.Rows.Count > 0)
            {
                publicAddress.lineNumber = dt1.Rows[0]["lineNumber"].ToString();
                publicAddress.lineType = dt1.Rows[0]["lineType"].ToString();
            }
            #region 加载初始ListView控件
            this.listView1.Items.Clear();
            ListViewItem item1 = new ListViewItem();
            item1.ImageIndex = 1;
            item1.Text = "计划选择";
            this.listView1.Items.Add(item1);
            #endregion

            //打开默认窗口
            #region 打开默认窗口
            ShowPlcStatusMessagesForm f = new ShowPlcStatusMessagesForm();
            if (!CheckFormIsOpen(f.Name, out form))
            {
                f.TopLevel = false;
                f.Parent = splitContainer2.Panel2;
                f.WindowState = FormWindowState.Maximized;
                f.FormBorderStyle = FormBorderStyle.FixedSingle;
                form = f;
                f.Show();
            }
            else
            {
                form.TopMost = true;
                form.WindowState = FormWindowState.Maximized;
                form.Activate();
            }
            #endregion
        }

        /// <summary>
        /// 顺序创建所有PLC站实例（稳定可靠，避免并发冲突）。
        ///
        /// 【本次改动】无。保持原有逻辑（循环创建 + 100ms 间隔）。
        /// </summary>
        private async Task InitializeStationsSequentially()
        {
            await Task.Run(async () =>
            {
                try
                {
                    string sqlstr = "select * from fynT_threadXML order by threadNum";
                    DataTable dt = new DataTable();
                    Hint hint = sqlToolsYY.GetDataTable("", sqlstr, true, out dt);
                    if (!hint.Valid || dt.Rows.Count == 0)
                    {
                        LogError("未从数据库读取到PLC站配置（fynT_threadXML）");
                        return;
                    }

                    int totalStations = dt.Rows.Count;
                    LogInfo($"开始顺序创建 {totalStations} 个PLC站实例...");

                    // 按IP:Port去重统计，用于日志
                    var ipPortSet = new System.Collections.Generic.HashSet<string>();

                    for (int i = 0; i < totalStations; i++)
                    {
                        DataRow row = dt.Rows[i];
                        int plcstationNum = Convert.ToInt32(row["PLCstationNum"]);
                        int receive = Convert.ToInt32(row["threadNum"]);
                        try
                        {
                            LogInfo($"正在创建第 {i + 1}/{totalStations} 个站: PLCstationNum={plcstationNum}, threadNum={receive}");
                            var instance = await receiveClass.CreateAsync(plcstationNum, receive);
                            LogInfo($"站 {plcstationNum} 创建成功");

                            // 记录该站使用的IP:Port（从数据库获取），用于日志
                            string ip = GetIPFromStation(plcstationNum);
                            ipPortSet.Add(ip);
                        }
                        catch (Exception ex)
                        {
                            LogError($"创建站 {plcstationNum} (threadNum={receive}) 失败: {ex.Message}\r\n{ex.StackTrace}");
                        }
                        // 每个站创建后等待100ms，避免数据库压力过大
                        await Task.Delay(100);
                    }
                    LogInfo($"所有PLC站实例创建完成，涉及 {ipPortSet.Count} 个物理IP:Port，每个物理连接只有一套后台线程。");
                }
                catch (Exception ex)
                {
                    LogError($"InitializeStationsSequentially 整体异常: {ex.Message}\r\n{ex.StackTrace}");
                    MessageBox.Show($"初始化PLC站失败，请查看日志。\n{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            });
        }

        /// <summary>
        /// 辅助方法：根据PLCstationNum查询IP地址。
        ///
        /// 【本次改动】无。保持原有逻辑。
        /// </summary>
        private string GetIPFromStation(int plcstationNum)
        {
            try
            {
                string sql = $"select top 1 IPAddress from fynT_PLCaddress where PLCstationNum={plcstationNum}";
                DataTable dt = new DataTable();
                Hint hint = sqlToolsYY.GetDataTable("", sql, true, out dt);
                if (hint.Valid && dt.Rows.Count > 0)
                    return dt.Rows[0]["IPAddress"].ToString();
            }
            catch { }
            return "未知";
        }

        /// <summary>
        /// 记录信息日志。
        ///
        /// ============================================================
        /// 【本次改动：从 errtext 字符串拼接改为 LogCollector 入队】
        /// ============================================================
        /// 原实现：
        ///     publicAddress.errtext += log;   // O(n) 字符串复制
        /// 程序长期运行后 errtext 累积到十几 MB，每次 += 都要复制整个字符串，
        /// 是导致"多站同时卡顿"的主因之一。
        ///
        /// 新实现：
        ///     publicAddress.AddLog(log);      // O(1) 入队，后台批量落盘
        /// ============================================================
        /// </summary>
        private void LogInfo(string msg)
        {
            // 拼上级别前缀（时间戳由 WriteReadLog 落盘时自动补）
            string log = $"[信息] {msg}";

            // O(1) 入队，立即返回
            publicAddress.AddLog(log);

            // 仍保留 Debug.WriteLine，方便开发时在输出窗口看到
            Debug.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {log}");
        }

        /// <summary>
        /// 记录错误日志。
        ///
        /// ============================================================
        /// 【本次改动：从 errtext 字符串拼接改为 LogCollector 入队】
        /// ============================================================
        /// 与 LogInfo 一致：把"日志写入"从 errtext += 换成 AddLog。
        /// MessageBox 弹窗逻辑保持不变（业务语义需要）。
        /// ============================================================
        /// </summary>
        private void LogError(string msg)
        {
            // 拼上级别前缀
            string log = $"[错误] {msg}";

            // O(1) 入队，立即返回
            publicAddress.AddLog(log);

            // Debug 输出
            Debug.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {log}");

            // ========== 弹窗提示（保持原逻辑） ==========
            // 如果错误发生在 UI 线程外，用 BeginInvoke 回到 UI 线程显示
            if (this.InvokeRequired)
                this.BeginInvoke(new Action(() => MessageBox.Show(msg, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)));
            else
                MessageBox.Show(msg, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /// <summary>
        /// 单站模式：仅连接一个 PLC 站。
        ///
        /// 【本次改动】无。保持原有逻辑。
        /// </summary>
        private async Task InitializeSingleStation()
        {
            await Task.Run(async () =>
            {
                var instance = await receiveClass.CreateAsync(1, 1);
                var instance1 = await receiveClass.CreateAsync(2, 2);
            });
        }

        /// <summary>计划管理按钮。</summary>
        private void button1_Click(object sender, EventArgs e)
        {
            this.listView1.Items.Clear();
            ListViewItem item1 = new ListViewItem();
            item1.ImageIndex = 1;
            item1.Text = "计划选择";
            this.listView1.Items.Add(item1);
        }

        /// <summary>生产统计管理。</summary>
        private void button2_Click(object sender, EventArgs e)
        {
            this.listView1.Items.Clear();
            ListViewItem item = new ListViewItem();
            item.ImageIndex = 2;
            item.Text = "过程数据";
            this.listView1.Items.Add(item);

            ListViewItem item1 = new ListViewItem();
            item1.ImageIndex = 13;
            item1.Text = "自校数据";
            this.listView1.Items.Add(item1);
            ListViewItem item2 = new ListViewItem();
            item2.ImageIndex = 12;
            item2.Text = "自校录入";
            this.listView1.Items.Add(item2);
        }

        /// <summary>质量管理。</summary>
        private void button3_Click(object sender, EventArgs e)
        {
            this.listView1.Items.Clear();
            ListViewItem item = new ListViewItem();
            item.ImageIndex = 3;
            item.Text = "检验计划";
            this.listView1.Items.Add(item);
        }

        /// <summary>工艺管理。</summary>
        private void button4_Click(object sender, EventArgs e)
        {
            this.listView1.Items.Clear();
            ListViewItem item = new ListViewItem();
            item.ImageIndex = 4;
            item.Text = "程序参数";
            this.listView1.Items.Add(item);
            ListViewItem item1 = new ListViewItem();
            item1.ImageIndex = 12;
            item1.Text = "设备参数";
            this.listView1.Items.Add(item1);

            ListViewItem item2 = new ListViewItem();
            item2.ImageIndex = 14;
            item2.Text = "程序参数历史版本";
            this.listView1.Items.Add(item2);

            ListViewItem item3 = new ListViewItem();
            item3.ImageIndex = 15;
            item3.Text = "设备参数历史版本";
            this.listView1.Items.Add(item3);
        }

        /// <summary>设备管理。</summary>
        private void button6_Click(object sender, EventArgs e)
        {
            this.listView1.Items.Clear();
            ListViewItem item = new ListViewItem();
            item.ImageIndex = 5;
            item.Text = "报警信息";
            this.listView1.Items.Add(item);
            ListViewItem item2 = new ListViewItem();
            item2.ImageIndex = 7;
            item2.Text = "状态监控";
            this.listView1.Items.Add(item2);
        }

        /// <summary>用户管理。</summary>
        private void button7_Click(object sender, EventArgs e)
        {
            this.listView1.Items.Clear();
            ListViewItem item = new ListViewItem();
            item.ImageIndex = 8;
            item.Text = "用户管理";
            this.listView1.Items.Add(item);
            ListViewItem item1 = new ListViewItem();
            item1.ImageIndex = 9;
            item1.Text = "权限管理";
            this.listView1.Items.Add(item1);
        }

        /// <summary>基础数据。</summary>
        private void button5_Click(object sender, EventArgs e)
        {
            this.listView1.Items.Clear();
            ListViewItem item = new ListViewItem();
            item.ImageIndex = 10;
            item.Text = "检验项目";
            this.listView1.Items.Add(item);
            ListViewItem item1 = new ListViewItem();
            item1.ImageIndex = 11;
            item1.Text = "设备资料";
            this.listView1.Items.Add(item1);
        }

        /// <summary>左侧显示隐藏。</summary>
        private void button8_Click(object sender, EventArgs e)
        {
            if ("《" == button8.Text)
            {
                splitContainer2.SplitterDistance = 18;
                button8.Text = "》";
                if (form != null)
                {
                    form.WindowState = FormWindowState.Minimized;
                    form.WindowState = FormWindowState.Maximized;
                }
            }
            else
            {
                splitContainer2.SplitterDistance = sp_2;
                button8.Text = "《";
                if (form != null)
                {
                    form.WindowState = FormWindowState.Minimized;
                    form.WindowState = FormWindowState.Maximized;
                }
            }
        }

        /// <summary>上端显示隐藏。</summary>
        private void button9_Click(object sender, EventArgs e)
        {
            if ("︽" == button9.Text)
            {
                splitContainer1.SplitterDistance = 18;
                button9.Text = "︾";
                if (form != null)
                {
                    form.WindowState = FormWindowState.Minimized;
                    form.WindowState = FormWindowState.Maximized;
                }
            }
            else
            {
                splitContainer1.SplitterDistance = sp_1;
                button9.Text = "︽";
                if (form != null)
                {
                    form.WindowState = FormWindowState.Minimized;
                    form.WindowState = FormWindowState.Maximized;
                }
            }
        }

        /// <summary>
        /// 定时器1s执行一次。
        ///
        /// 【本次改动】无。保持原有逻辑（package++ / 运行时间显示）。
        /// </summary>
        private void timer1_Tick(object sender, EventArgs e)
        {
            try
            {
                publicAddress.package++;//工控机心跳包
                if (publicAddress.package > 99999)
                {
                    publicAddress.package = 0;
                }
                cumulativeTime = DateTime.Now - openTime;
                this.textcumulativeTime.Text = "累计运行时间：" + cumulativeTime.Days.ToString() + "天" + cumulativeTime.Hours.ToString() + "时" + cumulativeTime.Minutes.ToString() + "分" + cumulativeTime.Seconds.ToString() + "秒";
            }
            catch (Exception) { }
        }
    }
}