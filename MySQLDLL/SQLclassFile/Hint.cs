using System;
using System.Collections.Generic; 
using System.Text;

namespace generaControlProcedure
{
    using System; 

    public class Hint
    {
        private string msg = "未捕获异常";
        private bool valid = false;

        public string Msg
        {
            get
            {
                return this.msg;
            }
            set
            {
                this.msg = value;
            }
        }

        public bool Valid
        {
            get
            {
                return this.valid;
            }
            set
            {
                this.valid = value;
            }
        }
    }
}
