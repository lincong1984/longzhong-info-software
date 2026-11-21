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
    public partial class ErrorForm : Form
    {
        public ErrorForm()
        {
            InitializeComponent();
        }
        SQLToolsYY sql = new SQLToolsYY();
        private void ErrorForm_Load(object sender, EventArgs e)
        {
            reporesh();
        }
        private void reporesh()
        {

            string str = "select * from fynV_errorDescribe";
            DataTable dt = new DataTable();
            Hint hint = sql.GetDataTable("", str, true, out dt);
            if (hint.Valid)
            {
                this.dataGridView1.DataSource = dt.DefaultView;
                for (int i = 0; i < this.dataGridView1.Rows.Count; i++)
                {
                    if (Convert.ToInt16(this.dataGridView1.Rows[i].Cells["状态"].Value) != 0)
                    {
                        this.dataGridView1.Rows[i].DefaultCellStyle.BackColor = Color.Red;//将颜色设定为绿色
                    }
                    else
                    {
                        this.dataGridView1.Rows[i].DefaultCellStyle.BackColor = Color.Lime;//将颜色设定为红色
                    }
                    this.dataGridView1.Columns["报警信息"].Width = 300;
                }
            }
            else
            {
                MessageBox.Show(hint.Msg);
            }

        }

        private void toolStripButtonReporesh_Click(object sender, EventArgs e)
        {
            reporesh();
        }
    }
}
