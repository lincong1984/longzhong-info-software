using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace generaControlProcedure
{
    class publicMethod
    {
        /// <summary>
        /// 判断输入的字符串是否只包含数字和英文字母
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static bool IsNumAndEnCh(string input)
        {
            string pattern = @"^[A-Za-z0-9-]+$";
            Regex regex = new Regex(pattern);
            bool returnBOOL= regex.IsMatch(input);
            return returnBOOL;
        }
        /// <summary>
        /// 判断输入的是否为纯数字
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static bool IsNum(string input)
        {
            string pattern = @"^[0-9]+$";
            Regex regex = new Regex(pattern);
            bool returnBOOL = regex.IsMatch(input);
            return  returnBOOL ;
        }
        /// <summary>
        /// 判断输入的是否为纯字母
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static bool IsEnCh(string input)
        {
            string pattern = @"^[A-Za-z]+$";
            Regex regex = new Regex(pattern);
            return regex.IsMatch(input);
        }
    }
}
