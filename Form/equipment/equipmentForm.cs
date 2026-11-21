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
    public partial class equipmentForm : Form
    {
        public equipmentForm()
        {
            InitializeComponent();
        }
        SQLToolsYY sql = new SQLToolsYY();
        Hint hint = new Hint();
        /// <summary>
        /// 窗口加载时运行
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void equipmentForm_Load(object sender, EventArgs e)
        {
            refresh();
        }
        /// <summary>
        /// 刷新画面
        /// </summary>
        private void refresh()
        {
            string str = "select * from fynT_equipment";
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
            this.dataGridView1.Columns[0].Width = 80;
            this.dataGridView1.Columns["equipmentName"].HeaderText = "设备名称";
            this.dataGridView1.Columns["equipmentCode"].HeaderText = "设备编号";
            this.dataGridView1.Columns["intOprNo"].HeaderText = "工序号";

        }
        /// <summary>
        /// 添加
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            this.dataGridView1.AllowUserToAddRows = true;
        }
        /// <summary>
        /// 修改按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripButton3_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < this.dataGridView1.Rows.Count; i++)
            {
                this.dataGridView1.Rows[i].ReadOnly = false;
            }
        }
        /// <summary>
        /// 删除按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripButton4_Click(object sender, EventArgs e)
        {
            this.dataGridView1.AllowUserToDeleteRows = true;
            int row = this.dataGridView1.CurrentCell.RowIndex;
            this.dataGridView1.Rows.RemoveAt(row);//移除指定的行
        }
        /// <summary>
        /// 保存按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            DataTable dt = new DataTable();
            dt = this.dataGridView1.DataSource as DataTable;
            hint = sql.updateTable("fynT_equipment", dt);
            if (hint.Valid == false)
                MessageBox.Show(hint.Msg);
            //sql.SqlInsertInto(dt,"fynT_checkoutProject");
            refresh();//刷新表格
        }
    }
}
