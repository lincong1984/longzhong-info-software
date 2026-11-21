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
    public partial class messageForm : Form
    {
        public messageForm()
        {
            InitializeComponent();
        }
        private string _calssCode = string.Empty;
        /// <summary>
        /// 物资类别
        /// </summary>
        public string CalssCode
        {
            get
            {
                return _calssCode;
            }
            set
            {
                _calssCode = value;
            }
        }
        private void messageForm_Load(object sender, EventArgs e)
        {
            
        }

        private void textChangde(object sender, EventArgs e)
        {
            publicAddress.reasonText = this.reasonTextBox.Text;
        }
    }
}
