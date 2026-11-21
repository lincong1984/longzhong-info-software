using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.Net;
using MES_SCAF;

namespace generaControlProcedure
{
    public partial class ShowPlcStatusMessagesForm : Form
    {
        Hint hint = new Hint();
        SQLToolsYY sqltoolsyy = new SQLToolsYY();
        WriteReadLog writeReadLog = new WriteReadLog();
        Thread thread = null;
        int rowcount = 0;
        public ShowPlcStatusMessagesForm()
        {
            InitializeComponent();
            #region
            /**
             * 利用双缓冲减少界面卡顿
             *OptimizedDoubleBuffer:双缓冲 
             * AllPaintingInWmPaint:不擦除背景，减少闪烁
             */
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint, true);
            this.UpdateStyles();
            #endregion
            this.ControlBox = false;//禁用窗体的关闭按钮
            Control.CheckForIllegalCrossThreadCalls = false;
            this.Load += Form2_Load;

        }
        /// <summary>
        /// 解决页面上所有控件加载慢的方法
        /// </summary>
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;
                return cp;
            }
        }
        /// <summary>
        /// 窗体加载事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Form2_Load(object sender, EventArgs e)
        {
            try
            {
                init();
                _ = RefreshData();
                _ = ReadListBoxtoText();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        private void init()
        {
            try
            {
                string hostName = Dns.GetHostName(); // 本机名 
                System.Net.IPAddress[] ipHost = Dns.GetHostAddresses(hostName); // 返回所有地址，包括IPv4和IPv6

                string sqlStr = @"select * from fynV_ShowPlcStatusMessages";
                DataTable dt = new DataTable();
                hint = sqltoolsyy.GetDataTable("", sqlStr, true, out dt);
                if (hint.Valid)
                {
                    rowcount = dt.Rows.Count;

                    // 预先处理控件
                    Dictionary<string, Control> controls = new Dictionary<string, Control>();
                    for (int i = 0; i < dt.Rows.Count; i++)
                    {
                        addPanel(i.ToString());

                        string labelName = "label_3_" + i.ToString();
                        string ipAddressName = "textBox_2_" + i.ToString();
                        string plcBrandName = "textBox_6_" + i.ToString();

                        controls[labelName] = Controls.Find(labelName, true).FirstOrDefault();
                        controls[ipAddressName] = Controls.Find(ipAddressName, true).FirstOrDefault();
                        controls[plcBrandName] = Controls.Find(plcBrandName, true).FirstOrDefault();
                    }

                    // 设置控件的值
                    for (int i = 0; i < dt.Rows.Count; i++)
                    {
                        string labelName = "label_3_" + i.ToString();
                        string ipAddressName = "textBox_2_" + i.ToString();
                        string plcBrandName = "textBox_6_" + i.ToString();

                        if (controls.TryGetValue(labelName, out Control labelControl) && labelControl != null)
                        {
                            labelControl.Text = dt.Rows[i]["name"].ToString();
                        }

                        if (controls.TryGetValue(ipAddressName, out Control ipAddressControl) && ipAddressControl != null)
                        {
                            ipAddressControl.Text = dt.Rows[i]["IPAddress"].ToString();
                        }

                        if (controls.TryGetValue(plcBrandName, out Control plcBrandControl) && plcBrandControl != null)
                        {
                            plcBrandControl.Text = dt.Rows[i]["PLCbrand"].ToString();
                        }
                    }

                    // 优化IP地址匹配逻辑
                    if (dt.Rows.Count > 0)
                    {
                        string[] targetSubnetParts = dt.Rows[0]["IPAddress"].ToString().Split('.');
                        string targetSubnet = $"{targetSubnetParts[0]}.{targetSubnetParts[1]}.{targetSubnetParts[2]}";

                        foreach (IPAddress ip in ipHost)
                        {
                            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) // 只处理IPv4地址
                            {
                                string[] ipParts = ip.ToString().Split('.');
                                string subnet = $"{ipParts[0]}.{ipParts[1]}.{ipParts[2]}";

                                if (subnet == targetSubnet)
                                {
                                    this.textBox1.Text = ip.ToString();
                                    break; // 提前退出循环
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
                // 可以在这里添加其他错误处理逻辑，例如记录日志
            }
        }

        private async Task RefreshData()
        {
            await Task.Run(() =>
            {
                try
                {
                    string sqlStr = @"select * from fynV_ShowPlcStatusMessages";
                    DataTable dt = new DataTable();
                    var hint = sqltoolsyy.GetDataTable("", sqlStr, true, out dt);

                    // 预存所有控件引用
                    var controls = new Dictionary<int, Control[]>();
                    for (int j = 0; j < dt.Rows.Count; j++)
                    {
                        controls[j] = new Control[]
                        {
                            Controls.Find("label_7_" + j, true)[0],
                            Controls.Find("myLED_1_" + j, true)[0],
                            Controls.Find("textBox_1_" + j, true)[0],
                            Controls.Find("textBox_3_" + j, true)[0],
                            Controls.Find("textBox_4_" + j, true)[0],
                            Controls.Find("textBox_5_" + j, true)[0]
                        };
                    }

                    TextBox textBox2 = this.textBox2;
                    TextBox textBox4 = this.textBox4;
                    TextBox textBox5 = this.textBox5;

                    while (true)
                    {
                        if (hint.Valid)
                        {
                            // 批量构建UI更新委托
                            Action updateAction = () =>
                            {
                                for (int j = 0; j < dt.Rows.Count; j++)
                                {
                                    var ctrls = controls[j];
                                    var statusLabel = ctrls[0] as Label;
                                    var led = ctrls[1] as MyLED;
                                    var heartbeatBox = ctrls[2] as TextBox;
                                    var barcodeBox = ctrls[3] as TextBox;
                                    var cinvaddcodeBox = ctrls[4] as TextBox;
                                    var batchBox = ctrls[5] as TextBox;
                                    // 更新PLC状态
                                    if (publicAddress.TrafficStatus[j, 0] != null)
                                        statusLabel.Text = "状态：" + publicAddress.TrafficStatus[j, 0];

                                    if (publicAddress.TrafficStatus[j, 1] != null)
                                    {
                                        switch (publicAddress.TrafficStatus[j, 1])
                                        {
                                            case "Color.Yellow": led.LedStatus = 1; break;
                                            case "Color.Lime": led.LedStatus = 3; break;
                                            case "Color.Red": led.LedStatus = 2; break;
                                        }
                                    }

                                    if (publicAddress.TrafficStatus[j, 2] != null)
                                        heartbeatBox.Text = publicAddress.TrafficStatus[j, 2];

                                    // 更新条码、图号、批号
                                    barcodeBox.Text = dt.Rows[j]["barcode"].ToString();
                                    cinvaddcodeBox.Text = dt.Rows[j]["cinvaddcode"].ToString();
                                    batchBox.Text = dt.Rows[j]["batch"].ToString();
                                }

                                // 更新全局信息
                                textBox2.Text = publicAddress.package.ToString();
                                textBox4.Text = publicAddress.Cinvaddcode;
                                textBox5.Text = publicAddress.Batch;
                            };

                            // 使用BeginInvoke避免阻塞
                            if (!this.IsDisposed)
                                this.BeginInvoke(updateAction);
                        }

                        Thread.Sleep(1000); // 降低更新频率
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("RefreshData Error: " + ex.Message);
                }
            });
        }

        /// <summary>
        /// 动态添加控件
        /// </summary>
        /// <param name="num"></param>
        private void addPanel(string num)
        {
            try
            {
                Panel panel = new Panel();
                // 
                // textBox_1 心跳包显示框
                // 
                TextBox textBox_1 = new TextBox();
                textBox_1.Name = "textBox_1_" + num;
                textBox_1.Location = new System.Drawing.Point(67, 79);//(84, 82)
                textBox_1.Size = new System.Drawing.Size(200, 26);
                textBox_1.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                textBox_1.ReadOnly = true;
                textBox_1.TabIndex = 4;
                // 
                // textBox_2 IP地址显示框
                // 
                TextBox textBox_2 = new TextBox();
                textBox_2.Name = "textBox_2_" + num;
                textBox_2.Location = new System.Drawing.Point(67, 44);
                textBox_2.Size = new System.Drawing.Size(200, 26);
                textBox_2.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                textBox_2.ReadOnly = true;
                textBox_2.TabIndex = 3;
                // 
                // label_1
                // 
                Label label_1 = new Label();
                label_1.AutoSize = true;
                label_1.Font = new System.Drawing.Font("宋体", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label_1.Location = new System.Drawing.Point(2, 83);
                label_1.Name = "label_1_" + num;
                label_1.Size = new System.Drawing.Size(66, 19);
                label_1.TabIndex = 2;
                label_1.Text = "心跳包";
                // 
                // label_2
                // 
                Label label_2 = new Label();
                label_2.AutoSize = true;
                label_2.Font = new System.Drawing.Font("宋体", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label_2.Location = new System.Drawing.Point(2, 48);
                label_2.Name = "label_2_" + num;
                label_2.Size = new System.Drawing.Size(67, 19);
                label_2.TabIndex = 1;
                label_2.Text = "IP地址";
                // 
                // label_3 工序名称 工序号
                // 
                Label label_3 = new Label();
                label_3.AutoSize = false;
                label_3.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                | System.Windows.Forms.AnchorStyles.Right)));
                label_3.Font = new System.Drawing.Font("宋体", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label_3.Location = new System.Drawing.Point(77, 11);
                label_3.Name = "label_3_" + num;
                label_3.Size = new System.Drawing.Size(104, 19);
                label_3.TabIndex = 0;
                label_3.Text = "上位机信息";
                label_3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
                // 
                // myLED1
                // 
                MyLED myLED_1 = new MyLED();
                myLED_1.BorderWidth = 3;
                myLED_1.CenterColor = System.Drawing.Color.White;
                myLED_1.FlashInterval = 500;
                myLED_1.GapWidth = 3;
                myLED_1.IsBorder = true;
                myLED_1.IsFlash = true;
                myLED_1.IsHighLight = true;
                myLED_1.LampColor = new System.Drawing.Color[] {
            System.Drawing.Color.White,
            System.Drawing.Color.Yellow,
            System.Drawing.Color.Red,
            System.Drawing.Color.Green};
                myLED_1.LedColor = System.Drawing.Color.Green;
                myLED_1.LedOneColor = System.Drawing.Color.Green;
                myLED_1.LedStatus = 1;
                myLED_1.LedThreeColor = System.Drawing.Color.Yellow;
                myLED_1.LedTwoColor = System.Drawing.Color.Red;
                myLED_1.Location = new System.Drawing.Point(16, 8);
                myLED_1.Name = "myLED_1_" + num;
                myLED_1.Size = new System.Drawing.Size(26, 27);
                myLED_1.TabIndex = 1;
                // 
                // label_4
                // 
                Label label_4 = new Label();
                label_4.AutoSize = true;
                label_4.Font = new System.Drawing.Font("宋体", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label_4.Location = new System.Drawing.Point(2, 118);
                label_4.Name = "label_4_" + num;
                label_4.Size = new System.Drawing.Size(47, 19);
                label_4.TabIndex = 6;
                label_4.Text = "条码";
                // 
                // label_5
                // 
                Label label_5 = new Label();
                label_5.AutoSize = true;
                label_5.Font = new System.Drawing.Font("宋体", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label_5.Location = new System.Drawing.Point(2, 153);
                label_5.Name = "label_5_" + num;
                label_5.Size = new System.Drawing.Size(47, 19);
                label_5.TabIndex = 7;
                label_5.Text = "型号";
                // 
                // label_6
                // 
                Label label_6 = new Label();
                label_6.AutoSize = true;
                label_6.Font = new System.Drawing.Font("宋体", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label_6.Location = new System.Drawing.Point(2, 188);
                label_6.Name = "label_6_" + num;
                label_6.Size = new System.Drawing.Size(47, 19);
                label_6.TabIndex = 8;
                label_6.Text = "批号";
                // 
                // textBox_3
                // 
                TextBox textBox_3 = new TextBox();
                textBox_3.Name = "textBox_3_" + num;
                textBox_3.Location = new System.Drawing.Point(67, 113);
                textBox_3.Size = new System.Drawing.Size(200, 26);
                textBox_3.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                textBox_3.ReadOnly = true;
                textBox_3.TabIndex = 9;
                // 
                // textBox_4
                // 
                TextBox textBox_4 = new TextBox();
                textBox_4.Name = "textBox_4_" + num;
                textBox_4.Location = new System.Drawing.Point(67, 148);
                textBox_4.Size = new System.Drawing.Size(200, 26);
                textBox_4.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                textBox_4.ReadOnly = true;
                textBox_4.TabIndex = 10;
                // 
                // textBox_5
                // 
                TextBox textBox_5 = new TextBox();
                textBox_5.Name = "textBox_5_" + num;
                textBox_5.Location = new System.Drawing.Point(67, 183);
                textBox_5.Size = new System.Drawing.Size(200, 26);
                textBox_5.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                textBox_5.ReadOnly = true;
                textBox_5.TabIndex = 11;
                // 
                // label_7
                // 
                Label label_7 = new Label();
                label_7.AutoSize = true;
                label_7.Font = new System.Drawing.Font("宋体", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
                label_7.Location = new System.Drawing.Point(2, 223);
                label_7.Name = "label_7_" + num;
                label_7.Size = new System.Drawing.Size(47, 19);
                label_7.TabIndex = 12;
                label_7.Text = "状态：";

                panel.Controls.Add(textBox_1);
                panel.Controls.Add(textBox_2);
                panel.Controls.Add(textBox_3);
                panel.Controls.Add(textBox_4);
                panel.Controls.Add(textBox_5);
                panel.Controls.Add(label_1);
                panel.Controls.Add(label_2);
                panel.Controls.Add(label_3);
                panel.Controls.Add(label_4);
                panel.Controls.Add(label_5);
                panel.Controls.Add(label_6);
                panel.Controls.Add(label_7);
                panel.Controls.Add(myLED_1);
                panel.Location = new System.Drawing.Point(3, 3);
                panel.Name = "pane_1_" + num;
                panel.Size = new System.Drawing.Size(277, 290);
                panel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
                panel.TabIndex = 0;

                this.flowLayoutPanel1.Controls.Add(panel);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        /// <summary>
        /// 清除报警日志事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            this.texterror.Items.Clear();
        }

        private void flowLayoutPanel1_Paint(object sender, PaintEventArgs e)
        {

        }
        /// <summary>
        /// 刷新
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            _ = ReadListBoxtoText();
        }
        /// <summary>
        /// 读取日志文本到 ListBox（texterror 控件）。
        ///
        /// 【本次改动】
        /// 1. catch 里 errtext = 改为 AddLog（修复日志黑洞）
        /// 2. 把"读文件（后台）"和"操作 ListBox（UI 线程）"分离，
        ///    消除依赖 CheckForIllegalCrossThreadCalls = false 的隐患。
        ///
        /// 【流程】
        /// 1. Task.Run 里只做 ReadLogReversed（纯文件读取，不碰 UI）
        /// 2. 回 UI 线程操作 texterror.Items
        /// </summary>
        private async Task ReadListBoxtoText()
        {
            try
            {
                // ========== 后台线程：只读文件，不碰 UI ==========
                var logs = await Task.Run(() => writeReadLog.ReadLogReversed(60));

                // ========== 回 UI 线程操作 ListBox ==========
                if (this.texterror.InvokeRequired)
                {
                    this.texterror.Invoke((MethodInvoker)delegate
                    {
                        this.texterror.Items.Clear();
                        foreach (var log in logs) this.texterror.Items.Add(log);
                        if (this.texterror.Items.Count > 0) this.texterror.SelectedIndex = 0;
                    });
                }
                else
                {
                    this.texterror.Items.Clear();
                    foreach (var log in logs) this.texterror.Items.Add(log);
                    if (this.texterror.Items.Count > 0) this.texterror.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                publicAddress.AddLog($"productionPlanForm错误：读取文本到listbox信息出错【{ex.Message}】!");
            }
        }
    }
}
