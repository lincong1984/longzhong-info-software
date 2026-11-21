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
    public partial class checkoutIndexForm : Form
    {
        public checkoutIndexForm()
        {
            InitializeComponent();
            
        }
        SQLToolsYY sql = new SQLToolsYY();
        Hint hint = new Hint();
        DataGridView dv = new DataGridView();
        int colIndex = 0, rowIndex = 0;//定义行、列的索引
        private void checkoutIndexForm_Load(object sender, EventArgs e)
        {
            refresh();
            this.Controls.Add(dv);
            this.dv.CellDoubleClick += Dv_CellDoubleClick;//添加双击单元格事件
            this.dv.Visible = false;
        }
        /// <summary>
        /// 双击单元格时发生
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Dv_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            string selectValue = "";
            string colname = this.dataGridView1.Columns[colIndex].Name;
            if (colname == "equipmentCode")
            {
                selectValue = this.dv.Rows[e.RowIndex].Cells["equipmentCode"].Value.ToString();
            }
            if (colname == "checkoutProjectID")
            {
                selectValue = this.dv.Rows[e.RowIndex].Cells["ID"].Value.ToString();
            }
            this.dv.Visible = false;
            //this.dataGridView1.Rows[rowIndex].Cells[colIndex].Value = selectValue;
            if (rowIndex == this.dataGridView1.Rows.Count - 1)
            {
                ((DataTable)this.dataGridView1.DataSource).Rows.Add();
            }
            //this.dataGridView1.RefreshEdit();
            this.dataGridView1.Rows[rowIndex].Cells[colIndex].Value = selectValue;
        }
        /// <summary>
        /// 添加按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            this.dataGridView1.AllowUserToAddRows = true;
            //((DataTable)this.dataGridView1.DataSource).Rows.Add();
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

        private void toolStripButton5_Click(object sender, EventArgs e)
        {
            ((DataTable)this.dataGridView1.DataSource).Rows.Add();

        }
        /// <summary>
        /// 双击单元格时执行
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cellContentDouble(object sender, DataGridViewCellEventArgs e)
        {
            colIndex = e.ColumnIndex;//获取选择单元格列的索引
            rowIndex = e.RowIndex;//获取选择单元格行的索引
            if (colIndex >= 0 && rowIndex >= 0 && this.dataGridView1.Rows[rowIndex].ReadOnly!=true)
            {
                string value = this.dataGridView1.Rows[rowIndex].Cells[colIndex].Value.ToString();
                string colname = this.dataGridView1.Columns[colIndex].Name;
                if (colname == "equipmentCode" || colname == "checkoutProjectID")
                {
                    if (colname == "equipmentCode")
                    {
                        string str = "select * from fynT_equipment where 1=1";
                        if (value != "")
                        {
                            str = str + "and equipmentCode='" + value + "'";
                        }
                        DataTable dt = new DataTable();
                        hint = sql.GetDataTable("", str, true, out dt);
                        if (hint.Valid != false)
                        {
                            this.dv.DataSource = dt;
                            this.dv.Columns[0].Width = 80;
                            this.dv.Columns["equipmentName"].HeaderText = "设备名称";
                            this.dv.Columns["equipmentCode"].HeaderText = "设备编号";
                            this.dv.Columns["intOprNo"].HeaderText = "工序号";
                        }
                    }
                    if (colname == "checkoutProjectID")
                    {
                        string str = "select * from fynT_checkoutProject where 1=1";
                        if (value != "")
                        {
                            str = str + " and ID=" + value;
                        }
                        DataTable dt = new DataTable();
                        hint = sql.GetDataTable("", str, true, out dt);
                        if (hint.Valid != false)
                        {
                            this.dv.DataSource = dt;
                            this.dv.Columns[0].Width = 80;
                            this.dv.Columns["checkoutType"].HeaderText = "检验类型";
                            this.dv.Columns["checkoutProject"].HeaderText = "检验项目";
                            this.dv.Columns["type"].HeaderText = "判定类型";
                            this.dv.Columns["lowerValue"].HeaderText = "下限值";
                            this.dv.Columns["upperValue"].HeaderText = "上限值";
                            this.dv.Columns["unit"].HeaderText = "单位";
                        }
                    }
                    this.dv.AllowUserToAddRows = false;//设定为也允许添加行
                    this.dv.AllowUserToDeleteRows = false;//设定为允许删除行
                    this.dv.ReadOnly = true;
                    this.dv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                    this.dv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    Rectangle rect = this.dataGridView1.GetCellDisplayRectangle(colIndex, rowIndex, false);//获取单元格的位置
                    dv.Left = dataGridView1.Left + 10 / 2;// rect.Left +
                    dv.Top = rect.Top + dataGridView1.Top + rect.Height * 2;
                    dv.Width = dataGridView1.Width - 10;
                    dv.Height = rect.Height * this.dv.Rows.Count + rect.Height;
                    dv.Visible = true;
                    dv.BringToFront();//将空间排列到最前面
                }
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
            dt = this.dataGridView1.DataSource as DataTable;
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                if (dt.Rows[i]["equipmentCode"].ToString() == "" && dt.Rows[i]["checkoutProjectID"].ToString() == "")
                {
                    dt.Rows.RemoveAt(i);
                }
            }
            hint = sql.updateTable("fynT_checkoutIndex", dt);
            if (hint.Valid == false)
                MessageBox.Show(hint.Msg);
            //sql.SqlInsertInto(dt,"fynT_checkoutProject");
            refresh();//刷新表格
        }
        /// <summary>
        /// 单击单元格时发生
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cellClick(object sender, DataGridViewCellEventArgs e)
        {
            this.dv.Visible = false;
        }

        /// <summary>
        /// 刷新按钮
        /// </summary>
        private void refresh()
        {
            string str = "select * from fynT_checkoutIndex";
            DataTable dt = new DataTable();
            hint = sql.GetDataTable("", str, true, out dt);
            //dt.Rows.Add();
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
            this.dataGridView1.Columns["equipmentCode"].HeaderText = "设备编号";
            this.dataGridView1.Columns["checkoutProjectID"].HeaderText = "检验项目ID";

        }

    }
}
