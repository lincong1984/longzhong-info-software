using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Windows.Forms;
using System.Threading;

namespace fynComClass
{
    public class ComClass
    {
        /// <summary>
        /// 申明委托
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public delegate void BroadcastEventHander(object sender, CustomerEventArgs e);
        /// <summary>
        /// 声明委托相关的事件
        /// </summary>
        public event BroadcastEventHander DataRead;
        public  SerialPort sp = new SerialPort();//实例化串口通讯类      
        private string strCode;
        public string StrCode
        {
            get
            {
                return strCode;
            }

            set
            {
                strCode = value;
            }
        }
        CustomerEventArgs customerEventArgs = new CustomerEventArgs();
        public enum ParityBits
        {
            无=0,
            奇=1,
            偶=2
        }
        /// <summary>
        /// 打开串口
        /// </summary>
        /// <param name="strPortName">设定端口号</param>
        /// <param name="strBaudRate">设定波特率</param>
        /// <param name="strDataBits">设定数据位</param>
        /// <param name="strStopBits">设定停止位</param>
        /// <param name="ParityBits">设定校验方式</param>
        /// <returns></returns>
        public bool openCom(string strPortName,string strBaudRate,string strDataBits,string strStopBits, ParityBits ParityBits)
        {
            bool return_1 = false;
            //SerialPort sp = new SerialPort();//实例化串口通讯类
            sp.Encoding = System.Text.Encoding.GetEncoding("GB2312");//设定信息交换用汉字编码字符集位GB2312
            if (strPortName != "" && strBaudRate != "" && strDataBits != "" && strStopBits != "")
            {
                try
                {
                    sp.PortName = strPortName;//设定端口号
                    sp.BaudRate = Convert.ToInt32(strBaudRate);//设定波特率
                    sp.DataBits = Convert.ToInt16(strDataBits);//设定数据位
                    sp.StopBits = (StopBits)int.Parse(strStopBits);//设定停止位
                    switch (ParityBits)//设定校验方式
                    {
                        case ParityBits.无: sp.Parity = Parity.None; break;//无校验
                        case ParityBits.奇: sp.Parity = Parity.Odd; break;//奇校验
                        case ParityBits.偶: sp.Parity = Parity.Even; break;//偶校验
                    }
                    sp.ReadTimeout = 500;//通讯超时时间
                    /*判断串口是否已经打开*/
                    if (sp.IsOpen)
                    {
                        sp.Close();
                        sp.Open();
                    }
                    else
                    {
                        sp.Open();
                    }
                    sp.DataReceived += Sp_DataReceived;
                    return_1 = true;
                }
                catch (Exception ex)
                {
                    return_1 = false;
                    MessageBox.Show("错误：" + ex.Message, "串口通讯错误");
                }
            }
            return return_1;
        }
        /// <summary>
        /// 串口数据接收事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Sp_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                customerEventArgs.ReadState = false;
                StrCode = "";
                if (sp.IsOpen)
                {
                    Thread.Sleep(100);
                    customerEventArgs.ReadCodeLength = sp.BytesToRead;
                    StrCode = sp.ReadExisting();
                    sp.DiscardInBuffer();
                    StrCode = StrCode.Replace("\r", "\r\n");
                    if (StrCode.Contains("\r\n"))
                    {
                        customerEventArgs.ReadCode = StrCode.Replace("\r", "").Replace("\n", "");
                    }
                    customerEventArgs.ReadState = true;
                }
                
            }
            catch (Exception )
            {
                customerEventArgs.ReadState = false;
            }
            DataRead(StrCode, customerEventArgs); // 产生事件
        }
        /// <summary>
        /// 串口数据发送
        /// </summary>
        /// <param name="writeStr">发送的字符串</param>
        /// <returns></returns>
        public bool Write(string writeStr)
        {
            try
            {
                if (sp.IsOpen)
                {
                    sp.Write(writeStr);
                    Thread.Sleep(100);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
        /// <summary>
        /// 关闭串口
        /// </summary>
        /// <returns></returns>
        public bool CloseCom()
        {
            try
            {
                if (sp.IsOpen)
                    sp.Close();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
            
        }

    }
    public class CustomerEventArgs : EventArgs
    {
        private string readCode;
        public string ReadCode
        {
            get
            {
                return readCode;
            }

            set
            {
                readCode = value;
            }
        }
        private bool readState;
        public bool ReadState
        {
            get
            {
                return readState;
            }

            set
            {
                readState = value;
            }
        }
        private int readCodeLength;
        public int ReadCodeLength
        {
            get
            {
                return readCodeLength;
            }

            set
            {
                readCodeLength = value;
            }
        }
    }
}
