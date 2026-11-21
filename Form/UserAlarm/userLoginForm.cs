using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using generaControlProcedure;
using fynComClass;
using ywb_USB;

namespace generaControlProcedure
{
    public partial class userLoginForm : Form
    {
        //Form form = null;
        string type;
        SQLToolsYY sqltoolsyy = new SQLToolsYY();
        //SQLTools sqltoolsyy = new SQLTools();
        Hint hint = new Hint();
        ComClass cc = new ComClass();
        public string name;
        public static int flag=0;
        public int islink = 0;  //0--usb  1--串口
        BardCodeHooK BarCode = new BardCodeHooK();
        private delegate void ShowInfoDelegate(BardCodeHooK.BarCodes barCode);
        public userLoginForm(string types)
        {
            InitializeComponent();
            this.label3.Text = "";
            type = types;
            Control.CheckForIllegalCrossThreadCalls = false;
            //获取配置信息
            int id = 1;
            WriteReadXML xml = new WriteReadXML();
            DataTable dt = xml.selectcoordinate("SetPort.xml", "id=" + id.ToString());
            islink = int.Parse(dt.Rows[0]["islink"].ToString());
            if (islink == 0)
            {
                BarCode.BarCodeEvent += new BardCodeHooK.BardCodeDeletegate(BarCode_BarCodeEvent);
                BarCode.Start();
            }
            else
            {
                port();
            }
        }

