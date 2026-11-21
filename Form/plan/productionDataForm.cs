using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace generaControlProcedure
{
    public partial class productionDataForm : Form
    {
        DataGridView dgv = new DataGridView();
        public productionDataForm()
        {
            InitializeComponent();
            dgv.ColumnAdded += Dgv_ColumnAdded;
        }

        
        SQLToolsYY sqltoolsyy = new SQLToolsYY();
        Hint hint = new Hint();
        DataGridView dv = new DataGridView();
        
        int colIndex = 0, rowIndex = 0;//定义行、列的索引
        /// <summary>
        /// 刷新按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            string sql = @"EXEC fynP_getCourseData 'where 1=1";
            foreach (var item in this.listBox1.Items)
            {
                sql += " and " + item.ToString();
            }
            DataTable dt = new DataTable();
            hint = sqltoolsyy.GetDataTable("", sql+"'", true, out dt);
            if (hint.Valid != true)
                MessageBox.Show(hint.Msg);
            this.dataGridView1.DataSource = dt;
        }
        /// <summary>
        /// 导出execl按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            WriteReadExecl.writeexecl(this.dataGridView1);
        }
        /// <summary>
        /// 窗体加载
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void productionDataForm_Load(object sender, EventArgs e)
        {
            referesh();
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
            this.dv.Visible = false;
        }
        /// <summary>
        /// 字段选择时执行
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void selectIndex(object sender, EventArgs e)
        {
            this.comboBox3.Items.Clear();
            this.comboBox3.Text = "";
            string sql = "";
            if (this.comboBox1.Text != "时间")
            {
                sql = @"select distinct " + this.comboBox1.Text + " from reduction";
            }
            else
            {
                sql = @"select distinct convert(nvarchar(50)," + this.comboBox1.Text + ",23) from reduction";
            }
            DataTable dt = new DataTable();
            hint = sqltoolsyy.GetDataTable("", sql, true, out dt);
            if (hint.Valid != true)
            {
                MessageBox.Show(hint.Msg);
            }
            else
            {
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    this.comboBox3.Items.Add(dt.Rows[i][0].ToString());
                }

            }
        }
        /// <summary>
        /// 添加条件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e)
        {
            if (this.comboBox1.Text != "" && this.comboBox2.Text != "" && this.comboBox3.Text != "")
            {
                string items= (this.comboBox2.Text=="like") ? this.comboBox1.Text + " " + this.comboBox2.Text + " ''%" + this.comboBox3.Text + "%''":this.comboBox1.Text + " " + this.comboBox2.Text + " ''" + this.comboBox3.Text + "''";
                items = this.comboBox1.Text == "时间" ? "convert(date," + this.comboBox1.Text + ") " + this.comboBox2.Text + " ''" + this.comboBox3.Text + "''" : items;
                if (!judgeitems(this.listBox1, items))
                    this.listBox1.Items.Add(items);
            }
        }
        /// <summary>
        /// 判断listbox选着项是否存在
        /// </summary>
        /// <param name="listbox"></param>
        /// <param name="items"></param>
        /// <returns></returns>
        private bool judgeitems(ListBox listbox, string items)
        {
            bool bools = false;
            foreach (var item in listbox.Items)
            {
                if (item.ToString() == items)
                {
                    bools = true;
                }
            }
            return bools;
        }
        /// <summary>
        /// 条件移除按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button2_Click(object sender, EventArgs e)
        {
            int index=this.listBox1.SelectedIndex;
            this.listBox1.Items.RemoveAt(index);
        }
        /// <summary>
        /// 清楚所有筛选条件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button3_Click(object sender, EventArgs e)
        {
            this.listBox1.Items.Clear();
        }
        /// <summary>
        /// 确定按钮，查询数据
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button5_Click(object sender, EventArgs e)
        {
            string sql = @"EXEC fynP_getCourseDatatable 'where 1=1 ";
            
            foreach (var item in this.listBox1.Items)
            {
                sql += " and " + item.ToString();
            }
            
            DataTable dt = new DataTable();
            hint = sqltoolsyy.GetDataTable("", sql+"'", true, out dt);
            if (hint.Valid != true)
                MessageBox.Show(hint.Msg);
            this.dataGridView1.DataSource = dt;
            
            for (int i = 0; i < this.dataGridView1.Rows.Count; i++)
            {
                for (int j = 5; j < this.dataGridView1.Columns.Count; j++)
                {
                    if (this.dataGridView1.Rows[i].Cells[j].Value.ToString() == "1")
                    {
                        this.dataGridView1.Rows[i].Cells[j].Style.BackColor = Color.Green;//将颜色设定为绿色
                    }
                    if (this.dataGridView1.Rows[i].Cells[j].Value.ToString() == "2")
                    {
                        this.dataGridView1.Rows[i].Cells[j].Style.BackColor = Color.Red;//将颜色设定为绿色
                    }
                }
                
            }
            for (int j = 6; j < this.dataGridView1.Columns.Count; j++)
            {
                dataGridView1.Columns[j].Width = 50;
                dataGridView1.Columns[j].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
            dataGridView1.Columns[0].Width = 80;
            dataGridView1.Columns[1].Width = 80;
            dataGridView1.Columns[2].Width = 80;
            dataGridView1.Columns[3].Width = 80;
            dataGridView1.Columns[4].Width = 50;
            dataGridView1.Columns[5].Width = 120;
            this.panel1.Visible = false;
            
        }
        /// <summary>
        /// 取消按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button4_Click(object sender, EventArgs e)
        {
            this.panel1.Visible = false;
        }
        /// <summary>
        /// 查询框显示时执行
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void visible(object sender, EventArgs e)
        {
            referesh();
        }
        /// <summary>
        /// 刷新查询画面
        /// </summary>
        private void referesh()
        {
            //this.listBox1.Items.Clear();
            this.comboBox1.Items.Clear();
            this.comboBox2.Text = "";
            this.comboBox1.Text = "";
            this.comboBox3.Text = "";
            string sql = @"select name from syscolumns where id=object_id('reduction')";
            DataTable dt = new DataTable();
            hint = sqltoolsyy.GetDataTable("", sql, true, out dt);
            if (hint.Valid != true)
            {
                MessageBox.Show(hint.Msg);
            }
            else
            {
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    this.comboBox1.Items.Add(dt.Rows[i][0].ToString());
                }
            }
            //添加判断条件
            this.comboBox2.Items.Clear();
            this.comboBox2.Items.Add("=");
            this.comboBox2.Items.Add("<>");
            this.comboBox2.Items.Add(">");
            this.comboBox2.Items.Add(">=");
            this.comboBox2.Items.Add("<");
            this.comboBox2.Items.Add("<=");
            this.comboBox2.Items.Add("like");
        }
        /// <summary>
        /// 查询按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripButton3_Click(object sender, EventArgs e)
        {
            this.panel1.Visible = true;
            this.panel3.Visible = false;
        }
        /// <summary>
        /// 查询子合格信息
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void 查询子项ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            rowIndex = this.dataGridView1.CurrentCell.RowIndex;
            colIndex = this.dataGridView1.CurrentCell.ColumnIndex;
            if (this.dataGridView1.Rows[rowIndex].Cells[colIndex].Value.ToString() != "")
            {
                string colName = this.dataGridView1.Columns[colIndex].Name;
                string code = this.dataGridView1.Rows[rowIndex].Cells["条码"].Value.ToString();
                string qty = this.dataGridView1.Rows[rowIndex].Cells["次数"].Value.ToString();
                string sqlstring = "exec fynP_getCourseDatas '" + code + "','" + colName + "',1,"+ qty;
                DataTable dts = new DataTable();
                hint = sqltoolsyy.GetDataTable("", sqlstring, true, out dts);
                if (hint.Valid == false)
                {
                    MessageBox.Show(hint.Msg);
                    return;
                }
                if (dts.Rows.Count > 0)
                {
                    this.dv.DataSource = dts;
                    this.dv.AllowUserToAddRows = false;//设定为也允许添加行
                    this.dv.AllowUserToDeleteRows = false;//设定为允许删除行
                    this.dv.ReadOnly = true;
                    this.dv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                    this.dv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    Rectangle rect = this.dataGridView1.GetCellDisplayRectangle(colIndex, rowIndex, false);//获取单元格的位置
                    if ((rect.Top + dataGridView1.Top + rect.Height * 2) < (dataGridView1.Height * 2 / 3))
                    {
                        dv.Left = dataGridView1.Left + 10 / 2;// rect.Left +
                        dv.Top = rect.Top + dataGridView1.Top + rect.Height * 2;
                        dv.Width = dataGridView1.Width - 10;
                        dv.Height = (dts.Rows.Count * rect.Height+ rect.Height) >(dataGridView1.Height - dv.Top)? dataGridView1.Height - dv.Top:dts.Rows.Count* rect.Height+rect.Height;//rect.Height * this.dv.Rows.Count + rect.Height;
                    }
                    else
                    {
                        dv.Left = dataGridView1.Left + 10 / 2;// rect.Left +
                        dv.Top = (rect.Top- (dts.Rows.Count * rect.Height))< (panel2.Top+dataGridView1.Top+rect.Height * 2)? (panel2.Top + dataGridView1.Top + rect.Height * 2): (rect.Top - (dts.Rows.Count * rect.Height));
                        dv.Width = dataGridView1.Width - 10;
                        dv.Height = rect.Top- dv.Top+ rect.Height*2;//rect.Height * this.dv.Rows.Count + rect.Height;
                    }
                    
                    dv.Visible = true;
                    dv.BringToFront();//将空间排列到最前面
                }
            }
        }
        /// <summary>
        /// 表格重绘时
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void panint(object sender, PaintEventArgs e)
        {
            for (int i = 0; i < this.dataGridView1.Rows.Count; i++)
            {
                for (int j = 5; j < this.dataGridView1.Columns.Count; j++)
                {
                    if (this.dataGridView1.Rows[i].Cells[j].Value.ToString() == "1")
                    {
                        this.dataGridView1.Rows[i].Cells[j].Style.BackColor = Color.Green;//将颜色设定为绿色
                    }
                    if (this.dataGridView1.Rows[i].Cells[j].Value.ToString() == "2")
                    {
                        this.dataGridView1.Rows[i].Cells[j].Style.BackColor = Color.Red;//将颜色设定为绿色
                    }
                }

            }
            for (int j = 5; j < this.dataGridView1.Columns.Count; j++)
            {
                //dataGridView1.Columns[j].Width = 50;
                dataGridView1.Columns[j].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
        }

        /// <summary>
        /// 查询零件批次
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void 查询零件批次ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            rowIndex = this.dataGridView1.CurrentCell.RowIndex;
            colIndex = this.dataGridView1.CurrentCell.ColumnIndex;

            string colName = this.dataGridView1.Columns[colIndex].Name;
            string code = this.dataGridView1.Rows[rowIndex].Cells["条码"].Value.ToString();
            string qty = this.dataGridView1.Rows[rowIndex].Cells["次数"].Value.ToString();
            string sqlstring = "exec fynP_getCourseDatas '" + code + "','" + colName + "',0,"+qty;
            DataTable dts = new DataTable();
            hint = sqltoolsyy.GetDataTable("", sqlstring, true, out dts);
            if (hint.Valid == false)
            {
                MessageBox.Show(hint.Msg);
                return;
            }
            if (dts.Rows.Count > 0)
            {
                this.dv.DataSource = dts;
                this.dv.AllowUserToAddRows = false;//设定为也允许添加行
                this.dv.AllowUserToDeleteRows = false;//设定为允许删除行
                this.dv.ReadOnly = true;
                this.dv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                this.dv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                Rectangle rect = this.dataGridView1.GetCellDisplayRectangle(colIndex, rowIndex, false);//获取单元格的位置
                if ((rect.Top + dataGridView1.Top + rect.Height * 2) < (dataGridView1.Height * 2 / 3))
                {
                    dv.Left = dataGridView1.Left + 10 / 2;// rect.Left +
                    dv.Top = rect.Top + dataGridView1.Top + rect.Height * 2;
                    dv.Width = dataGridView1.Width - 10;
                    dv.Height = (dts.Rows.Count * rect.Height + rect.Height) > (dataGridView1.Height - dv.Top) ? dataGridView1.Height - dv.Top : dts.Rows.Count * rect.Height + rect.Height;//rect.Height * this.dv.Rows.Count + rect.Height;
                }
                else
                {
                    dv.Left = dataGridView1.Left + 10 / 2;// rect.Left +
                    dv.Top = (rect.Top - (dts.Rows.Count * rect.Height)) < (panel2.Top + dataGridView1.Top + rect.Height * 2) ? (panel2.Top + dataGridView1.Top + rect.Height * 2) : (rect.Top - (dts.Rows.Count * rect.Height));
                    dv.Width = dataGridView1.Width - 10;
                    dv.Height = rect.Top - dv.Top + rect.Height * 2;//rect.Height * this.dv.Rows.Count + rect.Height;
                }

                dv.Visible = true;
                dv.BringToFront();//将空间排列到最前面
            }
        }

        private void columnAdded(object sender, DataGridViewColumnEventArgs e)
        {
            e.Column.FillWeight = 10;
            e.Column.Width = 10;
        }

        private void toolStripButton4_Click(object sender, EventArgs e)
        {
            this.panel1.Visible = false;
            this.panel3.Visible = true;
        }
        private void Dgv_ColumnAdded(object sender, DataGridViewColumnEventArgs e)
        {
            e.Column.FillWeight = 10;
            e.Column.Width = 10;
        }

        private void receive(object sqlstr)
        {
            SQLToolsYY sqltoolsyy1 = new SQLToolsYY();
            Hint hint1 = new Hint();
            string sql = (string)sqlstr;//@"EXEC fynP_getCourseDatatable ";
            DataTable dtable = new DataTable();
            hint1 = sqltoolsyy1.GetDataTable("", sql, true, out dtable);
            if (hint1.Valid != true)
            {
                MessageBox.Show(hint1.Msg);
            }
            else
            {
                if (dtable.Rows.Count > 0)
                {
                    /*
                    string[] ColumnNames = { "code" };
                    string Columns02 = "columnfield";
                    string Quantity = "columnvalue";
                    DataTable dt=DataTableRowtoCon(ColumnNames, dtable, Columns02, Quantity);
                    */
                    DataTable dt = dtable;
                    DataTable dt1 = new DataTable();
                    hint1 = sqltoolsyy1.GetDataTable("", "select * from fynT_header", true, out dt1);
                    if (hint1.Valid != true)
                        MessageBox.Show(hint1.Msg);
                    foreach (DataRow item in dt1.Rows)
                    {
                        if (dt.Columns.Contains(item["columnValue"].ToString()))
                        {
                            dt.Columns[item["columnValue"].ToString()].ColumnName = item["columnName"].ToString();
                        }

                    }
                    WriteReadExecl.writeexecl(dt);
                }
                else
                {
                    MessageBox.Show("错误：无查询结果！");
                }
            }
        }

        private void visible_1(object sender, EventArgs e)
        {
            this.comboBox6.Items.Clear();
            this.comboBox6.Text = "";
            string sql = "";
            sql = @"select distinct 条码 from reduction";
            DataTable dt = new DataTable();
            hint = sqltoolsyy.GetDataTable("", sql, true, out dt);
            if (hint.Valid != true)
            {
                MessageBox.Show(hint.Msg);
            }
            else
            {
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    this.comboBox6.Items.Add(dt.Rows[i][0].ToString());
                }
            }
            this.comboBox7.Items.Clear();
            this.comboBox7.Text = "";
            sql = @"SELECT distinct partCode
                    FROM fynT_partDetail
                    where partName<>'标签条码'";
            hint = sqltoolsyy.GetDataTable("", sql, true, out dt);
            if (hint.Valid != true)
            {
                MessageBox.Show(hint.Msg);
            }
            else
            {
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    this.comboBox7.Items.Add(dt.Rows[i][0].ToString());
                }

            }
            this.comboBox4.Items.Clear();
            this.comboBox4.Text = "";
            sql = @"select distinct convert(nvarchar(50),时间,23) from reduction";
            hint = sqltoolsyy.GetDataTable("", sql, true, out dt);
            if (hint.Valid != true)
            {
                MessageBox.Show(hint.Msg);
            }
            else
            {
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    this.comboBox4.Items.Add(dt.Rows[i][0].ToString());
                }
            }
        }

        private void button7_Click(object sender, EventArgs e)
        {
            string sql = @"EXEC fynP_getCourseDatatable '"+this.comboBox6.Text+"','"+ this.comboBox7.Text+"','时间','"+this.comboBox4.Text+"'";
            object sqlstr =sql;
            Thread thread_3 = new Thread(new ParameterizedThreadStart(receive));
            thread_3.SetApartmentState(ApartmentState.STA);
            Control.CheckForIllegalCrossThreadCalls = false;
            thread_3.IsBackground = true;//设定为后台线程
            thread_3.Start(sqlstr);//开启线程
            //receive();
            this.panel3.Visible = false;
        }

        private void button6_Click(object sender, EventArgs e)
        {
            this.panel3.Visible = false;
        }

        ///***************************************************************************
        #region 行转列
        /// <summary>
        /// 行转列
        /// </summary>
        /// <param name="ColumnName">固定列列名</param>
        /// <param name="dtable">数据源</param>
        /// <param name="Columns02">不固定列</param>
        ///  <param name="Quantity">数据列</param>
        /// <returns></returns>
        public DataTable DataTableRowtoCon(string[] ColumnNames, DataTable dtable, string Columns02, string Quantity)
        {
            //从DataTable中读取不重复的行，用来构造新DataTable的列
            DataTable distinct_date = dtable.DefaultView.ToTable(true, ColumnNames);

            DataTable newDt = new DataTable();
            foreach (string ColumnName in ColumnNames)
            {
                //创建固定列
                DataColumn col01 = new DataColumn(ColumnName, Type.GetType("System.String"));
                newDt.Columns.Add(col01);
            }
            //循环创建不固定列
            for (int i = 0; i < dtable.Rows.Count; i++)
            {
                if (!newDt.Columns.Contains(dtable.Rows[i][Columns02].ToString()))
                {

                    DataColumn col02 = new DataColumn(dtable.Rows[i][Columns02].ToString(), Type.GetType("System.String"));
                    newDt.Columns.Add(col02);
                }
            }
            //给新表添加数据
            foreach (DataRow dr in distinct_date.Rows)
            {
                DataRow new_dr = newDt.NewRow();
                string dr_Where = string.Empty;//从不重复的数据中提取固定列的数据来组成对比条件
                foreach (string ColumnName in ColumnNames)
                {
                    new_dr[ColumnName] = dr[ColumnName].ToString();//给固定列添加数据
                    dr_Where += dr[ColumnName].ToString();
                }
                foreach (DataRow dr02 in dtable.Rows)
                {
                    string dr02_Where = string.Empty;//从原始数据中提取固定列的数据来组成对比条件
                    foreach (string ColumnName in ColumnNames)
                    {
                        dr02_Where += dr02[ColumnName].ToString();
                    }
                    if (dr_Where.Equals(dr02_Where))
                    {
                        new_dr[dr02[Columns02].ToString()] = dr02[Quantity].ToString();//给非固定列添加数据
                    }
                }
                newDt.Rows.Add(new_dr);
            }
            return newDt;
        }
        #endregion 
    }
}
