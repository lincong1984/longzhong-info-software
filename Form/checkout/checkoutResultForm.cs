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
    public partial class checkoutResultForm : Form
    {
        public checkoutResultForm()
        {
            InitializeComponent();
        }
        SQLToolsYY sql = new SQLToolsYY();
        Hint hint = new Hint();
        DataGridView dgv = new DataGridView();
        int sign = 0;
        /// <summary>
        /// 添加按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void addButton_Click(object sender, EventArgs e)
        {
            DataTable dt = new DataTable();

            hint = sql.GetDataTable("", "select isnull(MAX(checkoutNumber),0) as max from fynT_checkoutResult", true, out dt);
            if (hint.Valid != false && dt.Rows.Count>0)
            {
                string checkoutNumber = ("00000000" + (Convert.ToInt32(dt.Rows[0][0]) + 1).ToString());
                this.checkoutNumber.Text = checkoutNumber.Substring(checkoutNumber.Length-8, 8);
            }
            this.code.Text = "";
            this.oprNo.Text = "";
            this.checkoutName.Text = "";
            this.dataGridView1.DataSource = null;
            this.code.Enabled = true;
            this.oprNo.Enabled = true;
            this.checkoutName.Enabled = true;
        }
        /// <summary>
        /// 窗体加载
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkoutResultForm_Load(object sender, EventArgs e)
        {
            this.Controls.Add(dgv);
            dgv.BringToFront();//将空间排在最外层
            dgv.Visible = false;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgv.AllowUserToAddRows = false;//设定为也允许添加行
            dgv.AllowUserToDeleteRows = false;//设定为允许删除行
            dgv.ReadOnly = true;
            dgv.CellContentDoubleClick += Dgv_CellContentDoubleClick;

            this.code.Enabled = false;
            this.oprNo.Enabled = false;
            this.checkoutName.Enabled = false;
        }
        /// <summary>
        /// 双击弹出表格单元格
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Dgv_CellContentDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (sign == 1)
            {
                this.code.Text = dgv.Rows[e.RowIndex].Cells["productCode"].Value.ToString();
            }
            if (sign == 2)
            {
                this.oprNo.Text = dgv.Rows[e.RowIndex].Cells["intOprNo"].Value.ToString();
                string sqlstring = @"select row_number()over(order by c.ID) as ID
                                            ,[checkoutType]
                                            ,[checkoutProject]
                                            ,[type]
                                            ,[lowerValue]
                                            ,[upperValue]
                                            ,[unit]
                                     from fynT_equipment a 
                                     inner join fynT_checkoutIndex b on b.equipmentCode=a.equipmentCode
                                     inner join fynT_checkoutProject c on c.ID=b.checkoutProjectID where intOprNo='" + this.oprNo.Text+"'";
                DataTable dt = new DataTable();
                hint = sql.GetDataTable("", sqlstring, true, out dt);
                dt.Columns.Add("result", typeof(float));
                //dt.Columns.Add("decide", typeof(string));
                this.dataGridView1.DataSource = dt;
                this.dataGridView1.Columns[0].Width = 80;
                this.dataGridView1.Columns["ID"].HeaderText = "序号";
                this.dataGridView1.Columns["checkoutType"].HeaderText = "检验类型";
                this.dataGridView1.Columns["checkoutProject"].HeaderText = "检验项目";
                this.dataGridView1.Columns["type"].HeaderText = "判定类型";
                this.dataGridView1.Columns["lowerValue"].HeaderText = "下限值";
                this.dataGridView1.Columns["upperValue"].HeaderText = "上限值";
                this.dataGridView1.Columns["unit"].HeaderText = "单位";
                this.dataGridView1.Columns["result"].HeaderText = "结果";
                //this.dataGridView1.Columns["result"].DisplayIndex = 6;
                DataGridViewComboBoxColumn decide = new DataGridViewComboBoxColumn();
                decide.HeaderText = "判定";
                decide.Name = "decide";
                decide.DataPropertyName = "decide";
                decide.Items.Add("合格");
                decide.Items.Add("不合格");
                this.dataGridView1.Columns.Add(decide);
                dt.Columns.Add("decide", typeof(string));
                for (int i = 0; i < this.dataGridView1.Columns.Count - 2; i++)
                {
                    this.dataGridView1.Columns[i].ReadOnly = true;
                }
            }
            
            dgv.Visible = false;
        }

        /// <summary>
        /// 条码输入框聚焦
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void enter(object sender, EventArgs e)
        {
            dgv.Top = this.code.Top + this.code.Height+this.splitContainer1.Top+5;
            dgv.Left = 50;
            dgv.Width = this.splitContainer1.Width - 100;
            dgv.Height = 500;
            dgv.Visible = true;
            getproductCode(1);
            sign = 1;
        }
        /// <summary>
        /// 单元格的值改变时运行
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void code_TextChanged(object sender, EventArgs e)
        {
            getproductCode(1);
        }
        /// <summary>
        /// 读取SQL数据写入弹出表格
        /// </summary>
        /// <param name="state"></param>
        private void getproductCode(int state)
        {
            string sqlstr = "";
            if (state == 1)
            {
                sqlstr = "select productCode,intOprNo,decide,time from fynT_intOprNoResult where CONVERT(date,time)= CONVERT(date,GETDATE())";
                if (this.code.Text != "")
                {
                    sqlstr = sqlstr + "and productCode like '%" + this.code.Text + "%'";
                }
            }
            if (state == 2)
            {
                sqlstr = "select * from fynT_equipment where 1= 1";
                if (this.oprNo.Text != "")
                {
                    sqlstr = sqlstr + "and intOprNo like '%" + this.oprNo.Text + "%'";
                }
            }
            DataTable dt = new DataTable();
            hint = sql.GetDataTable("", sqlstr, true, out dt);
            this.dgv.DataSource = dt;
        }
        /// <summary>
        /// 工序号输入框聚焦
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void oprNoEnter(object sender, EventArgs e)
        {
            dgv.Top = this.oprNo.Top + this.oprNo.Height + this.splitContainer1.Top + 5;
            dgv.Left = 50;
            dgv.Width = this.splitContainer1.Width - 100;
            dgv.Height = 500;
            dgv.Visible = true;
            getproductCode(2);
            sign = 2;
        }
        /// <summary>
        /// 工序号改变时
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void oprNo_TextChanged(object sender, EventArgs e)
        {
            getproductCode(2);
        }
        /// <summary>
        /// 单元格的值更改时发生
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void valusChanged(object sender, DataGridViewCellEventArgs e)
        {
            int rowIndex = e.RowIndex; //this.dataGridView1.CurrentCell.RowIndex;
            int colIndex = e.ColumnIndex;// this.dataGridView1.CurrentCell.ColumnIndex;
            if (rowIndex >= 0)
            {
                string columnName = this.dataGridView1.Columns[colIndex].Name;
                if (columnName == "result")
                {
                    string aaa;
                    aaa = this.dataGridView1.Rows[rowIndex].Cells["result"].Value.ToString() == "" ? "0" : this.dataGridView1.Rows[rowIndex].Cells["result"].Value.ToString();
                    double value = Convert.ToDouble(aaa);
                    double lowerValue = Convert.ToDouble(this.dataGridView1.Rows[rowIndex].Cells["lowerValue"].Value);
                    double upperValue = Convert.ToDouble(this.dataGridView1.Rows[rowIndex].Cells["upperValue"].Value);
                    if (value >= lowerValue && value <= upperValue)
                    {
                        this.dataGridView1.Rows[rowIndex].Cells["decide"].Value = "合格";
                    }
                    else
                    {
                        this.dataGridView1.Rows[rowIndex].Cells["decide"].Value = "不合格";
                    }
                }
            }
        }
        /// <summary>
        /// 保存按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void saveButton_Click(object sender, EventArgs e)
        {
            if (this.checkoutName.Text == "")
            {
                return;
            }
            DataTable dt = new DataTable();
            dt = this.dataGridView1.DataSource as DataTable;
            DataTable dt2 = dt.Copy();
            dt2.Columns.Add("AuotID", typeof(int)).SetOrdinal(0);
            dt2.Columns["result"].SetOrdinal(7);
            dt2.Columns.Add("time", typeof(DateTime));
            foreach (DataRow row in dt2.Rows)
            {
                if (row["type"].ToString() == "定量" && row["result"].ToString()=="")
                {
                    MessageBox.Show("错误：结果未填写完整！");
                    return;
                }
                if (row["decide"].ToString() == "")
                {
                    MessageBox.Show("错误：未给出合格判定！");
                    return;
                }
            }
            string sqlstr = @"exec fynP_savecheckoutResult '" + this.checkoutNumber.Text + "','" + this.code.Text + "','" + this.oprNo.Text + "','" + this.checkoutName.Text + "'";
            DataTable dt1 = new DataTable();
            hint = sql.GetDataTable("", sqlstr, true, out dt1);
            if (hint.Valid == true && Convert.ToInt32(dt1.Rows[0][0]) > 0)
            {
                string str = "";
                DataTable dt3 = new DataTable();
                foreach (DataRow row in dt2.Rows)
                {
                    str = @"exec fynP_savecheckoutResults " +
                        dt1.Rows[0][0] + ",'" +
                        row["checkoutType"].ToString() + "','" +
                        row["checkoutProject"].ToString() + "','" +
                        row["type"].ToString() + "'," +
                        (row["lowerValue"].ToString() == "" ? "0" : row["lowerValue"].ToString()) + "," +
                        (row["upperValue"].ToString()=="" ? "0": row["upperValue"].ToString()) + "," +
                        (row["result"].ToString()=="" ? "0": row["result"].ToString()) + ",'" +
                        row["unit"].ToString() + "','" +
                        row["decide"].ToString() + "'";
                    hint = sql.GetDataTable("", str, true, out dt3);
                    if (hint.Valid == false)
                    {
                        MessageBox.Show(hint.Msg);
                        return;
                    }

                }
            }

        }
    }
}