        /// <summary>
        /// usb扫描
        /// </summary>
        /// <param name="barCode"></param>
        void BarCode_BarCodeEvent(BardCodeHooK.BarCodes barCode)
        {
            ShowInfo(barCode);
        }
        private void ShowInfo(BardCodeHooK.BarCodes barCode)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new ShowInfoDelegate(ShowInfo), new object[] { barCode });
            }
            else
            {
                string USBRead = barCode.IsValid ? barCode.BarCode : "";//是否为扫描枪输入，如果为true则是 否则为键盘输入
                if(USBRead.Length>=10)
                {
                    label4.Text = USBRead;
                    logincard(USBRead);
                }
            }
        }
        /// <summary>
        /// 手动登录按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Lojinbutton_Click(object sender, EventArgs e)
        {
            if (this.button1.Text == "切换至刷卡登录")
            {
                //if (this.comboBox1.Text == "admin" && this.textpassword.Text == "admin")
                //{
                //    publicAddress.loginName = this.comboBox1.Text;
                //    this.DialogResult = DialogResult.OK;
                //}
                //else
                //{
                    this.label3.Text = "";
                    string sqlstring = "select * from  fynV_getUserRole where jurisdictionEncoding like '" + type + "%' and jurisdiction=1 and jobNumber=" + this.comboBox1.Text + " and password='" + this.textpassword.Text + "'";
                    DataTable dt = new DataTable();
                    hint = sqltoolsyy.GetDataTable("", sqlstring, true, out dt);
                    if (hint.Valid != false && dt.Rows.Count > 0)
                    {
                        publicAddress.loginName = dt.Rows[0]["name"].ToString();
                        this.DialogResult = DialogResult.OK;
                        comboBox1.Items.Add(this.comboBox1.Text);

                        //获得登录用户名
                        string namesql = "select * from fynV_getUserRole where jobNumber='" + this.comboBox1.Text + "'";
                        DataTable dt3;
                        hint = sqltoolsyy.GetDataTable("", namesql, true, out dt3);
                        name = dt3.Rows[0][1].ToString();
                        publicAddress.loginName = name;
                        publicAddress.loginData = dt3;
                        this.DialogResult = DialogResult.OK;
                        flag = 1;
                    }
                    else
                    {
                        this.label3.Text = "用户名或密码错误";
                    }
               // }
            }
    }
        /// <summary>
        /// 隐藏密码为*
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void pictureBox1_Click(object sender, EventArgs e)
        {
            if (this.textpassword.PasswordChar == '*')
            {
                this.textpassword.PasswordChar = new char();
            }
            else
            {
                this.textpassword.PasswordChar = '*';
            }
        }
        /// <summary>
        /// 刷卡手输切换按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e)
        {
            if (this.button1.Text == "切换至刷卡登录")
            {
                button1.Text = "切换至密码登录";
                this.label1.Visible = false;
                this.comboBox1.Visible = false;
                this.label2.Visible = false;
                this.textpassword.Visible = false;
                this.pictureBox1.Visible = false;
                this.Lojinbutton.Visible = false;
                //button1.Image = MeasurementInstrument.Properties.Resources.settings;
                this.label3.Text = "";
                pictureBox2.Visible = true;
                label4.Visible = true;
                label4.Text = "";
            }
            else if (this.button1.Text == "切换至密码登录")
            {
                button1.Text = "切换至刷卡登录";
                this.label1.Visible = true;
                this.comboBox1.Visible = true;
                this.label2.Visible = true;
                this.textpassword.Visible = true;
                this.pictureBox1.Visible = true;
                this.Lojinbutton.Visible = true;
                //button1.Image = MeasurementInstrument.Properties.Resources.user;
                this.label3.Text = "";
                pictureBox2.Visible = false;
                label4.Visible = false;
            }
        }
        /// <summary>
        /// 刷卡登录判断
        /// </summary>
        /// <param name="loginId">卡号</param>
        /// <returns></returns>
        private void logincard(string loginId)
        {
            string strlogin = "select * from  fynV_getUserRole where jurisdictionEncoding like '" + type + "%' and jurisdiction=1 and card=" + loginId + " ";
            DataTable dt;
            hint = sqltoolsyy.GetDataTable("", strlogin, true, out dt);
            if (dt.Rows.Count > 0)
            {
                label3.Visible = true;
                label3.Text = "登录成功";
                this.DialogResult = DialogResult.OK;
                this.BeginInvoke((EventHandler)delegate
                {
                    //cc.CloseCom();
                });
                string namesql = "select * from  fynV_getUserRole where jurisdictionEncoding like '" + type + "%' and jurisdiction=1 and card=" + loginId + " ";
                DataTable dt3;
                hint = sqltoolsyy.GetDataTable("", namesql, true, out dt3);

                name = dt3.Rows[0][1].ToString();
                publicAddress.loginName = name;
                publicAddress.loginData = dt3;
                this.DialogResult = DialogResult.OK;
                //flag = 1;
            }
            else
            {
                label3.Visible = true;
                label3.Text = "登录失败:卡号错误或未登记";
                //return false;
            }
        }
        /// <summary>
        /// 执行扫描程序，接收数据
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Cc_DataRead(object sender, CustomerEventArgs e)
        {
            if (this.button1.Text == "切换至密码登录")
            {
                this.Invoke((EventHandler)delegate
                {
                    //label4.Text = int.Parse(e.ReadCode.Substring(1, 10)).ToString();
                    label4.Text = e.ReadCode.TrimStart('0');
                    logincard(label4.Text);
                });
            }
        }
        /// <summary>
        /// 关闭窗口执行事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void userLoginForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (islink == 0)
            {
                BarCode.Stop();
            }
            else
            {
                cc.CloseCom();
            }
        }
        /// <summary>
        /// 加载窗口执行事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void userLoginForm_Load_1(object sender, EventArgs e)
        {
            button1.Text = "切换至密码登录";
            this.label1.Visible = false;
            this.comboBox1.Visible = false;
            this.label2.Visible = false;
            this.textpassword.Visible = false;
            this.pictureBox1.Visible = false;
            this.Lojinbutton.Visible = false;
            this.label3.Text = "";
            pictureBox2.Visible = true;
            label4.Visible = true;
            label4.Text = "";
        }
        /// <summary>
        /// 获取串口信息和刷卡密码切换功能
        /// </summary>
        private void port()
        {
            WriteReadXML selectXML = new WriteReadXML();
            DataTable dt = selectXML.selectcoordinate("SetPort.xml","id=1");
            switch(dt.Rows[0][5].ToString())
            {
                case "奇":
                    cc.openCom(dt.Rows[0][1].ToString(), dt.Rows[0][2].ToString(), dt.Rows[0][3].ToString(), dt.Rows[0][4].ToString(), ComClass.ParityBits.奇);
                    break;
                case "偶":
                    cc.openCom(dt.Rows[0][1].ToString(), dt.Rows[0][2].ToString(), dt.Rows[0][3].ToString(), dt.Rows[0][4].ToString(), ComClass.ParityBits.偶);
                    break;
                case "无":
                    cc.openCom(dt.Rows[0][1].ToString(), dt.Rows[0][2].ToString(), dt.Rows[0][3].ToString(), dt.Rows[0][4].ToString(), ComClass.ParityBits.无);
                    break;
            }
            switch(dt.Rows[0][6].ToString())
            {
                case "true":
                    button1.Visible = true;
                    break;
                case "false":
                    button1.Visible = false;
                    break;
            }
            cc.DataRead += Cc_DataRead;
        }
    }
}
