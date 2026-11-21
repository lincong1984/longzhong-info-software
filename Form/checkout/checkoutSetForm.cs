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
    public partial class checkoutSetForm : Form
    {
        SQLToolsYY sql = new SQLToolsYY();
        Hint hint = new Hint();
        public checkoutSetForm()
        {
            InitializeComponent();
        }
        /// <summary>
        /// 窗体加载时
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkoutSetForm_Load(object sender, EventArgs e)
        {
            refresh();
        }
        /// <summary>
        /// 刷新表格
        /// </summary>
        private void refresh()
        {
            string sqlstr = @"SELECT * FROM fynT_checkoutSet";
            DataTable dt = new DataTable();
            hint = sql.GetDataTable("", sqlstr, true, out dt);
            if (hint.Valid != false)
            {
                this.dataGridView1.DataSource = dt;
                this.dataGridView1.Columns["intOprNo"].HeaderText = "工序号";
                this.dataGridView1.Columns["checkoutQty"].HeaderText = "检验数量";
                this.dataGridView1.ReadOnly = true;
                this.dataGridView1.AllowUserToAddRows = false;
                this.dataGridView1.AllowUserToDeleteRows = false;
                this.dataGridView1.Columns[0].Width = 80;
            }
            
        }
        /// <summary>
        /// 添加行
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripButton4_Click(object sender, EventArgs e)
        {
            this.dataGridView1.AllowUserToAddRows = true;
            ((DataTable)this.dataGridView1.DataSource).Rows.Add();

        }
        /// <summary>
        /// 保存按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            save();
        }
        /// <summary>
        /// 修改按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            this.dataGridView1.ReadOnly = false;
        }
        /// <summary>
        /// 删除按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripButton3_Click(object sender, EventArgs e)
        {
            int index = this.dataGridView1.CurrentCell.RowIndex;
            ((DataTable)this.dataGridView1.DataSource).Rows[index].Delete();
            save();
        }
        /// <summary>
        /// 保存
        /// </summary>
        private void save()
        {
            DataTable dt = new DataTable();
            dt = this.dataGridView1.DataSource as DataTable;
            hint = sql.updateTable("fynT_checkoutSet", dt);
            if (hint.Valid == false)
                MessageBox.Show(hint.Msg);
            //sql.SqlInsertInto(dt,"fynT_checkoutProject");
            refresh();//刷新表格
        }
    }
}
