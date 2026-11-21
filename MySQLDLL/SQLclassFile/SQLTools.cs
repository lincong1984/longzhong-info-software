using System;
using System.Data;
using System.Data.SqlClient;
using System.Runtime.InteropServices;
using System.Xml;
using fynClass;
using fyn_XML;

namespace generaControlProcedure
{
    public class SQLTools
    {
        private SqlHelper sqlhelper = new SqlHelper();
        string dataSourceConStr = "";
        SqlConnection sqlcon;
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="id"></param>
        public SQLTools(int id)
        {
            try
            {
                XMLClass xml = new XMLClass();
                DataTable dt = xml.selectcoordinate("id=" + id.ToString(), "SetSQLConnectionString.xml");
                if (dt.Rows.Count > 0)
                {
                    dataSourceConStr = dt.Rows[0][1].ToString();//获取链接字符串
                    sqlcon = new SqlConnection(dataSourceConStr);//链接sql
                }
            }
            catch (Exception ex)
            { }
        }

        /// <summary>
        /// 更新表
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="table"></param>
        /// <returns></returns>
        public Hint updateTable(string tableName, DataTable table)
        {
            Hint hint = new Hint();
            string selectStatement = "SELECT top 0 * FROM " + tableName;
            System.Data.DataTable dt = new System.Data.DataTable();
            //SqlConnection conn = new SqlConnection(dataSourceConStr);
            if ((sqlcon == null) || (sqlcon.State == ConnectionState.Closed))
            {
                sqlcon.Open();
            }
            try
            {
                //conn.Open();
                SqlDataAdapter sqlDa = new SqlDataAdapter();
                sqlDa.SelectCommand = new SqlCommand(selectStatement, sqlcon);
                SqlCommandBuilder cb = new SqlCommandBuilder(sqlDa);
                sqlDa.Fill(dt);
                sqlDa.UpdateCommand = cb.GetUpdateCommand();
                int irst = sqlDa.Update(table);
                hint.Valid = true;
                hint.Msg = "";
            }
            catch (Exception ex)
            {
                hint.Valid = false;
                hint.Msg = "SQL语句执行错误:" + ex.Message;
            }
            if ((sqlcon != null) && (sqlcon.State != ConnectionState.Closed))
            {
                sqlcon.Close();
            }
            return hint;
            //conn.Close();
        }
        /// <summary>
        /// 将数据复制到数据库表
        /// </summary>
        /// <param name="SqlTable"></param>
        /// <param name="dt"></param>
        public void SqlInsertInto(string SqlTable, DataTable dt)
        {
            using (SqlConnection conn = new SqlConnection(dataSourceConStr))
            {
                SqlBulkCopy bulkCopy = new SqlBulkCopy(conn);
                bulkCopy.DestinationTableName = SqlTable;
                bulkCopy.BatchSize = dt.Rows.Count;
                conn.Open();
                if (dt != null && dt.Rows.Count != 0)
                {
                    bulkCopy.WriteToServer(dt);
                }
            }
        }
        /// <summary>
        /// 执行SQL语句，返回dataset
        /// </summary>
        /// <param name="configNode"></param>
        /// <param name="commandText">数据库语句</param>
        /// <param name="autoOpenClose"></param>
        /// <param name="ds">返回表</param>
        /// <returns></returns>
        public Hint GetDataSet(string configNode, string commandText, bool autoOpenClose, out DataSet ds)
        {
            Hint hint = new Hint();
            ds = null;

            if ((sqlcon == null) || (sqlcon.State == ConnectionState.Closed))
            {
                sqlcon.Open();
            }
            try
            {
                SqlCommand cmd = new SqlCommand(commandText, sqlcon);
                ds = sqlhelper.ExecuteDataSet(sqlcon, cmd);
                return hint;
            }
            catch (Exception exception)
            {
                hint.Valid = false;
                hint.Msg = "SQL语句执行错误:" + exception.Message;
                ds = null;
                return hint;
            }
            if ((sqlcon != null) && (sqlcon.State != ConnectionState.Closed))
            {
                sqlcon.Close();
            }
            hint.Valid = true;
            hint.Msg = "";
            return hint;
        }
        /// <summary>
        /// 执行SQL语句，返回datatable
        /// </summary>
        /// <param name="configNode"></param>
        /// <param name="commandText">数据库语句</param>
        /// <param name="autoOpenClose"></param>
        /// <param name="dt">返回表</param>
        /// <returns></returns>
        public Hint GetDataTable(string configNode, string commandText, bool autoOpenClose, out DataTable dt)
        {
            Hint hint = new Hint();
            dt = null;

            if ((sqlcon == null) || (sqlcon.State == ConnectionState.Closed))
            {
                sqlcon.Open();//打开sql
            }
            try
            {
                SqlCommand cmd = new SqlCommand(commandText, sqlcon);
                dt = sqlhelper.ExecuteDataTable(sqlcon, cmd);//执行sql语句
                hint.Valid = true;
                hint.Msg = "";
                return hint;
            }
            catch (Exception exception)
            {
                hint.Valid = false;
                hint.Msg = "SQL语句执行错误:" + exception.Message;
                dt = null;
                return hint;
            }
            if ((sqlcon != null) || (sqlcon.State != ConnectionState.Closed))
            {
                sqlcon.Close();
            }
            hint.Valid = true;
            hint.Msg = "";
            return hint;
        }
        /// <summary>
        /// 执行SQL语句
        /// </summary>
        /// <param name="commandText"></param>
        /// <returns></returns>
        public Hint ExecuteNonQuery( string commandText)
        {
            Hint hint = new Hint();

            if ((sqlcon == null) || (sqlcon.State == ConnectionState.Closed))
            {
                sqlcon.Open();//打开sql
            }
            try
            {
                SqlCommand cmd = new SqlCommand(commandText, sqlcon);
                int rows=cmd.ExecuteNonQuery();
                hint.Valid = true;
                hint.Msg = "";
                return hint;
            }
            catch (Exception exception)
            {
                hint.Valid = false;
                hint.Msg = "SQL语句执行错误:" + exception.Message;
                return hint;
            }
            if ((sqlcon != null) || (sqlcon.State != ConnectionState.Closed))
            {
                sqlcon.Close();
            }
            hint.Valid = true;
            hint.Msg = "";
            return hint;
        }
        /// <summary> 获取配置文件链接sql链接字符串 </summary>
        private static string GetConnectionString()
        {
            try
            {
                string attribute = null;
                WriteReadXML read = new WriteReadXML();
                attribute = read.Read("SetString.xml", "ConnectionString", "config");
                return attribute;//链接sql
            }
            catch (Exception exception)
            {
                //pda.Log.Log.WriteLog("读取连接字符串", ((SqlException)exception).Message);
                return null;
            }
        }
       
    }
}
