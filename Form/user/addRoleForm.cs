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
    public partial class addRoleForm : Form
    {
        public addRoleForm()
        {
            InitializeComponent();
        }
        SQLToolsYY sql = new SQLToolsYY();
        private void savebutton_Click(object sender, EventArgs e)
        {
            DataTable dt = new DataTable();
            string sqlstr = "insert into fynT_role (roleName) values('"+this.roletext.Text.Trim()+"')";
            Hint hint = sql.GetDataTable("", sqlstr, true, out dt);
            if (hint.Valid != true)
                MessageBox.Show(hint.Msg);
            this.Close();
        }
    }
}
