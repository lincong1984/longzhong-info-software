using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MES_SCAF
{
    class convertType
    {
        /// <summary>
        /// int转化为byte[]:
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        public byte[] IntToBitConverter(int num)
        {
            byte[] bytes = BitConverter.GetBytes(num);
            return bytes;
        }
        /// <summary>
        /// byte[] 转化为int:
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public int BitConverterToInt(byte[] bytes)
        {
            int temp = BitConverter.ToInt32(bytes, 0);
            return temp;
        }
        /// <summary>
        /// string转化为byte[]
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public byte[] stringToByte(string str)
        {
            byte[] byteArray = System.Text.Encoding.ASCII.GetBytes(str);
            return byteArray;
        }
        /// <summary>
        /// byte[]转化为string
        /// </summary>
        /// <param name="byteArray"></param>
        /// <returns></returns>
        public string ByteToString(byte[] byteArray)
        {
            string str = System.Text.Encoding.ASCII.GetString(byteArray).TrimEnd('\0');
            return str;
        }
        public bool[] IntToBoolArray(int inputNum)
        {
            bool[] bArray = new bool[32];
            //int inputNum = 3;
            int endNum;
            for (int i = 0; i < 32; i++)
            {
                endNum = 1 << i;
                if (endNum == (endNum & inputNum))
                {
                    bArray[i] = true;
                }
                else
                {
                    bArray[i] = false;
                }
            }
            return bArray;
        }
    }
}
