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
    public partial class roleForm : Form
    {
        
        
        public roleForm()
        {
            InitializeComponent();
            
        }

        SQLToolsYY sqlToolsYY = new SQLToolsYY();
        
        public void refreshRole(DataGridView dataGrowView)
        {
            string selectsql = "select * from fynV_role";
            DataTable dt = new DataTable();
            Hint hint = sqlToolsYY.GetDataTable("", selectsql, true, out dt);
            if (hint.Valid == false)
                MessageBox.Show("错误：" + hint.Msg);
            dataGrowView.DataSource = dt.DefaultView;
            dataGrowView.Columns[0].Width = 80;//设定第一列列宽为70
            dataGrowView.Columns[1].Width = 150;//设定第二列列宽为100
            dataGrowView.Columns[0].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;//设定单元格字体居中
            if (this.role.Rows.Count > 0)
            {
                int index =Convert.ToInt32(this.role.Rows[0].Cells[0].Value);//获取当前选择行的ID
                if (index != 0)
                {
                    refreshTreeView(index);
                }
            }
        }
        /// <summary>
        /// 单击鼠标选择角色时获取对应的权限
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CellClick(object sender, DataGridViewCellEventArgs e)
        {
            int index =Convert.ToInt32( this.role.Rows[this.role.CurrentCell.RowIndex].Cells[0].Value);//获取当前选择行的ID
            if (index != 0)
            {
                refreshTreeView(index);
            }
        }
        /// <summary>
        /// 窗体加载时运行
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void roleFormLoad(object sender, EventArgs e)
        {
            refreshRole(this.role);//刷新角色表
            this.treeView1.AfterCheck += TreeView1_AfterCheck;//单击复选框时触发事件

        }
        /// <summary>
        /// 保存按钮按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void save_Click(object sender, EventArgs e)
        {
            saverole();//保存权限
        }
        /// <summary>
        /// 删除角色
        /// </summary>
        /// <param name="index"></删除行ID>
        public void deleterole(string index)
        {
            try
            {
                string selectsql = "delete fynT_role where roleEncoding=" + index;
                DataTable dt = new DataTable();
                Hint hint = sqlToolsYY.GetDataTable("", selectsql, true, out dt);
                if (hint.Valid == false)
                    MessageBox.Show("错误：" + hint.Msg);
                refreshRole(this.role);//刷新角色表
            }
            catch (Exception )
            { }
        }
        /// <summary>
        /// 保存权限
        /// </summary>
        public void saverole()
        {
            try
            {
                DataTable dt = new DataTable();
                dt.Columns.Add("name", typeof(string));
                dt.Columns.Add("class", typeof(string));
                dt.Columns.Add("state", typeof(string));
                travelNode(this.treeView1, ref dt);
                int roleID = Convert.ToInt32(this.role.Rows[this.role.CurrentCell.RowIndex].Cells[0].Value);
                string sql = "";
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    sql = sql+ "exec fynP_saveRoleJurisdiction '" + dt.Rows[i]["name"].ToString() + "','" + dt.Rows[i]["class"].ToString() + "'," + Convert.ToInt32(Convert.ToBoolean(dt.Rows[i]["state"])).ToString() + "," + roleID.ToString()+"\r\n";
                }
                Hint hint = sqlToolsYY.GetDataTable("", sql, true, out dt);
                if (hint.Valid == false)
                    MessageBox.Show("错误：" + hint.Msg);
                MessageBox.Show("保存成功！");
            }
            catch (Exception )
            { }
        }

        
        /// <summary>
        /// 删除按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void delete_Click(object sender, EventArgs e)
        {
            string ID = this.role.Rows[this.role.CurrentCell.RowIndex].Cells["序号"].Value.ToString();//获取角色ID
            deleterole(ID);//删除角色
        }
        
        /// <summary>
        /// 打开用户窗口
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripButton1_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            refreshTreeView(14);


        }
        /// <summary>
        /// 刷新TreeView
        /// </summary>
        /// <param name="role">角色ID</param>
        private void refreshTreeView(int role)
        {
            this.treeView1.Nodes.Clear();
            DataTable dt = new DataTable();
            Hint hint = new Hint();
            string sqlstr = "exec fynP_getRoleJurisdiction "+role.ToString();
            hint = sqlToolsYY.GetDataTable("", sqlstr, true, out dt);
            for (int i = 0; i < dt.Rows.Count; i++)
            {

                string classes = dt.Rows[i]["classes"].ToString();
                if (classes.Length <= 2)
                {
                    this.treeView1.Nodes.Add(dt.Rows[i]["jurisdictionFunction"].ToString());

                    this.treeView1.Nodes[this.treeView1.Nodes.Count - 1].Tag = dt.Rows[i]["classes"].ToString();
                    this.treeView1.Nodes[this.treeView1.Nodes.Count - 1].Checked=Convert.ToBoolean(dt.Rows[i]["jurisdiction"]);
                }
                TreeNode tn = FindNode(this.treeView1, classes.Substring(0, classes.Length - 2));
                if (tn != null)
                {
                    tn.Nodes.Add(dt.Rows[i]["jurisdictionFunction"].ToString());
                    tn.Nodes[tn.Nodes.Count - 1].Tag = dt.Rows[i]["classes"].ToString();
                    tn.Nodes[tn.Nodes.Count - 1].Checked= Convert.ToBoolean(dt.Rows[i]["jurisdiction"]);
                }
            }
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
            if (tnParent.Tag.ToString() == strValue) return tnParent;
            TreeNode tnRet = null;
            TreeNode tnTemp = null;
            foreach (TreeNode tn in tnParent.Nodes)
            {
                tnTemp = FindNodes(tn, strValue);
                if (tnTemp != null && tnTemp.Tag.ToString() == strValue)
                {
                    tnRet = tnTemp;
                    break;
                }
            }
            return tnRet;
        }



        #region 复选框选择判断
        /// <summary>
        /// 判断同级的节点是否全选
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        private bool nextCheck(TreeNode n)   //判断同级的节点是否全选
                {
                    foreach (TreeNode tn in n.Parent.Nodes)
                    {
                        if (tn.Checked == false) return false;
                    }
                    return true;
                }
        /// <summary>
        /// 判断同级的节点是否全不选
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        private bool nextNotCheck(TreeNode n)  //判断同级的节点是否全不选
        {
            if (n.Checked == true)
            {
                return false;
            }
            if (n.NextNode == null)
            {
                return true;
            }

            return this.nextNotCheck(n.NextNode);
        }
        /// <summary>
        /// 遍历节点下的子节点
        /// </summary>
        /// <param name="tn"></param>
        /// <param name="check"></param>
        private void cycleChild(TreeNode tn, bool check)    //遍历节点下的子节点
        {
            if (tn.Nodes.Count != 0)
            {
                foreach (TreeNode child in tn.Nodes)
                {
                    child.Checked = check;
                    if (child.Nodes.Count != 0)
                    {
                        cycleChild(child, check);
                    }
                }
            }
            else
                return;
        }
        /// <summary>
        /// 遍历节点上的父节点
        /// </summary>
        /// <param name="tn"></param>
        /// <param name="check"></param>
        private void cycleParent(TreeNode tn, bool check)    //遍历节点上的父节点
        {
            if (tn.Parent != null)
            {
                if (nextCheck(tn))
                {
                    tn.Parent.Checked = true;
                }
                else
                {
                    tn.Parent.Checked = false;
                }
                cycleParent(tn.Parent, check);
            }
            return;
        }

        /// <summary>
        /// 当选中或取消选中树节点上的复选框时发生
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TreeView1_AfterCheck(object sender, TreeViewEventArgs e)
        {
            //要求父节点被勾选，则子节点全部被勾选；父节点不被勾选，则子节点不全不被勾选
            if (e.Node.Checked == true)
            {
                if (e.Action != TreeViewAction.Unknown)
                {
                    cycleChild(e.Node, true);
                }
                if (e.Node.Parent != null)
                {
                    if (nextCheck(e.Node))
                    {
                        cycleParent(e.Node, true);
                    }
                    else
                    {
                        cycleParent(e.Node, false);
                    }
                }
            }

            if (e.Node.Checked == false)
            {
                if (e.Action != TreeViewAction.Unknown)
                {
                    cycleChild(e.Node, false);  //中间节点不选中则子节点全部不选中
                    cycleParent(e.Node, false);       //父节点不选中
                }
                // bCheck = false;
            }
            return;
        }
        #endregion 复选框选择判断
        private void button2_Click(object sender, EventArgs e)
        {
            string aa= this.treeView1.Nodes[0].Tag.ToString();
            


        }
        /// <summary>
        /// 查找TreeView控件下的说有节点
        /// </summary>
        /// <param name="tv"></param>
        public void travelNode(TreeView tv,ref DataTable dt)
        {
            if (tv.Nodes != null)
            {
                foreach (TreeNode tn in tv.Nodes)
                {
                    travelNodes(tn,ref dt);
                }
            }
        }
        /// <summary>
        /// 查找当前节点下的所有节点
        /// </summary>
        /// <param name="tn"></param>
        public void travelNodes(TreeNode tn ,ref DataTable dt)
        {
            
            if (tn != null)
            {
                dt.Rows.Add(tn.Text, tn.Tag.ToString(), tn.Checked.ToString());
                foreach (TreeNode tn1 in tn.Nodes)
                {
                    travelNodes(tn1,ref dt);
                }
            }
        }
        /// <summary>
        /// 添加角色按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void addrole_Click(object sender, EventArgs e)
        {
            addRoleForm f = new addRoleForm();
            f.ShowDialog();
            refreshRole(this.role);

        }
    }
}
