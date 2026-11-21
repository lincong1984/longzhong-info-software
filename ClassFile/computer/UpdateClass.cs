using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace generaControlProcedure
{
    class UpdateClass
    {
        /// <summary>
        /// 获取服务器更新文件夹xml信息
        /// </summary>
        /// <param name="filepath"></param>
        /// <param name="targePath"></param>
        /// <returns></returns>
        public bool UpdateVersion( string filepath)
        {
            bool result = false;
            try
            {
                string Content = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
                //Content.Text += "文件版本：" + Application.ProductVersion.ToString() + "\n";
                //Content.Text += "部署版本：" + System.Deployment.Application.ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString();
                string user = "xxgcb13";
                string password = "";
                string fileName = "UpdateList.xml";
                string fullstr = Assembly.GetExecutingAssembly().FullName;//获取当前程序集名称
                string projectName = fullstr.Substring(0, fullstr.IndexOf(","));//获取项目名称
                string DownlodePath = "ftp://10.0.0.6" + ":" + "21" + "/" + projectName + "/update";//服务器路径"
                Download(DownlodePath, user, password, fileName, filepath);
                //string CopyPath = "\\\\10.0.0.6\\e\\XXGCBXXHTS\\generaControlProcedure\\update";
                //CopyDirectory(CopyPath, filepath, true);
                string path = filepath + "\\" + fileName;
                XmlDocument doc = new XmlDocument();
                doc.Load(path);
                XmlNode root = doc.SelectSingleNode("UpdateMessage");
                XmlNode xn = root.SelectSingleNode("MessageTable");
                string versions = xn.Attributes["versions"].Value.ToString().Trim();
                //string fileName = xn.Attributes["fileName"].Value.ToString().Trim();
                if (Content != versions)
                {
                    //Download(fileName, Application.StartupPath+"\\"+ fileName);
                    System.Diagnostics.Process.Start(filepath + "\\Update.exe");//打开跟新软件
                    Application.Exit();
                }
                result = true;
            }
            catch (Exception )
            {
            }
            return result;
        }
        /// <summary>
        /// 复制粘贴文件
        /// </summary>
        /// <param name="SourcePath">复制文件路径</param>
        /// <param name="DestinationPath">粘贴文件路径</param>
        /// <param name="overwriteexisting">是否覆盖文件</param>
        /// <returns></returns>
        private bool CopyDirectory(string SourcePath, string DestinationPath, bool overwriteexisting)
        {
            bool ret = false;
            try
            {
                SourcePath = SourcePath.EndsWith(@"\") ? SourcePath : SourcePath + @"\";
                DestinationPath = DestinationPath.EndsWith(@"\") ? DestinationPath : DestinationPath + @"\";

                if (Directory.Exists(SourcePath))
                {
                    if (Directory.Exists(DestinationPath) == false)
                        Directory.CreateDirectory(DestinationPath);

                    foreach (string fls in Directory.GetFiles(SourcePath))
                    {
                        FileInfo flinfo = new FileInfo(fls);
                        flinfo.CopyTo(DestinationPath + flinfo.Name, overwriteexisting);
                    }
                    foreach (string drs in Directory.GetDirectories(SourcePath))
                    {
                        DirectoryInfo drinfo = new DirectoryInfo(drs);
                        if (CopyDirectory(drs, DestinationPath + drinfo.Name, overwriteexisting) == false)
                            ret = false;
                    }
                }
                ret = true;
            }
            catch (Exception )
            {
                ret = false;
            }
            return ret;
        }
        /// 从ftp服务器上下载文件
        /// </summary>
        /// <param name="ftpPath">FTP服务器路径</param>
        /// <param name="fileName">需要下载文件名称</param>
        /// <param name="targetPath">保存文件路径名称</param>
        public void Download(string ftpPath, string user, string password, string fileName, string targetPath)
        {
            FtpWebRequest reqFTP;
            try
            {
                //string filePath = Application.StartupPath;//获取当前运行文件路径
                //FileStream outputStream = new FileStream(filePath + "\\" + fileName, FileMode.Create);
                FileStream outputStream = new FileStream(targetPath + "\\" + fileName, FileMode.Create);//指定保存文件路径
                string str = ftpPath + "/" + fileName;//"ftp://10.0.6.17" + ":" + "21" + "/" + fileName;//服务器路径
                reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(str));//连接服务器
                reqFTP.Method = WebRequestMethods.Ftp.DownloadFile;//下载文件
                reqFTP.UseBinary = true;//指示服务器要传输的是二进制数据
                reqFTP.Credentials = new NetworkCredential(user, password);//FTP服务器连接用户名、密码
                reqFTP.UsePassive = false;//客户端应用程序的数据传输过程侦听数据端口上的连接
                FtpWebResponse response = (FtpWebResponse)reqFTP.GetResponse();//返回FTP服务器响应
                Stream ftpStream = response.GetResponseStream();
                long cl = response.ContentLength;//获取接收长度
                int bufferSize = 2048;
                int readCount;
                byte[] buffer = new byte[bufferSize];
                readCount = ftpStream.Read(buffer, 0, bufferSize);//接收数据
                while (readCount > 0)
                {
                    outputStream.Write(buffer, 0, readCount);
                    readCount = ftpStream.Read(buffer, 0, bufferSize);
                }
                ftpStream.Close();
                outputStream.Close();
                response.Close();
            }
            catch (Exception)
            {
            }
        }

    }
}
