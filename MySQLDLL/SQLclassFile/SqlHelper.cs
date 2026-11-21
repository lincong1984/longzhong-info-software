using System;
using System.Collections.Generic; 
using System.Text;

namespace generaControlProcedure
{
    using System;
    using System.Data;
    using System.Data.SqlClient;

    public  class SqlHelper
    {
        public  DataSet ExecuteDataSet(SqlConnection con, SqlCommand cmd)
        {
            DataSet dataSet = new DataSet();
            SqlTransaction trans = null;
            PrepareCommand(cmd, con, ref trans, false, cmd.CommandType, cmd.CommandText, new SqlParameter[0]);
            try
            {
                new SqlDataAdapter(cmd).Fill(dataSet);
            }
            catch (Exception exception)
            {
                throw exception;
            }
            finally
            {
                if ((cmd.Connection != null) && (cmd.Connection.State == ConnectionState.Open))
                {
                    cmd.Connection.Close();
                }
            }
            return dataSet;
        }

        public  DataSet ExecuteDataSet(string connectionString, SqlCommand cmd)
        {
            if ((connectionString == null) || (connectionString.Length == 0))
            {
                throw new ArgumentNullException("connectionString");
            }
            SqlConnection con = new SqlConnection(connectionString);
            return ExecuteDataSet(con, cmd);
        }

        public  DataSet ExecuteDataSet(SqlConnection con, string commandText, CommandType commandType)
        {
            DataSet dataSet = new DataSet();
            SqlCommand cmd = new SqlCommand();
            SqlTransaction trans = null;
            PrepareCommand(cmd, con, ref trans, false, commandType, commandText, new SqlParameter[0]);
            try
            {
                new SqlDataAdapter(cmd).Fill(dataSet);
            }
            catch (Exception exception)
            {
                throw exception;
            }
            finally
            {
                if ((con != null) && (con.State == ConnectionState.Open))
                {
                    con.Close();
                }
            }
            return dataSet;
        }

        public DataSet ExecuteDataSet(string connectionString, string commandText, CommandType commandType)
        {
            if ((connectionString == null) || (connectionString.Length == 0))
            {
                throw new ArgumentNullException("connectionString");
            }
            if ((commandText == null) || (commandText.Length == 0))
            {
                throw new ArgumentNullException("commandText");
            }
            SqlConnection con = new SqlConnection(connectionString);
            return ExecuteDataSet(con, commandText, commandType);
        }

        public DataSet ExecuteDataSet(SqlConnection con, CommandType commandType, string commandText, params SqlParameter[] commandParameters)
        {
            DataSet set2;
            using (con)
            {
                SqlCommand cmd = new SqlCommand();
                PrepareCommand(cmd, con, commandText, commandParameters);
                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataSet dataSet = new DataSet();
                    adapter.Fill(dataSet);
                    set2 = dataSet;
                }
            }
            return set2;
        }

        public DataSet ExecuteDataSet(SqlConnection con, string commandText, CommandType commandType, params SqlParameter[] cmdParms)
        {
            DataSet dataSet = new DataSet();
            SqlCommand cmd = new SqlCommand();
            SqlTransaction trans = null;
            PrepareCommand(cmd, con, ref trans, false, commandType, commandText, cmdParms);
            try
            {
                new SqlDataAdapter(cmd).Fill(dataSet);
            }
            catch (Exception exception)
            {
                throw exception;
            }
            finally
            {
                if ((con != null) && (con.State == ConnectionState.Open))
                {
                    con.Close();
                }
            }
            return dataSet;
        }

