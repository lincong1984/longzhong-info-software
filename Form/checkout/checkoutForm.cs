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
    public partial class checkoutForm : Form
    {
        public checkoutForm()
        {
            InitializeComponent();
        }
        SQLToolsYY sql = new SQLToolsYY();
        Hint hint = new Hint();
        /// <summary>
        /// 窗体加载时运行
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkoutForm_Load(object sender, EventArgs e)
        {
            refresh();//刷新
        }
        /// <summary>
        /// 刷新页面
        /// </summary>
        private void refresh()
        {
            string str = "select * from fynT_checkoutProject";
            DataTable dt = new DataTable();
            hint = sql.GetDataTable("", str, true, out dt);
            if (hint.Valid != false)
            {
                this.dataGridView1.DataSource = dt;
            }
            for (int i = 0; i < this.dataGridView1.Rows.Count; i++)
            {
                this.dataGridView1.Rows[i].ReadOnly = true;//将行设定为禁止编辑
            }
            this.dataGridView1.AllowUserToAddRows = false;//设定为也允许添加行
            this.dataGridView1.AllowUserToDeleteRows = false;//设定为允许删除行
            //this.dataGridView1.Columns[0].Width = 80;
            //this.dataGridView1.Columns["checkoutType"].HeaderText = "检验类型";
            //this.dataGridView1.Columns["checkoutProject"].HeaderText = "检验项目";
            //this.dataGridView1.Columns["type"].HeaderText = "判定类型";
            //this.dataGridView1.Columns["lowerValue"].HeaderText = "下限值";
            //this.dataGridView1.Columns["upperValue"].HeaderText = "上限值";
            //this.dataGridView1.Columns["unit"].HeaderText = "单位";
            //DataGridViewComboBoxColumn a = new DataGridViewComboBoxColumn();

          
            ////a.DataSource = dt;
            //a.HeaderText = "判定类型";
            //a.Name = "type_ex";
            //a.Items.Add("定量");
            //a.Items.Add("定性");
            //// a.ValueMember = "type";
            ////a.DataPropertyName = "type";

            //this.dataGridView1.Columns.Insert(3, a);
            //for (int i = 0; i < dt.Rows.Count; i++)
            //{
            //    this.dataGridView1.Rows[i].Cells[3].Value = dt.Rows[i]["type"];
            //}
            
           
           
           // a.DisplayIndex = 3;
            
            
            //this.dataGridView1.DataBindings.Add("text", this.dataGridView1, "type.text", false, DataSourceUpdateMode.Never);

        }
        /// <summary>
        /// 添加行按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            this.dataGridView1.AllowUserToAddRows = true;
        }
        /// <summary>
        /// 删除按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripButton3_Click(object sender, EventArgs e)
        {
            this.dataGridView1.AllowUserToDeleteRows = true;
            int row = this.dataGridView1.CurrentCell.RowIndex;
            this.dataGridView1.Rows.RemoveAt(row);//移除指定的行
        }
        /// <summary>
        /// 修改按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripButton4_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < this.dataGridView1.Rows.Count; i++)
            {
                this.dataGridView1.Rows[i].ReadOnly = false;
            }
        }
        /// <summary>
        /// 保存按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            DataTable dt = new DataTable();
            dt=this.dataGridView1.DataSource as DataTable;
            hint = sql.updateTable("fynT_checkoutProject", dt);
            if (hint.Valid == false)
                MessageBox.Show(hint.Msg);
            //sql.SqlInsertInto(dt,"fynT_checkoutProject");
            refresh();//刷新表格
        }
    }
}
