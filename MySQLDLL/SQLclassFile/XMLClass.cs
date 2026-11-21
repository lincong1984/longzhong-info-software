using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Data;
using System.IO;

namespace fynClass
{
    class XMLClass
    {
        /// <summary>
        /// 保存数据库连接配置
        /// </summary>
        /// <param name="IP">IP地址</param>
        /// <param name="CONNECT_DATA">数据库名</param>
        /// <param name="User">用户名</param>
        /// <param name="Password">密码</param>
        /// <returns>成功标识</returns>
        public bool SaveConnectString(string IP, string CONNECT_DATA, string User, string Password)
        {
            bool returns = false;
            string connectString = null;
            try
            {
                XmlDataDocument doc = new XmlDataDocument();
                XmlElement connect = null;
                XmlElement booklst = doc.CreateElement("oracleConnectString");
                connect = doc.CreateElement("connectStr");
                connect.SetAttribute("IP", IP.Trim());
                connect.SetAttribute("CONNECT_DATA", CONNECT_DATA.Trim());
                connect.SetAttribute("User", User.Trim());
                connect.SetAttribute("Password", Password.Trim());
                connectString = "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=" + IP.Trim() + ")(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=" + CONNECT_DATA.Trim() + ")));Persist Security Info=True;User ID=" + User.Trim() + ";Password=" + Password.Trim() + ";";
                //connect.SetAttribute("connectString", connectString);
                booklst.AppendChild(connect);
                doc.AppendChild(booklst);
                doc.Save("oracleConnectString.xml");
                returns = true;
            }
            catch (Exception ex)
            {
                returns = false;
                throw ex;
            }
            return returns;
        }
        /// <summary>
        /// 获取连接Oracle字符串
        /// </summary>
        /// <param name="part">文件路径</param>
        /// <returns>参数表</returns>
        public string getConnectString(string part)
        {
            string connectStr = null;
            XmlDocument doc = new XmlDocument();
            doc.Load(part);
            XmlNode root = doc.SelectSingleNode("oracleConnectString");
            XmlNode xn = root.SelectSingleNode("connectStr");
            connectStr = "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=" + xn.Attributes["IP"].Value.ToString().Trim() + ")(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=" + xn.Attributes["CONNECT_DATA"].Value.ToString().Trim()+ ")));Persist Security Info=True;User ID=" + xn.Attributes["User"].Value.ToString().Trim() + ";Password=" + xn.Attributes["Password"].Value.ToString().Trim()+ ";";
            return connectStr;
        }
        /// <summary>
        /// 获取连接Oracle参数
        /// </summary>
        /// <param name="part">文件路径</param>
        /// <returns></returns>
        public DataTable getConnectparameter(string part)
        {
            DataTable dt = new DataTable("connectOracle");
            dt.Columns.Add("ip", typeof(string));
            dt.Columns.Add("connect_data");
            dt.Columns.Add("user", typeof(string));
            dt.Columns.Add("password", typeof(string));
            XmlDocument doc = new XmlDocument();
            doc.Load(part);
            XmlNode root = doc.SelectSingleNode("oracleConnectString");
            XmlNode xn = root.SelectSingleNode("connectStr");
            dt.Rows.Add(xn.Attributes["IP"].Value.ToString().Trim() , xn.Attributes["CONNECT_DATA"].Value.ToString().Trim(), xn.Attributes["User"].Value.ToString().Trim() , xn.Attributes["Password"].Value.ToString().Trim());
            return dt;
        }
        /// <summary>
        /// 查询IP地址
        /// </summary>
        /// <param name="ip">ip地址</param>
        /// <returns></returns>
        public DataTable selectelement(string id)
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("ID", typeof(string));
            dt.Columns.Add("IP", typeof(string));
            dt.Columns.Add("设备名称", typeof(string));
            dt.Columns.Add("设备编号", typeof(string));
            XmlDocument doc = new XmlDocument();
            try
            {
                if (File.Exists(@"IPAddress.xml"))
                {
                    doc.Load("IPAddress.xml");
                    XmlNodeList nlist = doc.SelectNodes("IPAddress/IPTable");
                    foreach (XmlNode nx in nlist)
                    {
                        if (id != "" && nx.Attributes["id"].Value == id)
                        {
                            dt.Rows.Add(nx.Attributes["id"].Value, nx.Attributes["IP"].Value, nx.Attributes["devicename"].Value, nx.Attributes["deviceCode"].Value);
                        }
                        else
                        {
                            if (id == "-1")
                            {
                                dt.Rows.Add(nx.Attributes["id"].Value, nx.Attributes["IP"].Value, nx.Attributes["devicename"].Value, nx.Attributes["deviceCode"].Value);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return dt;
        }
        /// <summary>
        /// 修改IP地址表
        /// </summary>
        /// <param name="ip"></param>
        /// <returns></returns>
        public bool updateelement(string id,string ip, string devicename, string deviceCode)
        {
            bool bools = false;
            XmlDocument doc = new XmlDocument();
            try
            {
                if (File.Exists(@"IPAddress.xml"))
                {
                    doc.Load("IPAddress.xml");
                    XmlNodeList nlist = doc.SelectNodes("IPAddress/IPTable");
                    XmlNode appsetting = doc.SelectSingleNode("IPAddress");
                    foreach (XmlNode xn in nlist)
                    {
                        if (xn.Attributes["id"].Value == id)
                        {
                            xn.Attributes["IP"].Value = ip;
                            xn.Attributes["devicename"].Value = devicename;
                            xn.Attributes["deviceCode"].Value = deviceCode;
                        }
                    }
                    doc.Save("IPAddress.xml");
                    bools = true;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return bools;
        }
        /// <summary>
        /// 删除IP地址
        /// </summary>
        /// <param name="IP"></param>
        /// <returns></returns>
        public bool deleteelement(string IP)
        {
            XmlDocument doc = new XmlDocument();
            bool bools = false;
            try
            {
                if (File.Exists(@"IPAddress.xml"))
                {
                    doc.Load("IPAddress.xml");
                    XmlNodeList nlist = doc.SelectNodes("IPAddress/IPTable");//获取了所有叫add的节点，而不是父节点的所有子节点
                    XmlNode appsetting = doc.SelectSingleNode("IPAddress");
                    foreach (XmlNode xn in nlist)
                    {
                        if (xn.Attributes["IP"].Value == IP)
                        {
                            appsetting.RemoveChild(xn);
                        }
                    }
                    doc.Save("IPAddress.xml");
                    bools = true;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return bools;
        }
        /// <summary>
        /// 添加IP地址
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="devicename"></param>
        /// <param name="deviceCode"></param>
        /// <returns></returns>
        public bool addelement(string ip, string devicename, string deviceCode)
        {
            XmlDocument doc = new XmlDocument();
            XmlElement iptable = null;
            bool bools = false;
            try
            {
                if (File.Exists(@"IPAddress.xml"))
                {
                    doc.Load("IPAddress.xml");
                    XmlNode root = doc.SelectSingleNode("IPAddress");
                    XmlNode iproot = root.SelectSingleNode("IPTable");
                    int idmax = 0;
                    for (int i = 0; i < root.ChildNodes.Count; i++)
                    {
                        int id_1 = Convert.ToInt32(root.ChildNodes[i].Attributes["id"].Value);
                        if (id_1 > idmax)
                            idmax = id_1;
                    }
                    iptable = doc.CreateElement("IPTable");
                    iptable.SetAttribute("id", (idmax + 1).ToString());
                    iptable.SetAttribute("IP", ip);
                    iptable.SetAttribute("devicename", devicename);
                    iptable.SetAttribute("deviceCode", deviceCode);
                    root.AppendChild(iptable);
                    doc.Save("IPAddress.xml");
                }
                else
                {
                    XmlElement booklst = doc.CreateElement("IPAddress");
                    doc.AppendChild(booklst);
                    iptable = doc.CreateElement("IPTable");
                    iptable.SetAttribute("id", "1");
                    iptable.SetAttribute("IP", ip);
                    iptable.SetAttribute("devicename", devicename);
                    iptable.SetAttribute("deviceCode", deviceCode);
                    booklst.AppendChild(iptable);
                    doc.Save("IPAddress.xml");
                }
                bools = true;
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return bools;
        }
        /// <summary>
        /// 添加设备消息表
        /// </summary>
        /// <param name="ip">IP地址</param>
        /// <param name="cmd">命令码</param>
        /// <param name="project">项目</param>
        /// <param name="projectName">项目名</param>
        /// <param name="value">结果</param>
        /// <param name="readTime">时间</param>
        /// <returns></returns>
        public bool addcoordinate(string ip, string cmd, string project, string projectName, string  value, string readTime)
        {
            XmlDocument doc = new XmlDocument();
            XmlElement table = null;
            bool bools = false;
            try
            {
                if (File.Exists(@"CNCmessage.xml"))
                {
                    doc.Load("CNCmessage.xml");
                    XmlNode root = doc.SelectSingleNode("CNCmessage");
                    XmlNode iproot = root.SelectSingleNode("CNCmessageTable");
                    int idmax = 0;
                    for (int i = 0; i < root.ChildNodes.Count; i++)
                    {
                        int id_1 = Convert.ToInt32(root.ChildNodes[i].Attributes["id"].Value);
                        if (id_1 > idmax)
                            idmax = id_1;
                    }
                    table = doc.CreateElement("CNCmessageTable");
                    table.SetAttribute("id", (idmax + 1).ToString());//行ID
                    table.SetAttribute("IP", ip);//IP地址
                    table.SetAttribute("cmd", cmd);//命令码
                    table.SetAttribute("project", project);//项目
                    table.SetAttribute("projectName", projectName);//项目名
                    table.SetAttribute("value", value);//结果
                    table.SetAttribute("readTime", readTime);//时间
                    root.AppendChild(table);
                    doc.Save("CNCmessage.xml");
                }
                else
                {
                    XmlElement booklst = doc.CreateElement("CNCmessage");
                    doc.AppendChild(booklst);
                    table = doc.CreateElement("CNCmessageTable");
                    table.SetAttribute("IP", ip);//IP地址
                    table.SetAttribute("cmd", cmd);//命令码
                    table.SetAttribute("project", project);//项目
                    table.SetAttribute("projectName", projectName);//项目名
                    table.SetAttribute("value", value);//结果
                    table.SetAttribute("readTime", readTime);//时间
                    booklst.AppendChild(table);
                    doc.Save("CNCmessage.xml");
                }
                bools = true;
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return bools;
        }
        /// <summary>
        /// 更新设备消息表
        /// </summary>
        /// <param name="id">行ID</param>
        /// <param name="ip">IP地址</param>
        /// <param name="cmd">命令码</param>
        /// <param name="project">项目</param>
        /// <param name="projectName">项目名</param>
        /// <param name="value">结果</param>
        /// <param name="readTime">时间</param>
        /// <returns></returns>
        public bool updatecoordinate(string id, string ip, string cmd, string project, string projectName, string value, string readTime)
        {
            bool bools = false;
            XmlDocument doc = new XmlDocument();
            try
            {
                if (File.Exists(@"CNCmessage.xml"))
                {
                    doc.Load("CNCmessage.xml");
                    XmlNodeList nlist = doc.SelectNodes("CNCmessage/CNCmessageTable");
                    XmlNode appsetting = doc.SelectSingleNode("CNCmessage");
                    foreach (XmlNode xn in nlist)
                    {
                        if (xn.Attributes["id"].Value == id)
                        {
                            xn.Attributes["IP"].Value = ip;
                            xn.Attributes["cmd"].Value = cmd;
                            xn.Attributes["project"].Value = project;
                            xn.Attributes["projectName"].Value = projectName;
                            xn.Attributes["value"].Value = value;
                            xn.Attributes["readTime"].Value = readTime;
                        }
                    }
                    doc.Save("CNCmessage.xml");
                    bools = true;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return bools;
        }
        /// <summary>
        /// 删除设备消息表
        /// </summary>
        /// <param name="IP">IP地址</param>
        /// <param name="cmd">命令码</param>
        /// <returns></returns>
        public bool deletecoordinate(string IP, string cmd)
        {
            XmlDocument doc = new XmlDocument();
            bool bools = false;
            try
            {
                if (File.Exists(@"CNCmessage.xml"))
                {
                    doc.Load("CNCmessage.xml");
                    XmlNodeList nlist = doc.SelectNodes("CNCmessage/CNCmessageTable");//获取了所有叫add的节点，而不是父节点的所有子节点
                    XmlNode appsetting = doc.SelectSingleNode("CNCmessage");
                    foreach (XmlNode xn in nlist)
                    {
                        if ((xn.Attributes["IP"].Value == IP && xn.Attributes["cmd"].Value ==cmd) || (xn.Attributes["IP"].Value == IP && cmd==""))
                        {
                            appsetting.RemoveChild(xn);
                        }
                    }
                    doc.Save("CNCmessage.xml");
                    bools = true;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return bools;
        }
        /// <summary>
        /// 查询设备消息表
        /// </summary>
        /// <param name="SelectStr">查询条件</param>
        /// <returns></returns>
        public DataTable selectcoordinate(string SelectStr,string path)
        {
            DataTable dt = new DataTable();
            DataSet ds = new DataSet();
            DataView dv = new DataView();
            XmlDocument doc = new XmlDocument();
            try
            {
                if (File.Exists(path))
                {
                    
                    ds.ReadXml(path);
                    dv = ds.Tables[0].DefaultView;
                    dv.RowFilter = SelectStr;
                    //dv.Sort = "id";
                    dt = dv.ToTable();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return dt;
        }
    }
}
