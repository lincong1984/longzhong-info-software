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
using Microsoft.VisualBasic;

namespace generaControlProcedure
{
    public partial class PlanForm : Form
    {
        public PlanForm()
        {
            InitializeComponent();
            #region
            /**
             * 利用双缓冲减少界面卡顿
             *OptimizedDoubleBuffer:双缓冲 
             * AllPaintingInWmPaint:不擦除背景，减少闪烁
             */
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint, true);
            this.UpdateStyles();
            #endregion
            #region
            /**
             * 利用反射设置DataGridView的双缓冲
             * 控制DataGridView加载数据延迟的问题。
             */
            Type dgvType = this.dataGridView1.GetType();   //这里
            PropertyInfo pi = dgvType.GetProperty("DoubleBuffered",
                BindingFlags.Instance | BindingFlags.NonPublic);
            pi.SetValue(this.dataGridView1, true, null);
            #endregion
        }

        /// <summary>
        /// 解决页面上所有控件加载慢的方法
        /// </summary>
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;
                return cp;
            }
        }
        SQLToolsYY sqlToolsYY = new SQLToolsYY();
        int winform;
        /// <summary>
        /// 窗体加载时运行
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PlanForm_Load(object sender, EventArgs e)
        {
            refreshPlan(9);//刷新当前计划
        }

        /// <summary>
        /// 刷新生产计划
        /// </summary>
        private void refreshPlan(int state)
        {
            string sqlstr;
            if (state < 10)
            {
                sqlstr = "select  * from fynV_plan where 生产状态<= " + state.ToString()+" order by 生产状态 desc";
            }
            else
            {
                sqlstr = "select  * from fynV_plan where 生产状态>= " + state.ToString() + " order by 生产状态 desc";
            }
            
            DataTable dt = new DataTable();
            Hint hint = sqlToolsYY.GetDataTable("", sqlstr, true, out dt);
            this.dataGridView1.DataSource = dt.DefaultView;
            this.dataGridView1.Columns["urgent"].DisplayIndex = 16;//将该列设定为最后一列
            this.dataGridView1.Columns["ID"].Visible = false;
            /*设定列宽*/
            this.dataGridView1.Columns[0].Width = 80;//选择
            this.dataGridView1.Columns[2].Width = 150;//计划生产日期
            this.dataGridView1.Columns[3].Width = 150;//名称
            this.dataGridView1.Columns[4].Width = 150;//图号
            this.dataGridView1.Columns[5].Width = 150;//批号
            this.dataGridView1.Columns[6].Width = 80;//数量
            this.dataGridView1.Columns[7].Width = 80;//生产状态
            this.dataGridView1.Columns[8].Width = 80;//生产线编号
            this.dataGridView1.Columns[9].Width = 80;//投产数
            this.dataGridView1.Columns[10].Width = 80;//完成数
            this.dataGridView1.Columns[11].Width = 80;//报废数
            this.dataGridView1.Columns[12].Width = 150;//排产时间
            this.dataGridView1.Columns[13].Width = 150;//排产人
            this.dataGridView1.Columns[14].Width = 150;//修改时间
            this.dataGridView1.Columns[15].Width = 150;//修改人
            //将单元格设置为禁止编辑
            for (int i = 0; i < this.dataGridView1.Rows.Count - 1; i++)
            {
                for (int j = 1; j < this.dataGridView1.Columns.Count; j++)
                {
                    this.dataGridView1.Rows[i].Cells[j].ReadOnly = true;//将行设置为禁止编辑
                }
                //设定颜色
                switch (Convert.ToInt16(this.dataGridView1.Rows[i].Cells["生产状态"].Value))
                {
                    case 10:
                        this.dataGridView1.Rows[i].DefaultCellStyle.BackColor = Color.Gray;//将颜色设定为灰色
                        break;
                    case 9:
                        this.dataGridView1.Rows[i].DefaultCellStyle.BackColor = Color.Green;//将颜色设定为绿色

                        break;
                    case 8:
                        this.dataGridView1.Rows[i].DefaultCellStyle.BackColor = Color.Magenta;//将颜色设定为红色
                        this.dataGridView1.Rows[i].Cells["urgent"].Value = "是";//加急状态
                        break;
                    case 7:
                        this.dataGridView1.Rows[i].DefaultCellStyle.BackColor = Color.White;//将颜色设定为白色
                        this.dataGridView1.Rows[i].Cells["urgent"].Value = "否";//加急状态
                        break;
                }
            }
            this.savetoolStripButton.Enabled = false;
            this.amendtoolStripButton.Enabled = false;
            this.deletetoolStripButton.Enabled = false;

        }
        private void 当前计划ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            refreshPlan(9);//刷新当前计划
            winform = 9;
        }

        private void 完成计划ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            refreshPlan(10);//刷新完成计划
            winform = 10;
        }

        private void 未生产计划ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            refreshPlan(8);//刷新未生产计划
            winform = 8;
        }

        //保存计划

        private void savetoolStripButton_Click(object sender, EventArgs e)
        {
            bool savebool=savePlan();//保存计划
            if (savebool)
            {
                MessageBox.Show("保存成功!");
            }
        }
        /// <summary>
        /// 保存数据
        /// </summary>
        private bool savePlan()
        {
            try
            {
                this.textBox1.Text = "";//清除报警
                string sqlstr;
                string ID;
                string productionDate;
                string cinvname;
                string cinvaddcode;
                string batch;
                string qty;
                string liennum;
                string name;
                string productionstate;
                DataTable dt = new DataTable();
                Hint hint;
                for (int i = 0; i < this.dataGridView1.Rows.Count; i++)
                {
                    if (Convert.ToBoolean(this.dataGridView1.Rows[i].Cells["choice"].Value))//判断是否选择了当前行
                    {

                        sqlstr = "";
                        ID = this.dataGridView1.Rows[i].Cells["ID"].Value.ToString();
                        if (ID == "")
                        {
                            ID ="0";
                        }
                        productionDate = this.dataGridView1.Rows[i].Cells["计划生产日期"].Value.ToString();
                        cinvname = this.dataGridView1.Rows[i].Cells["名称"].Value.ToString();
                        cinvaddcode = this.dataGridView1.Rows[i].Cells["图号"].Value.ToString();
                        batch = this.dataGridView1.Rows[i].Cells["批号"].Value.ToString();
                        qty = this.dataGridView1.Rows[i].Cells["数量"].Value.ToString();
                        liennum = this.dataGridView1.Rows[i].Cells["生产线编号"].Value.ToString();
                        name = "fyn";//对应权限人姓名
                        productionstate = this.dataGridView1.Rows[i].Cells["生产状态"].Value.ToString();
                        if (productionstate == "")
                        {
                            productionstate = "7";//未生产状态
                        }
                        if (this.dataGridView1.Rows[i].Cells["urgent"].Value.ToString() == "是")
                        {
                            productionstate = "8";//未生产状态
                        }
                        //组装SQL语句
                        sqlstr = "exec fnyP_savePlan " + ID + ",'" + productionDate + "','" + cinvname + "','" + cinvaddcode + "','" + batch + "'," + qty + "," + liennum + ",'" + name + "'," + productionstate;
                        hint = sqlToolsYY.GetDataTable("", sqlstr, true, out dt);//执行SQL语句
                        if (hint.Valid == false)
                        {
                            this.textBox1.AppendText("第【"+(i+1).ToString()+ "】行"+hint.Msg + "; ");//报警信息
                        }
                    }
                }
                return true;
            }
            catch (Exception )
            {
                return false;
            }
        }
        //修改当前行
        private void amendtoolStripButton_Click(object sender, EventArgs e)
        {
            int rowValue;
            string state;
            for (int i = 0; i < this.dataGridView1.Rows.Count; i++)
            {
                rowValue = this.dataGridView1.CurrentCell.RowIndex;
                state = this.dataGridView1.Rows[rowValue].Cells["生产状态"].Value.ToString();
                if (state == "") { state = "0"; }//判断生产状态是否为空
                if (Convert.ToBoolean(this.dataGridView1.Rows[i].Cells["choice"].Value)==true && Convert.ToInt16(state)<9)//判断是否选择了当前行
                {
                    this.dataGridView1.Rows[i].ReadOnly = false;//将第1列取消禁止编辑
                }
            }
        }
        //删除计划
        private void deletetoolStripButton_Click(object sender, EventArgs e)
        {
            int rowValue;
            string state;
            string reasonStr="";
            string sqlstr;
            Hint hint;
            DataTable dt = new DataTable();
            string ID;
            string name;

            messageForm f = new messageForm();
            f.TopLevel = true;
            f.ShowDialog();
            if (f.DialogResult == DialogResult.OK)
            {
                reasonStr = publicAddress.reasonText;
            }
            for (int i = 0; i < this.dataGridView1.Rows.Count-1; i++)
            {
                if (Convert.ToBoolean(this.dataGridView1.Rows[i].Cells["choice"].Value))
                {
                    rowValue = this.dataGridView1.CurrentCell.RowIndex;
                    state = this.dataGridView1.Rows[rowValue].Cells["生产状态"].Value.ToString();
                    if (state == "") { state = "0"; }//判断生产状态是否为空

                    ID = this.dataGridView1.Rows[i].Cells["ID"].Value.ToString();
                    if (ID == "")
                    {
                        ID = "0";
                    }
                    name = "fyn";//对应权限人姓名
                    sqlstr = "exec fnyP_deletePlan " + ID + ",'" + name + "','" + reasonStr + "'";
                    hint = sqlToolsYY.GetDataTable("", sqlstr, true, out dt);//执行SQL语句
                }
            }
            refreshPlan(9);


        }
        //刷新按钮
        private void refreshtoolStripButton_Click(object sender, EventArgs e)
        {
            switch (winform)
            {
                case 9:
                    refreshPlan(9);
                    break;
                case 10:
                    refreshPlan(10);
                    break;
                case 8:
                    refreshPlan(8);
                    break;
                default:
                    refreshPlan(9);
                    break;
            }
                
        }
        /// <summary>
        /// 选择当前行
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cellcontentclick(object sender, DataGridViewCellEventArgs e)
        {
            int rowValue = this.dataGridView1.CurrentCell.RowIndex;
            string state = this.dataGridView1.Rows[rowValue].Cells["生产状态"].Value.ToString();
            if (state == "") { state = "0"; }//判断生产状态是否为空
            string columnname = this.dataGridView1.Columns[this.dataGridView1.CurrentCell.ColumnIndex].Name;//获取当前活动单元格列名称
            this.dataGridView1.EndEdit();
            /*只有生产状态<9 才能修改，删除*/
            if (Convert.ToBoolean(this.dataGridView1.Rows[rowValue].Cells["choice"].Value) && columnname == "choice" && Convert.ToInt16(state) < 9)
            {
                this.savetoolStripButton.Enabled = true;
                this.amendtoolStripButton.Enabled = true;
                this.deletetoolStripButton.Enabled = true;
            }
            else
            {
                if (Convert.ToBoolean(this.dataGridView1.Rows[rowValue].Cells["choice"].Value) == false)
                {
                    this.savetoolStripButton.Enabled = false;
                    this.amendtoolStripButton.Enabled = false;
                    this.deletetoolStripButton.Enabled = false;
                }
            }
        }
    }
}
