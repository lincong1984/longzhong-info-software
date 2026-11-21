using fyn_AutoSizeForm;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace generaControlProcedure
{
    public partial class communicationForm : Form
    {

        public static communicationForm f2;
        AutoSizeFormClass autoForm = new AutoSizeFormClass();
        Hint hint = new Hint();
        SQLToolsYY sql = new SQLToolsYY();
        public communicationForm()
        {
            InitializeComponent();
            CheckForIllegalCrossThreadCalls = false;
            f2 = this;
            this.SizeChanged += MaiForm_SizeChanged;
        }
        /// <summary>
        /// Size属性改变时执行
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MaiForm_SizeChanged(object sender, EventArgs e)
        {
        }
        /// <summary>
        /// 窗体加载时运行
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void communicationForm_Load(object sender, EventArgs e)
        {
            //autoForm.ControlAutoSize(this);//控件更具分辨率变化
            DataTable dt = new DataTable();
            hint = sql.GetDataTable("", "SELECT * FROM [ConsoleSQL].[dbo].[fynT_threadXML]", true, out dt);
            for (int i = 0; i < dt.Rows.Count - 1; i++)//dt.Rows.Count-1
            {
                addrow();
            }
            //清除IP地址
            
            Control control = new Control();
            for (int j = 1; j < tableLayoutPanel1.RowCount; j++)
            {
                for (int i = 0; i < 5; i++)
                {
                    

                        //this.TrafficStatus_1.BackColor = Color.Yellow;
                        control = Controls.Find("TrafficStatus_" + j.ToString(), true)[0];
                        control.BackColor = Color.Yellow;
                    
                }
            }
            
        }
        /// <summary>
        /// 定时器
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void timer1_Tick(object sender, EventArgs e)
        {
            
            this.errortext.Text = publicAddress.errtext;
            Control control = new Control();
            for (int j = 1; j < tableLayoutPanel1.RowCount; j++)
            {
                for (int i = 0; i < 5; i++)
                {
                    if (publicAddress.TrafficStatus[j - 1, i] != null)
                    {
                        switch (i)
                        {
                            case 0:
                                //this.TrafficStatus_1.Text = publicAddress.TrafficStatus[1, i];
                                control= Controls.Find("TrafficStatus_" + j.ToString(), true)[0];
                                control.Text = publicAddress.TrafficStatus[j - 1, i];
                                break;
                            case 1:
                                switch (publicAddress.TrafficStatus[j - 1, i])
                                {
                                    case "Color.Yellow":
                                        //this.TrafficStatus_1.BackColor = Color.Yellow;
                                        control = Controls.Find("TrafficStatus_" + j.ToString(), true)[0];
                                        control.BackColor = Color.Yellow;
                                        break;
                                    case "Color.Lime":
                                        //this.TrafficStatus_1.BackColor = Color.Lime;
                                        control = Controls.Find("TrafficStatus_" + j.ToString(), true)[0];
                                        control.BackColor = Color.Lime;
                                        break;
                                    case "Color.Red":
                                        //this.TrafficStatus_1.BackColor = Color.Red;
                                        control = Controls.Find("TrafficStatus_" + j.ToString(), true)[0];
                                        control.BackColor = Color.Red;
                                        break;
                                }
                                break;
                            case 2:
                                //this.HeartBeat_1.Text = publicAddress.TrafficStatus[j-1, i];
                                control = Controls.Find("HeartBeat_" + j.ToString(), true)[0];
                                control.Text = publicAddress.TrafficStatus[j - 1, i];
                                break;
                            case 3:
                                //this.IPport_1.Text = publicAddress.TrafficStatus[j-1, i];
                                control = Controls.Find("IPport_" + j.ToString(), true)[0];
                                control.Text = publicAddress.TrafficStatus[j - 1, i];
                                break;
                            case 4:
                                //this.IPport_1.Text = publicAddress.TrafficStatus[j-1, i];
                                control = Controls.Find("name_" + j.ToString(), true)[0];
                                control.Text = publicAddress.TrafficStatus[j - 1, i];
                                break;
                        }
                    }
                }
            }
        }
        /// <summary>
        /// 添加行
        /// </summary>
        private void addrow()
        {
            // 动态添加一行
            tableLayoutPanel1.RowCount++;
            //设置高度
            tableLayoutPanel1.Height = tableLayoutPanel1.RowCount * 35;
            // 行高
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 35));

            // 设置cell样式，
            //tableLayoutPanel1.CellBorderStyle = TableLayoutPanelCellBorderStyle.Single;

            int i = tableLayoutPanel1.RowCount - 1;
            // 添加控件
            Label p = new Label();
            p.Width = Convert.ToInt32(tableLayoutPanel1.ColumnStyles[0].Width);
            p.Height = 25;
            //p.Dock = DockStyle.Fill;
            p.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            p.BackColor = Color.Yellow;
            p.Name = "TrafficStatus_" + i.ToString();
            p.Font = new Font("宋体", 15.75f);
            p.TextAlign = ContentAlignment.MiddleCenter;
            tableLayoutPanel1.Controls.Add(p, 0, i);
            p.Text = "检测中...";

            Label p1 = new Label();
            p1.Width = Convert.ToInt32(tableLayoutPanel1.ColumnStyles[2].Width);
            p1.Height = 25;
            //p.Dock = DockStyle.Fill;
            p1.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            p1.BackColor = Color.White;
            p1.Name = "name_" + i.ToString();
            p1.Font = new Font("宋体", 15.75f);
            p1.TextAlign = ContentAlignment.MiddleLeft;
            p1.BorderStyle = BorderStyle.FixedSingle;
            tableLayoutPanel1.Controls.Add(p1, 2, i);
            p1.Text = "";

            Label p2 = new Label();
            p2.Width = Convert.ToInt32(tableLayoutPanel1.ColumnStyles[4].Width);
            p2.Height = 25;
            //p.Dock = DockStyle.Fill;
            p2.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            p2.BackColor = Color.White;
            p2.Name = "IPport_" + i.ToString();
            p2.Font = new Font("宋体", 15.75f);
            p2.TextAlign = ContentAlignment.MiddleLeft;
            p2.BorderStyle = BorderStyle.FixedSingle;
            tableLayoutPanel1.Controls.Add(p2, 4, i);
            p2.Text = "";

            Label p3 = new Label();
            p3.Width = Convert.ToInt32(tableLayoutPanel1.ColumnStyles[6].Width);
            p3.Height = 25;
            //p.Dock = DockStyle.Fill;
            p3.Anchor =AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            p3.BackColor = Color.White;
            p3.Name = "HeartBeat_" + i.ToString();
            p3.Font = new Font("宋体", 15.75f);
            p3.TextAlign = ContentAlignment.MiddleLeft;
            p3.BorderStyle = BorderStyle.FixedSingle;
            tableLayoutPanel1.Controls.Add(p3, 6, i);
            p3.Text = "";

            /*
            TextBox name = new TextBox();
            name.Text = "name-" + i;
            name.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            name.TextAlign = HorizontalAlignment.Center;
            table.Controls.Add(name, 0, i);

            TextBox inc = new TextBox();
            inc.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            inc.TextAlign = HorizontalAlignment.Center;
            inc.Text = "in-" + i;
            table.Controls.Add(inc, 2, i);

            TextBox outc = new TextBox();
            outc.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            outc.TextAlign = HorizontalAlignment.Center;
            outc.Text = "out-" + i;
            table.Controls.Add(outc, 3, i);
            */
        }
        /// <summary>
        /// 报警清除
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e)
        {
            publicAddress.errtext = "";
        }

    }
}
