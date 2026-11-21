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
    public partial class monitoringForm : Form
    {
        public monitoringForm()
        {
            InitializeComponent();
        }
        Hint hint = new Hint();
        SQLToolsYY sql = new SQLToolsYY();
        DataTable fynT_PLCaddress = new DataTable();
        /// <summary>
        /// 刷新treeView
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void refreshTreeView()
        {
            foreach (DataTable table in publicAddress.dataset.Tables)
            {
                string PLCstationNum = table.TableName.Substring(4, table.TableName.Length - 4);
                DataRow[] datarow = fynT_PLCaddress.Select("PLCstationNum='"+ PLCstationNum+"'");
                string nodeName = datarow[0]["PLCname"].ToString();
                TreeNode tn = FindNode(this.treeView1, nodeName);
                if (tn == null)
                {
                    this.treeView1.Nodes.Add(nodeName);
                }

            }
            

        }

        private void nodeDouble()
        {
            /*
            DataGridView dgv = new DataGridView();
            dgv.Dock = System.Windows.Forms.DockStyle.Fill;
            TabPage Page = new TabPage();
            Page.Name = "Page" + 1.ToString();
            Page.Text = "tabPage" + 2.ToString();
            Page.TabIndex = 1;
            this.tabControl1.Controls.Add(Page);
            this.tabControl1.SelectedTab = Page;
            Page.Controls.Add(dgv);*/

            string selectNode = this.treeView1.SelectedNode.Text;
            DataRow[] datarows = fynT_PLCaddress.Select("PLCname='" + selectNode + "'");
            string tableName = "PLC_" + datarows[0]["PLCstationNum"].ToString();
            if (!FindTabPage(this.kkTab2, selectNode))
            {
                DataGridView dgv = new DataGridView();
                dgv.Dock = System.Windows.Forms.DockStyle.Fill;
                dgv.DataSource = publicAddress.dataset.Tables[tableName];
                

                TabPage Page = new TabPage();
                Page.Name = tableName;
                Page.Text = selectNode;
                Page.TabIndex = 1;
                this.kkTab2.Controls.Add(Page);
                this.kkTab2.SelectedTab = Page;
                Page.Controls.Add(dgv);
                dgv.Columns["category"].HeaderText = "类别";
                dgv.Columns["PLCstationNum"].HeaderText = "PLC站号";
                dgv.Columns["address"].HeaderText = "地址";
                dgv.Columns["AddressAnnotation"].HeaderText = "注释";
                dgv.Columns["dataType"].HeaderText = "数据类型";
                dgv.Columns["decimalPlaces"].HeaderText = "小数点位数";
                dgv.Columns["workNumber"].HeaderText = "工位号";
                dgv.Columns["intOprNo"].HeaderText = "工序号";
                dgv.Columns["decideRelevanceAddress"].HeaderText = "子合格信息绑定地址";
                dgv.Columns["reciprocate"].HeaderText = "是否字节互换";
                dgv.Columns["value"].HeaderText = "交互信息";
            }
            else
            {
                foreach (TabPage tab in this.kkTab2.TabPages)
                {
                    if (tab.Text == selectNode)
                    {
                        this.kkTab2.SelectedTab = tab;
                        break;
                    }
                }
                
            }
            
        }
        /// <summary>
        /// 窗体加载
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void monitoringForm_Load(object sender, EventArgs e)
        {
            hint = sql.GetDataTable("", "SELECT * FROM fynT_PLCaddress", true, out fynT_PLCaddress);
            refreshTreeView();
        }

        /// <summary>
        /// 查找TreeView控件下是否有指定值的节点
        /// </summary>
        /// <param name="tvControl">TreeView控件</param>
        /// <param name="strValue">指定节点值</param>
        /// <returns></returns>
        public TreeNode FindNode(TreeView tvControl, string strValue)
        {
            TreeNode tnRet = null;

            foreach (TreeNode tn in tvControl.Nodes)
            {
                tnRet = FindNodes(tn, strValue);
                if (tnRet != null) break;
            }
            return tnRet;
        }
        /// <summary>
        /// 查找当前节点下是否有指定值的节点
        /// </summary>
        /// <param name="tnParent">当前节点</param>
        /// <param name="strValue">指定节点值</param>
        /// <returns>TreeNode</returns>
        public TreeNode FindNodes(TreeNode tnParent, string strValue)
        {
            if (tnParent == null) return null;
            if (tnParent.Text == strValue) return tnParent;
            TreeNode tnRet = null;
            TreeNode tnTemp = null;
            foreach (TreeNode tn in tnParent.Nodes)
            {
                tnTemp = FindNodes(tn, strValue);
                if (tnTemp != null && tnTemp.Text == strValue)
                {
                    tnRet = tnTemp;
                    break;
                }
            }
            return tnRet;
        }
        public bool FindTabPage(TabControl tabcontrol, string strValue)
        {
            bool bot = false;
            foreach (TabPage tab in tabcontrol.TabPages)
            {
                if (tab.Text == strValue)
                {
                    bot = true;
                    break;
                }
            }
            return bot;
        }
        /// <summary>
        /// 双击节点
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void nodeDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            nodeDouble();
        }
    }
}
