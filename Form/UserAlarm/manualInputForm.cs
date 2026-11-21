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
    public partial class manualInputForm : Form
    {
        SQLToolsYY sqltoolsyy = new SQLToolsYY();
        Hint hint;
        private bool isDragging = false;
        private Point dragCursorPoint;
        private Point dragPanelPoint;
        List<string> listOnit1 = new List<string>();
        List<string> listNew1 = new List<string>();
        List<string> listOnit2 = new List<string>();
        List<string> listNew2 = new List<string>();
        public manualInputForm()
        {
            InitializeComponent();
            // 订阅Panel的鼠标事件,窗口移动
            panel1.MouseDown += new MouseEventHandler(Panel1_MouseDown);
            panel1.MouseMove += new MouseEventHandler(Panel1_MouseMove);
            panel1.MouseUp += new MouseEventHandler(Panel1_MouseUp);
        }
        private void manualInputForm_Load(object sender, EventArgs e)
        {
            //初始化工序号
            initIntOprNo();
            toolStripStatusLabel1.Text = "";
        }

        /// <summary>
        /// 初始化工序号
        /// </summary>
        private void initIntOprNo()
        {
            toolStripComboBox1.Items.Clear();
            string sql = "select distinct intOprNo from LwT_WriteCalibration";
            DataTable sqdt = new DataTable();
            hint = sqltoolsyy.GetDataTable("", sql, true, out sqdt);
            if (hint.Valid)
            {
                for (int i = 0; i < sqdt.Rows.Count; i++)
                {
                    toolStripComboBox1.Items.Add(sqdt.Rows[i][0]);
                }
            }
        }

        private void toolStripComboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            //获取plc号
            string sqlPLC = @"select PLCstationNum, workNumber from fynT_intOprNoXML where intOprNo = '" + toolStripComboBox1.Text + "'";
            DataTable dtPLC = new DataTable();
            hint = sqltoolsyy.GetDataTable("", sqlPLC, true, out dtPLC);
            //初始化总判定
            toolStripComboBox2.Text = "合格";
            toolStripStatusLabel1.Text = "";

            //获取产品条码
            getBarcode(dtPLC.Rows[0][0].ToString(), dtPLC.Rows[0][1].ToString());

            //获取检验项目
            getDataGridView(dtPLC.Rows[0][0].ToString());
            
        }

        /// <summary>
        /// 获取产品条码
        /// </summary>
        /// <param name="plcNum"></param>
        private void getBarcode(string plcNum,string workNumber)
        {
            string sqlBar = @"select top 1 productCode from fynT_intOprNoResult where  intOprNo = '" + plcNum + "_" + workNumber + "' and DATEDIFF(DD,time,GETDATE())=0 order by time";
            DataTable dtBar = new DataTable();
            hint = sqltoolsyy.GetDataTable("", sqlBar, true, out dtBar);
            if(hint.Valid && dtBar.Rows.Count > 0)
            {
                toolStripTextBox1.Text = dtBar.Rows[0][0].ToString();
            }
            else
            {
                MessageBox.Show("未获取到产品条码");
                toolStripTextBox1.Text = "";
            }
        }

        /// <summary>
        /// 获取检验项目
        /// </summary>
        /// <param name="plcNum"></param>
        private void getDataGridView(string plcNum)
        {
            dataGridView2.Columns.Clear();
            //获取程序号
            string sqlParameter = @"exec fynP_getParameter '" + plcNum + "'";
            DataTable dtParameter = new DataTable();
            hint = sqltoolsyy.GetDataTable("", sqlParameter, true, out dtParameter);
            if(!hint.Valid || dtParameter == null || dtParameter.Rows.Count <= 0)
            {
                MessageBox.Show("未获取到程序号");
                return;
            }
            //获取检验项目表
            string sql = @"exec LwP_WriteCalibration '"+ dtParameter.Rows[0]["programNumber"] + "','"+ toolStripComboBox1.Text + "'";
            DataTable sqdt = new DataTable();
            hint = sqltoolsyy.GetDataTable("", sql, true, out sqdt);
            if (!hint.Valid || sqdt.Rows.Count <= 0)
            {
                MessageBox.Show("未获取到检验项目");
                return;
            }
            dataGridView2.DataSource = sqdt;
            //设置只读
            dataGridView2.Columns["项目名称"].ReadOnly = true;
            dataGridView2.Columns["结果名称"].ReadOnly = true;
            dataGridView2.Columns["下限值"].ReadOnly = true;
            dataGridView2.Columns["上限值"].ReadOnly = true;

            //添加判定列
            DataGridViewComboBoxColumn comboBoxColumn = new DataGridViewComboBoxColumn();
            comboBoxColumn.HeaderText = "判定"; // 设置列头文本  
            comboBoxColumn.Name = "status"; // 设置列名（用于后续引用）  
            comboBoxColumn.Items.AddRange(new object[] { "合格", "不合格" }); // 添加下拉列表项
            comboBoxColumn.DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox; // 确保显示为下拉列表  
            comboBoxColumn.Width = 100; // 设置列宽  
            comboBoxColumn.DefaultCellStyle.NullValue = "合格";
            comboBoxColumn.DefaultCellStyle.Format = "合格";
            // 将新列添加到 DataGridView 的 Columns 集合中  
            dataGridView2.Columns.Add(comboBoxColumn);
        }

        /// <summary>
        /// GridView2单元格值改变事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dataGridView2_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            // 检查事件是否发生在输入数据的列上  
            if (e.ColumnIndex == dataGridView2.Columns["结果"].Index && e.RowIndex >= 0)
            {
                // 获取输入的值  
                var inputValue = double.Parse(dataGridView2.Rows[e.RowIndex].Cells["结果"].Value?.ToString());
                var upperValue = double.Parse(dataGridView2.Rows[e.RowIndex].Cells["上限值"].Value?.ToString());
                var lowValue = double.Parse(dataGridView2.Rows[e.RowIndex].Cells["下限值"].Value?.ToString());

                // 判断输入值是否合格（这里简单判断是否为数字）  
                bool isValid = inputValue >= lowValue && inputValue <= upperValue;

                // 根据判断结果更改ComboBox的值  
                var comboBoxCell = dataGridView2.Rows[e.RowIndex].Cells["status"] as DataGridViewComboBoxCell;
                if (comboBoxCell != null)
                {
                    comboBoxCell.Value = isValid ? "合格" : "不合格"; // 根据实际情况设置值  
                }
            }
            // 检查是否是ComboBox列发生了变化  
            if (dataGridView2.Columns[e.ColumnIndex].Name == "status")
            {
                toolStripComboBox2.Text = "合格";
                // 根据ComboBox的值设置总判定结果  
                for (int i = 0; i < dataGridView2.Rows.Count; i++)
                {
                    if (dataGridView2.Rows[i].Cells["status"].Value == null) continue;
                    if(dataGridView2.Rows[i].Cells["status"].Value.ToString() == "不合格")
                    {
                        toolStripComboBox2.Text = "不合格";
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// 向数据库存入数据
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void 保存ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.dataGridView2.CurrentCell = null;
            if(toolStripComboBox1.Text == "")
            {
                MessageBox.Show("请先选择工序号");
                return;
            }
            for(int i = 0;i < dataGridView2.Rows.Count; i++)
            {
                if(dataGridView2.Rows[i].Cells["结果"].Value.ToString() == "" || dataGridView2.Rows[i].Cells["结果"].Value == null)
                {
                    MessageBox.Show("请输入结果值");
                    return;
                }
            }
            //确认用户是否保存
            DialogResult dr = MessageBox.Show("是否保存当前数据", "保存", MessageBoxButtons.YesNo);
            //如果不保存
            if (dr == DialogResult.No)
            {
                return;
            }
            //确认用户身份
            userLoginForm login = new userLoginForm("PZP_1_ZJLRJY");
            if (login.ShowDialog() == DialogResult.OK)
            {
                string barcode = toolStripTextBox1.Text;   //产品条码
                string intOprNo = toolStripComboBox1.Text; //工序号
                int decideInt = toolStripComboBox2.Text == "合格" ? 1 : 2;
                //总结果保存到数据库中
                string sql = @"exec LwP_saveWriteCalibrationResult '" + barcode + "','" + intOprNo + "','" + publicAddress.loginData.Rows[0]["jobNumber"] + "','" + decideInt + "'";
                DataTable sqdt = new DataTable();
                hint = sqltoolsyy.GetDataTable("", sql, true, out sqdt);
                if (!hint.Valid)
                {
                    toolStripStatusLabel1.Text = "数据存入主表失败";
                    return;
                }
                //获取自增id
                string id = sqdt.Rows[0][0].ToString();
                //添加数据到子表中
                insertResults(id);
                toolStripStatusLabel1.Text = "保存成功";
            }
            
        }

        /// <summary>
        /// 向数据库子表中插入数据
        /// </summary>
        /// <param name="id"></param>
        private void insertResults(string id)
        {
            for (int i = 0; i < this.dataGridView2.Rows.Count; i++)
            {
                string pName = dataGridView2.Rows[i].Cells["项目名称"].Value.ToString();
                string rName = dataGridView2.Rows[i].Cells["结果名称"].Value.ToString();
                string upper = dataGridView2.Rows[i].Cells["上限值"].Value.ToString();
                string lower = dataGridView2.Rows[i].Cells["下限值"].Value.ToString();
                string result = dataGridView2.Rows[i].Cells["结果"].Value.ToString();
                int decide = 1;
                if (dataGridView2.Rows[i].Cells["status"].Value != null)
                {
                    decide = dataGridView2.Rows[i].Cells["status"].Value.ToString() == "合格" ? 1 : 0;
                }
                //存入
                //总结果保存到数据库中
                string sql = @"INSERT INTO MesT_calibrationResults
	                             ([ID]
                                  ,[projectName]
                                  ,[ResultName]
                                  ,[lowerValue]
                                  ,[upperValue]
                                  ,[result]
                                  ,[decide])  
                            VALUES ('" + id + "','" + pName + "','" + rName + "','" + upper + "','" + lower + "','" + result + "','" + decide + "') ";
                DataTable sqdt = new DataTable();
                hint = sqltoolsyy.GetDataTable("", sql, true, out sqdt);
                if (!hint.Valid)
                {
                    toolStripStatusLabel1.Text = "数据存入子表失败";
                    return;
                }
            }
        }

        #region panel窗口移动
        private void Panel1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDragging = true;
                dragCursorPoint = Cursor.Position;
                dragPanelPoint = panel1.Location;
            }
        }

        private void Panel1_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                Point diff = Point.Subtract(Cursor.Position, new Size(dragCursorPoint));
                panel1.Location = Point.Add(dragPanelPoint, new Size(diff));
            }
        }

        private void Panel1_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDragging = false;
            }
        }
        #endregion

        /// <summary>
        /// 设置窗口居中显示
        /// </summary>
        private void CenterPanel()
        {
            // 获取窗体的宽度和高度  
            int formWidth = this.ClientSize.Width;
            int formHeight = this.ClientSize.Height;

            // 获取Panel的宽度和高度  
            int panelWidth = panel1.Width;
            int panelHeight = panel1.Height;

            // 计算Panel的X和Y坐标，使其居中  
            int x = (formWidth - panelWidth) / 2;
            int y = (formHeight - panelHeight) / 2;

            // 设置Panel的位置  
            panel1.Location = new Point(x, y);
        }

        /// <summary>
        /// 管理员登录
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripMenuItem4_Click(object sender, EventArgs e)
        {
            userLoginForm login = new userLoginForm("PZP_1_ZJLRGL");  //权限代码之后修改
            if (login.ShowDialog() == DialogResult.OK)
            {
                panel1.Visible = true;
                thingID.Text = publicAddress.loginName;
                //初始化可选工序号
                initSelectIntOprNo();
                //显示手动检验项目
                showCalibtionItem();
                //设置窗口居中显示
                CenterPanel();
            }
        }

        /// <summary>
        /// 初始化可选工序号
        /// </summary>
        private void initSelectIntOprNo()
        {
            comboBox4.Items.Clear();
            comboBox1.Items.Clear();
            comboBox2.Items.Clear();
            string sql = "select distinct intOprNo from fynT_intOprNoXML";
            DataTable sqdt = new DataTable();
            hint = sqltoolsyy.GetDataTable("", sql, true, out sqdt);
            if (hint.Valid)
            {
                for (int i = 0; i < sqdt.Rows.Count; i++)
                {
                    comboBox4.Items.Add(sqdt.Rows[i][0]);
                }
            }
        }

        private void comboBox4_SelectedIndexChanged(object sender, EventArgs e)
        {
            comboBox1.Items.Clear();
            comboBox2.Items.Clear();
            string sql = @"select distinct parameterName from fynT_parameters where ID = (
                           select top 1 id from fynT_parameter where SUBSTRING(equipmentNumber, CHARINDEX('_', equipmentNumber)+1,LEN(equipmentNumber) - 2) = 
                           '" + comboBox4 .Text+ "' and programNumber = '1' order by versionNumber)and parameterName is not null"; 
            DataTable sqdt = new DataTable();
            hint = sqltoolsyy.GetDataTable("", sql, true, out sqdt);
            if (hint.Valid)
            {
                for (int i = 0; i < sqdt.Rows.Count; i++)
                {
                    comboBox1.Items.Add(sqdt.Rows[i][0]);
                    comboBox2.Items.Add(sqdt.Rows[i][0]);
                    listOnit1.Add(sqdt.Rows[i][0].ToString());
                    listOnit2.Add(sqdt.Rows[i][0].ToString());
                }
            }
        }

        /// <summary>
        /// 显示手动检验项目
        /// </summary>
        private void showCalibtionItem()
        {
            dtCalibration.Columns.Clear();
            string sql = @"SELECT  [id]
                                  ,[intOprNo] 工序号
                                  ,[projectName] 项目名称
                                  ,[operator] 操作员
                                  ,[dateTime] 时间
                              FROM [LwT_WriteCalibration]";
            DataTable sqdt = new DataTable();
            hint = sqltoolsyy.GetDataTable("", sql, true, out sqdt);
            dtCalibration.DataSource = sqdt;
            //隐藏ID列
            dtCalibration.Columns["id"].Visible = false;
            // 添加一个 DataGridViewButtonColumn 作为删除列  
            DataGridViewButtonColumn deleteColumn = new DataGridViewButtonColumn();
            deleteColumn.HeaderText = "操作";
            deleteColumn.Name = "操作";
            deleteColumn.Text = "删除";
            deleteColumn.UseColumnTextForButtonValue = true;


            // 将删除列添加到 DataGridView 的列集合中  
            dtCalibration.Columns.Add(deleteColumn);

            
        }


        /// <summary>
        /// 关闭管理员窗口
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e)
        {
            panel1.Visible = false;
            initIntOprNo();
            toolStripStatusLabel1.Text = "";
        }

        private void comboBox2_TextUpdate(object sender, EventArgs e)
        {
            //this.comboBox2.Items.Clear();
            //listNew2.Clear();
            //foreach (var item in listOnit2)
            //{
            //    if (item.Contains(this.comboBox2.Text))
            //    {
            //        listNew2.Add(item);
            //    }
            //}
            //this.comboBox2.Items.AddRange(listNew2.ToArray());
            //this.comboBox2.SelectionStart = this.comboBox2.Text.Length;
            //Cursor = Cursors.Default;
            //this.comboBox2.DroppedDown = true;
        }

        private void comboBox1_TextUpdate(object sender, EventArgs e)
        {
            //this.comboBox1.Items.Clear();
            //listNew1.Clear();
            //foreach (var item in listOnit1)
            //{
            //    if (item.Contains(this.comboBox1.Text))
            //    {
            //        listNew1.Add(item);
            //    }
            //}
            //this.comboBox1.Items.AddRange(listNew1.ToArray());
            //this.comboBox1.SelectionStart = this.comboBox1.Text.Length;
            //Cursor = Cursors.Default;
            //this.comboBox1.DroppedDown = true;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (comboBox1.Text == "" || comboBox2.Text == "" || comboBox4.Text == "" || textBox1.Text == "")
            {
                MessageBox.Show("数据不能为空", "提示", MessageBoxButtons.OK);
                return;
            }
            //确认用户是否保存
            DialogResult dr = MessageBox.Show("是否添加该条数据", "添加", MessageBoxButtons.YesNo);
            //如果不保存
            if (dr == DialogResult.No)
            {
                return;
            }
            string sql = @"exec LwP_saveWriteCalibrationItems '"+ comboBox4.Text + "','"+ textBox1.Text + "','"+ comboBox2.Text + "','"+ comboBox1.Text + "','"+ publicAddress.loginData.Rows[0]["jobNumber"] + "'";
            DataTable sqdt = new DataTable();
            hint = sqltoolsyy.GetDataTable("", sql, true, out sqdt);
            if (hint.Valid)
            {
                MessageBox.Show("添加成功！");
            }
            //刷新表格
            showCalibtionItem();
            initSelectIntOprNo();
            textBox1.Text = "";
        }

        private void dtCalibration_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == dtCalibration.Columns["操作"].Index && e.RowIndex >= 0)
            {
                //确认用户是否删除
                DialogResult dr = MessageBox.Show("是否删除该条数据", "删除", MessageBoxButtons.YesNo);
                //如果不删除
                if (dr == DialogResult.Yes)
                {
                    //数据库中删除
                    string delsql = @"delete LwT_WriteCalibration where id = '" + dtCalibration.Rows[e.RowIndex].Cells["id"].Value.ToString() + "'";
                    DataTable deldt = new DataTable();
                    hint = sqltoolsyy.GetDataTable("", delsql, true, out deldt);

                    //刷新界面
                    showCalibtionItem();
                    // 删除 DataGridView 中的行  
                    //dtCalibration.Rows.RemoveAt(e.RowIndex);


                    //// 注意：这里假设你的 DataTable 没有其他绑定或约束，否则可能需要处理这些  
                    //sqdt.Rows[e.RowIndex].Delete();
                    //sqdt.AcceptChanges();
                }

            }
        }
    }
}
