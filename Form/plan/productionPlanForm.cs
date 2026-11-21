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
using static MES_SCAF.ClassFile.computer.JsonToSql;

namespace generaControlProcedure
{
    public partial class productionPlanForm : Form
    {
        public static productionPlanForm proform;
        SQLToolsYY sqlToolsYY = new SQLToolsYY();
        public productionPlanForm()
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
            CheckForIllegalCrossThreadCalls = false;
            proform = this;
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
        private void productionPlanForm_Load(object sender, EventArgs e)
        {
            refresh();//刷新当前计划
        }
        /// <summary>
        /// 刷新当前生产计划
        /// </summary>
        private void refresh()
        {
            this.BeginInvoke((EventHandler)delegate
            {
                string sqlstr = "select * from fynV_choicePlan order by 生产状态 desc";
                Hint hint;
                DataTable dt = new DataTable();
                hint = sqlToolsYY.GetDataTable("", sqlstr, true, out dt);
                if (hint.Valid)
                {
                    this.dataGridView1.DataSource = dt.DefaultView;
                    //设定列宽
                    this.dataGridView1.Columns[0].Width = 80;//选择
                    this.dataGridView1.Columns[1].Width = 150;//计划生产日期
                    this.dataGridView1.Columns[2].Width = 150;//名称
                    this.dataGridView1.Columns[3].Width = 150;//图号
                    this.dataGridView1.Columns[4].Width = 150;//批号
                    this.dataGridView1.Columns[5].Width = 100;//数量
                    this.dataGridView1.Columns[6].Width = 100;//生产线编号
                    this.dataGridView1.Columns[7].Width = 100;//投产数
                    this.dataGridView1.Columns[8].Width = 100;//完成数
                    this.dataGridView1.Columns[9].Width = 100;//报废数
                    this.dataGridView1.Columns[10].Width = 100;//生产状态
                                                               //设定取消编辑模式
                    for (int i = 1; i < this.dataGridView1.Columns.Count; i++)
                    {
                        this.dataGridView1.Columns[i].ReadOnly = true;
                    }
                    for (int i = 0; i < this.dataGridView1.Rows.Count; i++)
                    {
                        switch (Convert.ToInt16(this.dataGridView1.Rows[i].Cells["生产状态"].Value))
                        {
                            case 10:
                                this.dataGridView1.Rows[i].DefaultCellStyle.BackColor = Color.Gray;//将颜色设定为灰色
                                break;
                            case 9:
                                this.dataGridView1.Rows[i].DefaultCellStyle.BackColor = Color.White;//将颜色设定为白色
                                if (Convert.ToInt16(this.dataGridView1.Rows[i].Cells["状态"].Value) == 1)
                                {
                                    this.dataGridView1.Rows[i].DefaultCellStyle.BackColor = Color.Green;//将颜色设定为绿色
                                    this.dataGridView1.Rows[i].DefaultCellStyle.ForeColor = Color.White;
                                }
                                break;
                            case 8:
                                this.dataGridView1.Rows[i].DefaultCellStyle.BackColor = Color.Magenta;//将颜色设定为红色
                                break;
                            case 7:
                                this.dataGridView1.Rows[i].DefaultCellStyle.BackColor = Color.White;//将颜色设定为白色
                                break;
                        }

                    }
                }

                //窗口打开时将当前生产计划写入窗口
                sqlstr = "select * from fynV_choicePlan where 状态=1";
                hint = sqlToolsYY.GetDataTable("", sqlstr, true, out dt);
                if (hint.Valid == true && dt.Rows.Count > 0)
                {
                    this.cinvname.Text = dt.Rows[0]["名称"].ToString();
                    this.cinvaddcode.Text = dt.Rows[0]["图号"].ToString();
                    this.batch.Text = dt.Rows[0]["批号"].ToString();
                    this.qty.Text = dt.Rows[0]["数量"].ToString();
                    this.modidtext.Text = dt.Rows[0]["modID"].ToString();
                }
                else
                {
                    this.cinvname.Text = "";
                    this.cinvaddcode.Text = "";
                    this.batch.Text = "";
                    this.qty.Text = "";
                    this.modidtext.Text = "";
                }

            });
        }
        //将单选框设定为单选功能
        private void CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            this.BeginInvoke((EventHandler)delegate
            {
                string columnname = this.dataGridView1.Columns[this.dataGridView1.CurrentCell.ColumnIndex].Name;
                //判断选择列为选择列
                if (columnname == "choice")
                {
                    for (int i = 0; i < this.dataGridView1.Rows.Count; i++)
                    {
                        DataGridViewCheckBoxCell ck = this.dataGridView1.Rows[i].Cells[0] as DataGridViewCheckBoxCell;
                        if (i != e.RowIndex)
                        {
                            ck.Value = false;
                        }
                        else
                        {
                            ck.Value = true;
                        }
                    }
                    this.dataGridView1.EndEdit();
                }
            });
        }
        /// <summary>
        /// 开始生产选择计划
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void productionButton_Click(object sender, EventArgs e)
        {
            Task.Run(async () =>
            {
                DataGridViewRow lastSelectedRow = null;

                // 1. 先找出最后一个被选中的行（使用foreach）
                foreach (DataGridViewRow row in this.dataGridView1.Rows)
                {
                    DataGridViewCheckBoxCell ck = row.Cells[0] as DataGridViewCheckBoxCell;
                    if (Convert.ToBoolean(ck.Value))
                    {
                        lastSelectedRow = row;
                    }
                }

                // 2. 如果没有选中任何行，直接返回
                if (lastSelectedRow == null)
                    return;

                // 3. 先执行全局清零
                string sqlClear = "update fynT_plan set state=0 where ISNULL(state,0)=1";
                sqlToolsYY.GetDataTable("", sqlClear, true, out DataTable _);

                // 4. 获取最后一个选中行的信息
                string cinvaddcode = lastSelectedRow.Cells["图号"].Value.ToString();
                string batch = lastSelectedRow.Cells["批号"].Value.ToString();
                int planQty = Convert.ToInt32(lastSelectedRow.Cells["数量"].Value.ToString());
                string id = lastSelectedRow.Cells["ID"].Value.ToString();
                int modId = Convert.ToInt32(lastSelectedRow.Cells["modID"].Value.ToString());

                // 5. 更新UI
                this.Invoke((MethodInvoker)(() =>
                {
                    this.cinvname.Text = lastSelectedRow.Cells["名称"].Value.ToString();
                    this.cinvaddcode.Text = cinvaddcode;
                    this.batch.Text = batch;
                    this.qty.Text = planQty.ToString();
                    this.modidtext.Text = modId.ToString();
                }));

                // 6. 设置全局变量
                publicAddress.Cinvaddcode = cinvaddcode;
                publicAddress.Cinvcode = "";
                publicAddress.Batch = batch;
                publicAddress.planQty = planQty;

                // 7. 执行存储过程更新工艺路线
                string execProc = $"exec [MesServerSQL].[dbo].[PlmP_UpdateProcess_route] 'where 图号=''{cinvaddcode}'' and 生产线编号=''{publicAddress.lineNumber}'''";
                sqlToolsYY.GetDataTable("", execProc, true, out DataTable _);

                // 8. 检查是否有工艺路线
                string checkSql = $"SELECT COUNT(*) AS qty FROM [MesServerSQL].[dbo].[PlmT_Process_route] WHERE 图号 = '{cinvaddcode}' AND 生产线编号 = '{publicAddress.lineNumber}'";
                var hintCheck = sqlToolsYY.GetDataTable("", checkSql, true, out DataTable dtCheck);

                if (hintCheck.Valid && dtCheck.Rows.Count > 0 && (int)dtCheck.Rows[0][0] > 0)
                {
                    // 9. 激活该计划
                    string activateSql = $"update fynT_plan set state=1, productionState=9 where ID={id}";
                    sqlToolsYY.GetDataTable("", activateSql, true, out DataTable _);
                }

                // 10. 获取工艺路线
                string routeSql = $"SELECT DISTINCT [工艺路线] FROM [MesServerSQL].[dbo].[PlmT_Process_route] WHERE 生产线编号='{publicAddress.lineNumber}'";
                var hintRoute = sqlToolsYY.GetDataTable("", routeSql, true, out DataTable dtRoute);

                if (hintRoute.Valid && dtRoute.Rows.Count > 0)
                {
                    publicAddress.Gylx = dtRoute.Rows[0][0].ToString();

                    // 11. 调用API获取BOM数据
                    try
                    {
                        var bomService = new BomService();
                        //var repository = new BomRepository("Server=10.0.0.112;Database=MesServerSQL;User Id=sa;Password=longzhong7202128BGS;");
                        var repository = new BomRepository(SQLToolsYY.GetMesServerConnectionString());
                        var response = await bomService.GetBomDataAsync();

                        if (response.State == 0)
                        {
                            await repository.SaveBomDataAsync(response);
                        }
                        else
                        {
                            
                            this.Invoke((MethodInvoker)(() => publicAddress.AddLog($"API错误：{response.Mess}")));
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        
                        this.Invoke((MethodInvoker)(() => publicAddress.AddLog($"错误：{ex.Message}")));
                        return;
                    }
                }
                else
                {
                    
                    this.Invoke((MethodInvoker)(() => publicAddress.AddLog("该计划没有工艺路线，请录入后生产！")));
                    return;
                }

                // 12. 更新Modid
                string activeSql = "SELECT TOP 1 * FROM fynV_choicePlan WHERE 状态=1";
                var hintActive = sqlToolsYY.GetDataTable("", activeSql, true, out DataTable dtActive);
                if (hintActive.Valid && dtActive.Rows.Count > 0)
                {
                    //新增执行存储过程将当前计划的程序号插入到表里
                    string execProc1 = $"exec MesP_process_procedure ";
                    sqlToolsYY.GetDataTable("", execProc1, true, out DataTable _);
                    publicAddress.Modid = modId;
                }

                // 13. 刷新计划列表
                this.Invoke((MethodInvoker)refresh);
            });
        }

        /// <summary>
        /// 刷新计划按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e)
        {
            refresh();//刷新当前计划
        }


    }
}
