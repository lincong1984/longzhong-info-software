using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.IO;
using System.Data;

namespace fyn_XML
{
    /// <summary>
    /// 读取或写入配置文件
    /// </summary>
    public class WriteReadXML
    {
        /// <summary>
        /// 读取配置文件
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public string Read(string xmlPath, string name,string Node)
       {
            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(xmlPath);
                XmlNode root = xmlDoc.SelectSingleNode(Node);//指向根节点
                XmlNode xn = root.SelectSingleNode(name);//指向根节点下的serv_ip节点
                string RuturnName = xn.InnerText;//读出里面的值 注意读取的是string 需要类型转换的话自己做
                return RuturnName;
            }
            catch (Exception ex)
            {
                return null;
            }
        }
        /// <summary>
        /// 写入配置文件
        /// </summary>
        /// <param name="name"></param>
        /// <param name="vlues"></param>
        /// <returns></returns>
        public bool Write(string xmlPath, string name,string vlues,string Node)
        {
            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(xmlPath);
                XmlNode root = xmlDoc.SelectSingleNode(Node);//指向根节点
                XmlNode xn = root.SelectSingleNode(name);//指向根节点下的serv_ip节点
                xn.InnerText = vlues;
                xmlDoc.Save(xmlPath);
                return true;
            }

            catch (Exception ex)
            {
                return false;
            }
        }
        /// <summary>
        /// 添加节点
        /// </summary>
        /// <param name="xmlPath">文件路径\文件名称</param>
        /// <param name="createNode">根节点名称</param>
        /// <param name="name">名称</param>
        /// <param name="values">值</param>
        /// <returns></returns>
        public bool Create(string xmlPath,string createNode,string nodeName,string name,string value)
        {
            try
            {
                if (!File.Exists(xmlPath))
                {
                    CreateXmlFile(xmlPath, createNode);
                }
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(xmlPath);
                CreateNode(xmlDoc, nodeName, name, value);//添加节点
                xmlDoc.Save(xmlPath);
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        /// <summary>
        /// 修改值
        /// </summary>
        /// <param name="xmlPath">文件路径\文件名称</param>
        /// <param name="selectSingleNode">查询节点</param>
        /// <param name="name">名称</param>
        /// <param name="values">值</param>
        /// <returns></returns>
        public bool CreateAttribute(string xmlPath,string selectSingleNode, string name, string values)
        {
            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(xmlPath);
                var root = xmlDoc.DocumentElement;//取到根结点
                XmlElement node = (XmlElement)xmlDoc.SelectSingleNode(selectSingleNode);
                node.InnerText = values;
                //node.SetAttribute(name, values);
                xmlDoc.Save(xmlPath);
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        /// <summary>
        /// 创建XML文件
        /// </summary>
        /// <param name="xmlPath">文件路径\文件名称</param>
        /// <param name="rootNodeName">根节点名称</param>
        public void CreateXmlFile(string xmlPath,string rootNodeName)
        {
            XmlDocument xmlDoc = new XmlDocument();
            //创建类型声明节点  
            XmlNode node = xmlDoc.CreateXmlDeclaration("1.0", "utf-8", "");
            xmlDoc.AppendChild(node);
            //创建根节点  
            XmlNode root = xmlDoc.CreateElement(rootNodeName);
            xmlDoc.AppendChild(root);
            try
            {
                xmlDoc.Save(xmlPath);
            }
            catch (Exception e)
            {
                //显示错误信息  
                Console.WriteLine(e.Message);
            }
            //Console.ReadLine();  

        }
        /// <summary>    
        /// 创建节点    
        /// </summary>    
        /// <param name="xmldoc">xml文档</param>
        /// <param name="parentnode">父节点</param>
        /// <param name="name">节点名</param>
        /// <param name="value">节点值</param>
        ///   
        public void CreateNode( XmlDocument xmlDoc, string parentNode, string name, string value)
        {
            var root = xmlDoc.DocumentElement;//取到根结点
            XmlNode xn = root.SelectSingleNode(parentNode);
            if (xn == null)
            {
                xn = xmlDoc.CreateElement(parentNode);
                root.AppendChild(xn);
            }
            XmlNode xn_1 = xn.SelectSingleNode(name);
            if (xn_1 == null)
            {
                XmlNode node = xmlDoc.CreateNode(XmlNodeType.Element, name, null);//创建节点名称
                node.InnerText = value;//添加属性值
                xn.AppendChild(node);
            }
            else
            {
                xn_1.InnerText = value;
            }
            
           
        }
        /// <summary>
        /// 查询配置文件返回datatable
        /// </summary>
        /// <param name="xmlPath">文件路径</param>
        /// <param name="SelectStr">查询条件</param>
        /// <returns></returns>
        public DataTable selectcoordinate(string xmlPath,string SelectStr)
        {
            DataTable dt = new DataTable();
            DataSet ds = new DataSet();
            DataView dv = new DataView();
            XmlDocument doc = new XmlDocument();
            try
            {
                if (File.Exists(xmlPath))
                {

                    ds.ReadXml(xmlPath);
                    dv = ds.Tables[0].DefaultView;
                    dv.RowFilter = SelectStr;
                    dt = dv.ToTable();
                }
                else
                {
                    throw new Exception("错误：配置文集不存在！");
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return dt;
        }
       /// <summary>
       /// 查询配置文件返回dataset
       /// </summary>
       /// <param name="xmlPath">文件路径</param>
       /// <returns></returns>
        public DataSet selectcoordinate(string xmlPath)
        {
            DataSet ds = new DataSet(); 
            try
            {
                if (File.Exists(xmlPath))
                {

                    ds.ReadXml(xmlPath);
                }
                else
                {
                    throw new Exception("错误：配置文集不存在！");
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return ds;
        }
        /// <summary>
        /// 将dataset写入xml文件
        /// </summary>
        /// <param name="xmlPath">文件路径</param>
        /// <param name="ds">写入类容</param>
        /// <returns></returns>
        public bool writeXML(string xmlPath,DataSet ds)
        {
            //DataSet Rds = new DataSet();
            bool re = false;
            try
            {
                if (File.Exists(xmlPath))
                {

                    ds.WriteXml(xmlPath);
                    //Rds.ReadXml(xmlPath);
                    re = true;
                }
                else
                {
                    throw new Exception("错误：配置文集不存在！");

                }
            }
            catch (Exception ex)
            {
                re = false;
                throw ex;
            }
            return re;
        }
    }
}
