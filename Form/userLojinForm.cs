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

namespace generaControlProcedure
{
    public partial class userLojinForm : Form
    {
        string type;
        SQLToolsYY sqltoolsyy = new SQLToolsYY();
        Hint hint = new Hint();
        public userLojinForm(string types)
        {
            InitializeComponent();
            this.label3.Text = "";
            type = types;
        }
        
        /// <summary>
        /// 登录按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Lojinbutton_Click(object sender, EventArgs e)
        {
            if (this.comboBox1.Text == "admin" && this.textpassword.Text == "admin")
            {
                publicAddress.loginName ="admin" ;
                this.DialogResult = DialogResult.OK;
            }
            this.label3.Text = "";
            string sqlstring = "select * from  fynV_getUserRole where jurisdictionEncoding like '"+ type + "%' and jurisdiction=1 and jobNumber=" + this.comboBox1.Text+" and password='"+this.textpassword.Text+"'";
            DataTable dt = new DataTable();
            hint = sqltoolsyy.GetDataTable("", sqlstring, true, out dt);
            if (hint.Valid != false && dt.Rows.Count > 0)
            {
                publicAddress.loginData = dt;
                this.DialogResult = DialogResult.OK;
            }
            else
            {
                this.label3.Text = "用户名或密码错误";
            }
            
        }

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
    }
}
