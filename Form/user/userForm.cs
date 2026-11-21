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
    public partial class userForm : Form
    {
       
        public userForm()
        {
            InitializeComponent();
        }
        SQLToolsYY sqlToolsYY = new SQLToolsYY();
        DataGridView dgv = new DataGridView();
        private void userForm_Load(object sender, EventArgs e)
        {
            userRefresh();//刷新用户表
            getroleNameItems();//添加角色
            /*添加DataGridView 控件*/
            //this.Controls.Add(dgv);


        }
        /// <summary>
        /// 刷新用户表
        /// </summary>
        private void userRefresh()
        {
            string sqlstr = "select * from fynV_user";
            DataTable dt = new DataTable();
            Hint hint = sqlToolsYY.GetDataTable("", sqlstr, true, out dt);
            if (hint.Valid == false)
                MessageBox.Show("错误：" + hint.Msg);
            this.userdataGridView.DataSource = dt.DefaultView;
        }
        /// <summary>
        /// 获取角色名添加到combobox下拉单中
        /// </summary>
        private void getroleNameItems()
        {
            string sqlstr = " select * from fynT_role";
            DataTable dt = new DataTable();
            Hint hint = sqlToolsYY.GetDataTable("", sqlstr, true, out dt);
            if (hint.Valid == false)
                MessageBox.Show("错误：" + hint.Msg);
            this.roleName.Items.Clear();
            for(int i=0;i<dt.Rows.Count;i++)
            {
                this.roleName.Items.Add(dt.Rows[i]["roleName"].ToString());
            }
        }

        private void userbutton_Click(object sender, EventArgs e)
        {
            string str = null;
            if (this.name.Text != "" && this.jobNumber.Text != "" && this.card.Text != "" && this.roleName.Text != "")
            {
                str = "exec fynP_setuser '" + this.name.Text + "','" + this.card.Text + "','" + this.jobNumber.Text + "','" + this.password.Text + "','" + this.roleName.Text + "'";
                DataTable dt = new DataTable();
                Hint hint = sqlToolsYY.GetDataTable("", str, true, out dt);
                if (hint.Valid == false)
                    MessageBox.Show("错误：" + hint.Msg);
                userRefresh();
            }
        }
    }
}