        public  DataSet ExecuteDataSet(string connectionString, CommandType commandType, string commandText, params SqlParameter[] commandParameters)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                return ExecuteDataSet(connection, commandType, commandText, commandParameters);
            }
        }

        public DataSet ExecuteDataSet(string connectionString, string commandText, CommandType commandType, params SqlParameter[] cmdParms)
        {
            if ((connectionString == null) || (connectionString.Length == 0))
            {
                throw new ArgumentNullException("connectionString");
            }
            if ((commandText == null) || (commandText.Length == 0))
            {
                throw new ArgumentNullException("commandText");
            }
            SqlConnection con = new SqlConnection(connectionString);
            return ExecuteDataSet(con, commandText, commandType, cmdParms);
        }
        /// <summary>
        /// 执行SQL语句
        /// </summary>
        /// <param name="con"></param>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public DataTable ExecuteDataTable(SqlConnection con, SqlCommand cmd)
        {
            DataTable dataTable = new DataTable();
            SqlTransaction trans = null;
            PrepareCommand(cmd, con, ref trans, false, cmd.CommandType, cmd.CommandText, new SqlParameter[0]);
            try
            {
                new SqlDataAdapter(cmd).Fill(dataTable);
            }
            catch (Exception exception)
            {
                throw exception;
            }
            finally
            {
                if ((cmd.Connection != null) && (cmd.Connection.State == ConnectionState.Open))
                {
                    cmd.Connection.Close();
                }
            }
            return dataTable;
        }

        public DataTable ExecuteDataTable(SqlConnection con, string commandText, CommandType commandType)
        {
            DataTable dataTable = new DataTable();
            SqlCommand cmd = new SqlCommand();
            SqlTransaction trans = null;
            PrepareCommand(cmd, con, ref trans, false, commandType, commandText, new SqlParameter[0]);
            try
            {
                new SqlDataAdapter(cmd).Fill(dataTable);
            }
            catch (Exception exception)
            {
                throw exception;
            }
            finally
            {
                if ((con != null) && (con.State == ConnectionState.Open))
                {
                    con.Close();
                }
            }
            return dataTable;
        }

        public DataTable ExecuteDataTable(SqlConnection con, string commandText, CommandType commandType, params SqlParameter[] cmdParms)
        {
            DataTable dataTable = new DataTable();
            SqlCommand cmd = new SqlCommand();
            SqlTransaction trans = null;
            PrepareCommand(cmd, con, ref trans, false, commandType, commandText, cmdParms);
            try
            {
                new SqlDataAdapter(cmd).Fill(dataTable);
            }
            catch (Exception exception)
            {
                throw exception;
            }
            finally
            {
                if ((con != null) && (con.State == ConnectionState.Open))
                {
                    con.Close();
                }
            }
            return dataTable;
        }

        public int ExecuteNonQuery(SqlConnection con, SqlCommand cmd)
        {
            int num = 0;
            using (con)
            {
                SqlTransaction trans = null;
                PrepareCommand(cmd, con, ref trans, true, cmd.CommandType, cmd.CommandText, new SqlParameter[0]);
                try
                {
                    num = cmd.ExecuteNonQuery();
                    trans.Commit();
                }
                catch (Exception exception)
                {
                    trans.Rollback();
                    throw exception;
                }
            }
            return num;
        }

        public int ExecuteNonQuery(string connectionString, SqlCommand cmd)
        {
            if ((connectionString == null) || (connectionString.Length == 0))
            {
                throw new ArgumentNullException("connectionString");
            }
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                return ExecuteNonQuery(connection, cmd);
            }
        }

        public int ExecuteNonQuery(SqlConnection con, string spName, params object[] parameterValues)
        {
            using (con)
            {
                SqlCommand cmd = new SqlCommand();
                PrepareCommand(cmd, con, spName, parameterValues);
                return cmd.ExecuteNonQuery();
            }
        }

        public int ExecuteNonQuery(SqlConnection con, string commandText, CommandType commandType)
        {
            int num = 0;
            SqlCommand cmd = new SqlCommand();
            using (con)
            {
                SqlTransaction trans = null;
                PrepareCommand(cmd, con, ref trans, true, commandType, commandText, new SqlParameter[0]);
                try
                {
                    num = cmd.ExecuteNonQuery();
                    trans.Commit();
                }
                catch (Exception exception)
                {
                    trans.Rollback();
                    throw exception;
                }
            }
            return num;
        }

        public int ExecuteNonQuery(string connectionString, string spName, params object[] parameterValues)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                return ExecuteNonQuery(connection, spName, parameterValues);
            }
        }

        public int ExecuteNonQuery(string connectionString, string commandText, CommandType commandType)
        {
            if ((connectionString == null) || (connectionString.Length == 0))
            {
                throw new ArgumentNullException("connectionString");
            }
            if ((commandText == null) || (commandText.Length == 0))
            {
                throw new ArgumentNullException("commandText");
            }
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                return ExecuteNonQuery(connection, commandText, commandType);
            }
        }

        public int ExecuteNonQuery(SqlConnection con, CommandType commandType, string commandText, params SqlParameter[] commandParameters)
        {
            int num = 0;
            SqlCommand cmd = new SqlCommand();
            using (con)
            {
                SqlTransaction trans = null;
                PrepareCommand(cmd, con, ref trans, true, commandType, commandText, new SqlParameter[0]);
                try
                {
                    num = cmd.ExecuteNonQuery();
                    trans.Commit();
                }
                catch (Exception exception)
                {
                    trans.Rollback();
                    throw exception;
                }
            }
            return num;
        }

        public int ExecuteNonQuery(string connectionString, CommandType commandType, string commandText, params SqlParameter[] commandParameters)
        {
            if ((connectionString == null) || (connectionString.Length == 0))
            {
                throw new ArgumentNullException("connectionString");
            }
            if ((commandText == null) || (commandText.Length == 0))
            {
                throw new ArgumentNullException("commandText");
            }
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                return ExecuteNonQuery(connection, commandType, commandText, commandParameters);
            }
        }

        public SqlDataReader ExecuteReader(SqlConnection con, SqlCommand cmd)
        {
            SqlDataReader reader = null;
            SqlTransaction trans = null;
            PrepareCommand(cmd, con, ref trans, false, cmd.CommandType, cmd.CommandText, new SqlParameter[0]);
            try
            {
                reader = cmd.ExecuteReader(CommandBehavior.CloseConnection);
            }
            catch (Exception exception)
            {
                throw exception;
            }
            return reader;
        }

        public SqlDataReader ExecuteReader(string connectionString, SqlCommand cmd)
        {
            if ((connectionString == null) || (connectionString.Length == 0))
            {
                throw new ArgumentNullException("connectionString");
            }
            SqlConnection con = new SqlConnection(connectionString);
            return ExecuteReader(con, cmd);
        }

        public SqlDataReader ExecuteReader(SqlConnection con, string commandText, CommandType commandType)
        {
            SqlDataReader reader = null;
            SqlCommand cmd = new SqlCommand();
            SqlTransaction trans = null;
            PrepareCommand(cmd, con, ref trans, false, commandType, commandText, new SqlParameter[0]);
            try
            {
                reader = cmd.ExecuteReader(CommandBehavior.CloseConnection);
            }
            catch (Exception exception)
            {
                throw exception;
            }
            return reader;
        }

        public SqlDataReader ExecuteReader(SqlConnection con, string spName, params object[] parameterValues)
        {
            SqlDataReader reader2;
            try
            {
                SqlCommand cmd = new SqlCommand();
                PrepareCommand(cmd, con, spName, parameterValues);
                reader2 = cmd.ExecuteReader(CommandBehavior.CloseConnection);
            }
            catch
            {
                con.Close();
                throw;
            }
            return reader2;
        }

        public SqlDataReader ExecuteReader(string connectionString, string spName, params object[] parameterValues)
        {
            return ExecuteReader(connectionString, spName, parameterValues);
        }

        public SqlDataReader ExecuteReader(string connectionString, string commandText, CommandType commandType)
        {
            if ((connectionString == null) || (connectionString.Length == 0))
            {
                throw new ArgumentNullException("connectionString");
            }
            if ((commandText == null) || (commandText.Length == 0))
            {
                throw new ArgumentNullException("commandText");
            }
            SqlConnection con = new SqlConnection(connectionString);
            return ExecuteReader(con, commandText, commandType);
        }

        public SqlDataReader ExecuteReader(SqlConnection con, string commandText, CommandType commandType, params SqlParameter[] cmdParms)
        {
            SqlDataReader reader = null;
            SqlCommand cmd = new SqlCommand();
            SqlTransaction trans = null;
            PrepareCommand(cmd, con, ref trans, false, commandType, commandText, cmdParms);
            try
            {
                reader = cmd.ExecuteReader(CommandBehavior.CloseConnection);
            }
            catch (Exception exception)
            {
                throw exception;
            }
            return reader;
        }

        public SqlDataReader ExecuteReader(string connectionString, string commandText, CommandType commandType, params SqlParameter[] cmdParms)
        {
            if ((connectionString == null) || (connectionString.Length == 0))
            {
                throw new ArgumentNullException("connectionString");
            }
            if ((commandText == null) || (commandText.Length == 0))
            {
                throw new ArgumentNullException("commandText");
            }
            SqlConnection con = new SqlConnection(connectionString);
            return ExecuteReader(con, commandText, commandType);
        }

        public object ExecuteScalar(SqlConnection con, SqlCommand cmd)
        {
            object obj2 = 0;
            using (con)
            {
                SqlTransaction trans = null;
                PrepareCommand(cmd, con, ref trans, true, cmd.CommandType, cmd.CommandText, new SqlParameter[0]);
                try
                {
                    obj2 = cmd.ExecuteScalar();
                    trans.Commit();
                }
                catch (Exception exception)
                {
                    trans.Rollback();
                    throw exception;
                }
            }
            return obj2;
        }

        public object ExecuteScalar(string connectionString, SqlCommand cmd)
        {
            if ((connectionString == null) || (connectionString.Length == 0))
            {
                throw new ArgumentNullException("connectionString");
            }
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                return ExecuteScalar(connection, cmd);
            }
        }

        public object ExecuteScalar(SqlConnection con, string spName, params object[] parameterValues)
        {
            SqlCommand cmd = new SqlCommand();
            using (con)
            {
                PrepareCommand(cmd, con, spName, parameterValues);
                return cmd.ExecuteScalar();
            }
        }

        public object ExecuteScalar(SqlConnection con, string commandText, CommandType commandType)
        {
            object obj2 = 0;
            SqlCommand cmd = new SqlCommand();
            using (con)
            {
                SqlTransaction trans = null;
                PrepareCommand(cmd, con, ref trans, true, commandType, commandText, new SqlParameter[0]);
                try
                {
                    obj2 = cmd.ExecuteScalar();
                    trans.Commit();
                }
                catch (Exception exception)
                {
                    trans.Rollback();
                    throw exception;
                }
            }
            return obj2;
        }

        public object ExecuteScalar(string connectionString, string spName, params object[] parameterValues)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                return ExecuteScalar(connection, spName, parameterValues);
            }
        }

        public object ExecuteScalar(string connectionString, string commandText, CommandType commandType)
        {
            if ((connectionString == null) || (connectionString.Length == 0))
            {
                throw new ArgumentNullException("connectionString");
            }
            if ((commandText == null) || (commandText.Length == 0))
            {
                throw new ArgumentNullException("commandText");
            }
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                return ExecuteScalar(connection, commandText, commandType);
            }
        }

        public object ExecuteScalar(SqlConnection con, string commandText, CommandType commandType, params SqlParameter[] cmdParms)
        {
            object obj2 = 0;
            SqlCommand cmd = new SqlCommand();
            using (con)
            {
                SqlTransaction trans = null;
                PrepareCommand(cmd, con, ref trans, true, commandType, commandText, new SqlParameter[0]);
                try
                {
                    obj2 = cmd.ExecuteScalar();
                    trans.Commit();
                }
                catch (Exception exception)
                {
                    trans.Rollback();
                    throw exception;
                }
            }
            return obj2;
        }

        public object ExecuteScalar(string connectionString, string commandText, CommandType commandType, params SqlParameter[] cmdParms)
        {
            if ((connectionString == null) || (connectionString.Length == 0))
            {
                throw new ArgumentNullException("connectionString");
            }
            if ((commandText == null) || (commandText.Length == 0))
            {
                throw new ArgumentNullException("commandText");
            }
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                return ExecuteScalar(connection, commandText, commandType, cmdParms);
            }
        }

        private void PrepareCommand(SqlCommand cmd, SqlConnection conn, string spName, params object[] parameterValues)
        {
            if (conn.State != ConnectionState.Open)
            {
                conn.Open();
            }
            cmd.Connection = conn;
            cmd.CommandText = spName;
            cmd.CommandType = CommandType.StoredProcedure;
            SqlCommandBuilder.DeriveParameters(cmd);
            cmd.Parameters.RemoveAt(0);
            if (parameterValues != null)
            {
                for (int i = 0; i < cmd.Parameters.Count; i++)
                {
                    cmd.Parameters[i].Value = parameterValues[i];
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="cmd">SQL命令</param>
        /// <param name="conn">SQL连接</param>
        /// <param name="trans"></param>
        /// <param name="useTrans"></param>
        /// <param name="commandType"></param>
        /// <param name="commandText"></param>
        /// <param name="cmdParms"></param>
        private void PrepareCommand(SqlCommand cmd, SqlConnection conn, ref SqlTransaction trans, bool useTrans, CommandType commandType, string commandText, params SqlParameter[] cmdParms)
        {
            if (conn.State != ConnectionState.Open)
            {
                conn.Open();
            }
            cmd.Connection = conn;
            cmd.CommandText = commandText;
            cmd.CommandType = commandType;
            if (useTrans)
            {
                trans = conn.BeginTransaction(IsolationLevel.ReadCommitted);
                cmd.Transaction = trans;
            }
            if (cmdParms != null)
            {
                foreach (SqlParameter parameter in cmdParms)
                {
                    cmd.Parameters.Add(parameter.ParameterName, parameter.Value);
                }
            }
        }
    }
}
