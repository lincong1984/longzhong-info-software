using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace generaControlProcedure
{
    public partial class equipmentParameterForm : Form
    {
        int sp_2;

        Form form = null;
        public equipmentParameterForm()
        {
            InitializeComponent();
            sp_2 = this.splitContainer3.SplitterDistance;
            this.splitContainer3.Panel1MinSize = 15;
        }
        SQLToolsYY sqltoolsyy = new SQLToolsYY();
        Hint hint;
        string name = "admin";//登录名
        bool saveJurisdiction = false;
        int rowIndex = 0;
        /// <summary>
        /// 窗体加载时运行
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void parameterForm_Load(object sender, EventArgs e)
        {
            if (publicAddress.loginName != "admin")
            {
                toolStripMenuItem1.Enabled = false;
                execl导入ToolStripMenuItem.Enabled = false;
                保存ToolStripMenuItem.Enabled = false;
                execl导出ToolStripMenuItem.Enabled = false;
                //参数上载ToolStripMenuItem.Enabled = false;
                toolSelect.Enabled = false;
                新建ToolStripMenuItem.Enabled = false;
                foreach (DataRow row in publicAddress.loginData.Rows)
                {
                    if (Convert.ToBoolean(row["jurisdiction"]))
                    {
                        name = row["name"].ToString();
                        switch (row["jurisdictionEncoding"].ToString().Replace(publicAddress.lineNumber, ""))
                        {
                            case "_parameter_modify":
                                toolStripMenuItem1.Enabled = true;
                                execl导入ToolStripMenuItem.Enabled = true;
                                //保存ToolStripMenuItem.Enabled = false;
                                break;
                            case "_parameter_save":
                                saveJurisdiction = true;
                                //保存ToolStripMenuItem.Enabled = false
                                break;
                            case "_parameter_export":
                                execl导出ToolStripMenuItem.Enabled = true;
                                break;
                            case "_parameter_upload":
                                //参数上载ToolStripMenuItem.Enabled = true;
                                break;
                            case "_parameter_select":
                                toolSelect.Enabled = true;
                                break;
                        }
                    }
                }
            }
            else
            {
                toolStripMenuItem1.Enabled = true;
                execl导入ToolStripMenuItem.Enabled = true;
                保存ToolStripMenuItem.Enabled = false;
                execl导出ToolStripMenuItem.Enabled = true;
                //参数上载ToolStripMenuItem.Enabled = true;
                toolSelect.Enabled = true;
                新建ToolStripMenuItem.Enabled = false;
                saveJurisdiction = true;
            }
            DataTable dt = new DataTable();
            string sl;
            sl = "select distinct PLCname from  dbo.fynT_PLCaddress";
            DataTable sqdt = new DataTable();
            hint = sqltoolsyy.GetDataTable("", sl, true, out sqdt);
            this.equipmentNumber.DataSource = sqdt;
            this.equipmentNumber.DisplayMember = "PLCname";
            this.equipmentNumber.Text = "";
            this.dataGridView2.CellDoubleClick += dataGridView2_CellDoubleClick;//添加双击单元格事件
        }
        /// <summary>
        /// 参数上载
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void 参数上载ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                for (int i = 0; i < publicAddress.readParameter.Length; i++)
                {
                    if (publicAddress.readParameter[i] != 10)
                    {
                        publicAddress.readParameter[i] = 10;
                    }
                }
            }
            catch (Exception )
            {

            }
        }
        /// <summary>
        /// 查询
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolSelect_Click(object sender, EventArgs e)
        {
            this.BeginInvoke((EventHandler)delegate
            {
                comboBox1.Items.Clear();
                comboBox1.Items.Add("设备编号");
                //comboBox1.Items.Add("程序号");
                //comboBox1.Items.Add("程序名称");
                //添加判断条件
                comboBox2.Items.Clear();
                comboBox2.Items.Add("=");
                comboBox2.Items.Add(">=");
                comboBox2.Items.Add("<=");
                comboBox2.Items.Add("<>");
                comboBox2.Items.Add(">");
                comboBox2.Items.Add("<");
                comboBox2.Items.Add("like");

                this.panel1.Visible = true;
                //面板可以改变位置和大小
                DragComponent a = new DragComponent();
                a.initProperty(panel1);

                /* DataTable dt = new DataTable();
                 DataTable dt1 = new DataTable();
                 int ID = 0;
                 string sqlstr = "select * from fynV_parameter where 1=1";
                 if (this.programNumber.Text.Trim() != "")
                 {
                     sqlstr = sqlstr + " and programNumber=" + this.programNumber.Text.Trim();
                 }
                 if (this.programName.Text.Trim() != "")
                 {
                     sqlstr = sqlstr + " and programName='" + this.programName.Text.Trim() + "'";
                 }
                 if (this.equipmentNumber.SelectedValue.ToString().Trim() != "")
                 {
                     sqlstr = sqlstr + " and equipmentNumber='" + this.equipmentNumber.SelectedValue.ToString().Trim() + "'";
                 }
                 sqlstr = sqlstr + "";
                 hint = sqltoolsyy.GetDataTable("", sqlstr, true, out dt);
                 if (dt.Rows.Count > 0 && hint.Valid)
                 {
                     DataTable copyDataTable = dt.Copy();
                     copyDataTable.Columns.Remove("versionNumber");
                     copyDataTable.Columns.Remove("redactName");
                     copyDataTable.Columns.Remove("redactTime");
                     this.textBoxVersionNumber.Text = "V"+dt.Rows[0]["versionNumber"].ToString();
                     this.textBoxRedactName.Text= dt.Rows[0]["redactName"].ToString();
                     this.textBoxRedactTime.Text= dt.Rows[0]["redactTime"].ToString();
                     this.dataGridView1.DataSource = copyDataTable;
                     refresh();
                 }
                 else
                 {
                     if (hint.Valid == false)
                     {
                         MessageBox.Show(hint.Msg);
                     }
                     //this.dataGridView1.DataSource = dt;
                 }
                 //
                 //execl导入ToolStripMenuItem.Enabled = false;//去除execl导入使能
                 //保存ToolStripMenuItem.Enabled = false;*/
            });
        }
        /// <summary>
        /// 刷新表格
        /// </summary>
        private void refresh()
        {
            this.BeginInvoke((EventHandler)delegate
            {
                DataTable dt = new DataTable();
                this.dataGridView1.Columns["category"].Visible = false;
                this.dataGridView1.Columns["equipmentNumber"].Visible = false;
                this.dataGridView1.Columns["programName"].Visible = false;
                this.dataGridView1.Columns["programNumber"].Visible = false;
                this.dataGridView1.Columns["parameterValues"].Visible = false;
                this.dataGridView1.Columns["decimalPlace"].Visible = false;
                this.dataGridView1.Columns["PLCAddress"].HeaderText = "PLC地址";
                this.dataGridView1.Columns["parameterName"].HeaderText = "参数名称";
                this.dataGridView1.Columns["dataType"].HeaderText = "数据类型";
                this.dataGridView1.Columns["value"].HeaderText = "参数值";
                this.dataGridView1.Columns["AutoID"].Width = 100;
                this.dataGridView1.Columns["PLCAddress"].Width = 200;
                this.dataGridView1.Columns["parameterName"].Width = 200;
                this.dataGridView1.Columns["dataType"].Width = 200;
                this.dataGridView1.Columns["value"].Width = 200;
                this.dataGridView1.Columns["AutoID"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

                //this.programNumber.Text = this.dataGridView1.Rows[0].Cells["programNumber"].Value.ToString();
                //this.programName.Text = this.dataGridView1.Rows[0].Cells["programName"].Value.ToString();

                //this.equipmentNumber.Text= this.dataGridView1.Rows[0].Cells["equipmentNumber"].Value.ToString();
                string name;
                name = this.dataGridView1.Rows[0].Cells["equipmentNumber"].Value.ToString();
                string sql = "select distinct PLCname from  dbo.fynT_PLCaddress  where equipmentNumber = '" + name + "'";
                DataTable eqnm = new DataTable();
                hint = sqltoolsyy.GetDataTable("", sql, true, out eqnm);
                this.equipmentNumber.Text = eqnm.Rows[0][0].ToString();

                this.dataGridView1.ReadOnly = true;
                this.dataGridView1.AllowUserToDeleteRows = false;
            });
        }
        /// <summary>
        /// 文件导出
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void execl导出ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            WriteReadExecl.writeexecl(this.dataGridView1);
        }
        /// <summary>
        /// 文件导入
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void execl导入ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DataTable dt = new DataTable();
            WriteReadExecl.readexecl(out dt);
            if (dt != null)
            {
                dt.Columns["参数名称"].ColumnName = "parameterName";
                dt.Columns["PLC地址"].ColumnName = "PLCAddress";
                dt.Columns["数据类型"].ColumnName = "dataType";
                dt.Columns["参数值"].ColumnName = "parameterValues";
                this.dataGridView1.DataSource = dt;
                //this.programNumber.Text = "";
                //this.programName.Text = "";
                this.equipmentNumber.Text = "";
                this.dataGridView1.Columns["PLCAddress"].HeaderText = "PLC地址";
                this.dataGridView1.Columns["parameterName"].HeaderText = "参数名称";
                this.dataGridView1.Columns["dataType"].HeaderText = "数据类型";
                this.dataGridView1.Columns["parameterValues"].HeaderText = "参数值";
                this.dataGridView1.Columns[0].Width = 100;
                this.dataGridView1.Columns["PLCAddress"].Width = 200;
                this.dataGridView1.Columns["parameterName"].Width = 500;
                this.dataGridView1.Columns["dataType"].Width = 200;
                this.dataGridView1.Columns["parameterValues"].Width = 200;
            }
        }
        /// <summary>
        /// 新建参数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void 新建ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.dataGridView1.DataSource=null;//清楚所有数据
            //this.programNumber.Text = "";
            //this.programName.Text = "";
            this.equipmentNumber.Text = "";
            execl导入ToolStripMenuItem.Enabled = true;
            保存ToolStripMenuItem.Enabled = true;
        }
        /// <summary>
        /// 保存参数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void 保存ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.equipmentNumber.SelectedValue.ToString() != "")
            {
                DataTable dt = (DataTable)this.dataGridView1.DataSource;
                if (dt != null)
                {
                    DataTable dtCopy = dt.Copy();
                    if (dtCopy.Columns.Contains("AutoID"))
                        dtCopy.Columns.Remove("AutoID");
                    if(dt.Columns.Contains("parameterName"))
                        dtCopy.Columns.Remove("parameterName");
                    if(dtCopy.Columns.Contains("dataType"))
                        dtCopy.Columns.Remove("dataType");
                    if (dtCopy.Columns.Contains("equipmentNumber"))
                        dtCopy.Columns.Remove("equipmentNumber");
                    if (dtCopy.Columns.Contains("programName"))
                        dtCopy.Columns.Remove("programName");
                    if (dtCopy.Columns.Contains("programNumber"))
                        dtCopy.Columns.Remove("programNumber");
                    if (dtCopy.Columns.Contains("decimalPlace"))
                        dtCopy.Columns.Remove("decimalPlace");
                    if (dtCopy.Columns.Contains("parameterValues"))
                        dtCopy.Columns.Remove("parameterValues");

                    if (dtCopy.Columns.Contains("category"))
                        dtCopy.Columns.Remove("category");

                    if (dtCopy.Columns.Contains("value"))
                    {
                        dtCopy.Columns["value"].ColumnName = "parameterValues";
                    }
                    DataTable dt1 = new DataTable();
                    sqltoolsyy.GetDataTable("", "delete fynT_TemporaryParametersExecl", true, out dt1);
                    sqltoolsyy.SqlInsertInto("fynT_TemporaryParametersExecl", dtCopy);
                    //string sqlstr = @"exec fynP_saveParameterExecl '" + this.equipmentNumber.SelectedValue.ToString() + "','" + this.programNumber.Text + "','" + this.programName.Text + "','" + name + "'";
                    string sqlstr = @"exec fynP_saveEquipmentParameterExecl '" + this.equipmentNumber.Text.Trim() + "','','','" + name + "'";
                    DataTable dt2 = new DataTable();
                    hint=sqltoolsyy.GetDataTable("", sqlstr, true, out dt1);
                    if (hint.Valid)
                    {                        
                        this.dataGridView1.ReadOnly = true;
                        this.dataGridView1.AllowUserToDeleteRows = false;
                        //programNumber.ReadOnly = true;
                        //programName.ReadOnly = true;
                        MessageBox.Show("保存成功！", "消息");
                        保存ToolStripMenuItem.Enabled = false;
                    }
                }
                
            }
            else
            {
                MessageBox.Show("请选择设备名称");
            }

        }

        /// <summary>
        /// 编辑按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            this.dataGridView1.ReadOnly = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            //programNumber.ReadOnly = false;
            //programName.ReadOnly = false;
        }
        /// <summary>
        /// 字段选择时执行
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void selectIndex(object sender,EventArgs e)
        {
            this.comboBox3.Items.Clear();
            this.comboBox3.Text = "";
            string sql = "";
            switch (this.comboBox1.Text)
            {
                case "设备编号":
                    sql = @"select distinct equipmentNumber from fynT_equipmentParameter";
                    break;
                case "程序号":
                    sql = @"select distinct programNumber from fynT_equipmentParameter order by programNumber";
                    break;
                case "程序名称":
                    sql = @"select distinct programName from fynT_equipmentParameter";
                    break;
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
        /// 确定按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button3_Click(object sender, EventArgs e)
        {
            this.BeginInvoke((EventHandler)delegate
            {
            Cursor.Current = Cursors.WaitCursor;
            //string sql = @"select [ID],[equipmentNumber] as 设备编号,[programNumber] as 程序号,[programName] as 程序名 from fynT_parameter  where 1=1 ";
            string sql = @"select distinct [equipmentNumber] as 设备编号 from fynT_equipmentParameter  where 1=1 ";
            foreach (var item in this.listBox1.Items)
            {
                switch (item.ToString().Substring(0, item.ToString().IndexOf(this.comboBox2.Text)).Trim())
                {
                    case "设备编号":
                        sql += " and " + item.ToString().Replace("设备编号", "equipmentNumber");
                        break;
                }
            }

            DataTable dt = new DataTable();
            hint = sqltoolsyy.GetDataTable("", sql , true, out dt);
            if (hint.Valid != true)
                MessageBox.Show(hint.Msg);
            dataGridView2.DataSource = dt;
            //dataGridView2.Columns["ID"].Visible = false;

            //this.dataGridView2.Columns["版本号"].DefaultCellStyle.Format = "0.#";
            //dataGridView2.Columns["ID"].Width = 0;
            comboBox1.Text = "";
            comboBox2.Text = "";
            comboBox3.Text = "";
            listBox1.Items.Clear();
            panel1.Visible = false;
            });
        }
        /// <summary>
        /// 添加条件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button2_Click(object sender, EventArgs e)
        {
            if (this.comboBox1.Text != "" && this.comboBox2.Text != "" && this.comboBox3.Text != "")
            {
                string items = (this.comboBox2.Text == "like") ? this.comboBox1.Text + " " + this.comboBox2.Text + " ''%" + this.comboBox3.Text + "%''" : this.comboBox1.Text + " " + this.comboBox2.Text + " '" + this.comboBox3.Text + "'";
                //items = this.comboBox1.Text == "生产时间" ? //"convert(date," + this.comboBox1.Text + ") " + this.comboBox2.Text + " '" + this.comboBox3.Text + "'" : items;
                if (!judgeitems(this.listBox1, items))
                    this.listBox1.Items.Add(items);
            }
        }
        /// <summary>
        /// 判断lisbox控件项是否存在
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
        /// 取消按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button4_Click(object sender, EventArgs e)
        {
            comboBox1.Text = "";
            comboBox2.Text = "";
            comboBox3.Text = "";
            listBox1.Items.Clear();
            panel1.Visible = false;
        }
        /// <summary>
        /// 双击单元格事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dataGridView2_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            this.BeginInvoke((EventHandler)delegate
            {
                Cursor.Current = Cursors.WaitCursor;
                DataTable dt = new DataTable();
                DataTable dt1 = new DataTable();
                rowIndex = this.dataGridView2.CurrentCell.RowIndex;
                //colIndex = this.dataGridView1.CurrentCell.ColumnIndex;
                string Number = this.dataGridView2.Rows[rowIndex].Cells["设备编号"].Value.ToString();
                //string code = this.dataGridView2.Rows[rowIndex].Cells["程序号"].Value.ToString();
                //string versionNumber= this.dataGridView2.Rows[rowIndex].Cells["版本号"].Value.ToString();
                //string sqlstr = "SELECT  a.[AutoID] ,[equipmentNumber],[programName],[programNumber], a.[PLCAddress],a.[parameterName],a.[dataType] ,a.[parameterValues],a.[decimalPlace],[value],[versionNumber],[redactName],[redactTime],[category],ID  FROM fynV_parameter a left join fynT_parameters b on b.AutoID=a.AutoID where ID='"+ code + "'";
                string sqlstr = "select * from fynV_EquipmentParameter where equipmentNumber='" + Number + "'";

                hint = sqltoolsyy.GetDataTable("", sqlstr, true, out dt);
                //dataGridView1.DataSource = dt;
                if (dt.Rows.Count > 0 && hint.Valid)
                {
                    DataTable copyDataTable = dt.Copy();
                    copyDataTable.Columns.Remove("versionNumber");
                    copyDataTable.Columns.Remove("redactName");
                    copyDataTable.Columns.Remove("redactTime");
                    this.textBoxVersionNumber.Text = "V" + dt.Rows[0]["versionNumber"].ToString();
                    this.textBoxRedactName.Text = dt.Rows[0]["redactName"].ToString();
                    this.textBoxRedactTime.Text = dt.Rows[0]["redactTime"].ToString();
                    this.dataGridView1.DataSource = copyDataTable;
                    refresh();
                }
                else
                {
                    if (hint.Valid == false)
                    {
                        MessageBox.Show(hint.Msg);
                    }
                    this.dataGridView1.DataSource = dt;
                    //programNumber.Text = "";
                    //programName.Text = "";

                }
                //
                //execl导入ToolStripMenuItem.Enabled = false;//去除execl导入使能
                //保存ToolStripMenuItem.Enabled = false;
            });
        }
        /// <summary>
        /// 移除全部条件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button6_Click(object sender, EventArgs e)
        {
            listBox1.Items.Clear();
        }

        private void button5_Click(object sender, EventArgs e)
        {
            try
            {
                int index = this.listBox1.SelectedIndex;
                this.listBox1.Items.RemoveAt(index);
            }
            catch
            {
                MessageBox.Show("请选择需要移除的条件");
            }
        }
        /// <summary>
        /// 参数值更改事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            int columnIndex = e.ColumnIndex;
            //DataTable csdt = (DataTable)this.dataGridView1.DataSource;
            if (this.dataGridView1.Columns[columnIndex].Name.ToString()== "value" && saveJurisdiction && !this.dataGridView1.ReadOnly)
            {
                保存ToolStripMenuItem.Enabled = true;
            }
        }

        /// <summary>
        /// "《" "》"按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e)
        {
            if ("《" == button1.Text)
            {
                splitContainer3.SplitterDistance = 10;
                button1.Text = "》";
                if (form != null)
                {
                    form.WindowState = FormWindowState.Minimized;
                    form.WindowState = FormWindowState.Maximized;
                }
            }
            else
            {
                splitContainer3.SplitterDistance = sp_2;
                button1.Text = "《";
                if (form != null)
                {
                    form.WindowState = FormWindowState.Minimized;
                    form.WindowState = FormWindowState.Maximized;
                }
            }
        }
    }
}
