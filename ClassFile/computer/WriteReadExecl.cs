using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Data.OleDb;
using System.Data;
using System.Collections;

namespace generaControlProcedure
{
    class WriteReadExecl
    {
        /// <summary>
        /// 读取execl文件数据
        /// </summary>
        /// <param name="dt"></param>
        public static void readexecl(out DataTable dt)
        {
            dt = null;
            OpenFileDialog ofd = new OpenFileDialog();//首先根据打开文件对话框，选择excel表格
            ofd.Filter = "表格|*.xlsx;*.xls;*.csv";//打开文件对话框筛选器
            string strPath;//文件完整的路径名
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    string strCon = null;
                    string tableName = null;
                    strPath = ofd.FileName;
                    string aaa = strPath.Split('.')[1];
                    switch (aaa)
                    {
                        case "xlsx":
                            strCon = "Provider = Microsoft.Ace.OleDb.12.0; " + "Data Source = " + strPath + "; " + "Extended Properties = Excel 8.0";
                            break;
                        case "xls":
                            strCon = "provider=microsoft.jet.oledb.4.0;data source=" + strPath + ";extended properties=excel 8.0";
                            break;
                        case "csv":
                            strCon = "Provider=Microsoft.Ace.OleDb.12.0;Data Source=" + strPath.Remove(strPath.LastIndexOf("\\") + 1) + ";Extended Properties='Text;FMT=Delimited;HDR=YES;'";
                            break;

                    }
                    //string strCon = "provider=microsoft.jet.oledb.4.0;data source=" + strPath + ";extended properties=excel 8.0";//关键是红色区域
                    //string strCon = "Provider = Microsoft.Ace.OleDb.12.0; " + "Data Source = " + strPath + "; " + "Extended Properties = Excel 8.0";
                    OleDbConnection Con = new OleDbConnection(strCon);//建立连接
                    Con.Open();
                    System.Data.DataTable sTable = Con.GetOleDbSchemaTable(System.Data.OleDb.OleDbSchemaGuid.Tables, null);
                    tableName = sTable.Rows[0][2].ToString().Trim();
                    //for (int i = 0; i < sTable.Rows.Count; i++)
                    //                        {
                    //                             schemaTable.Rows[i][2].ToString().TrimStart('\'').Trim('\'', '$');
                    //                         }

                    string strExcel = "select * from [" + tableName + "]";
                    //string strSql = "select * from [Sheet1$]";//表名的写法也应注意不同，对应的excel表为sheet1，在这里要在其后加美元符号$，并用中括号
                    OleDbCommand Cmd = new OleDbCommand(strExcel, Con);//建立要执行的命令
                    OleDbDataAdapter da = new OleDbDataAdapter(Cmd);//建立数据适配器
                    DataSet ds = new DataSet();//新建数据集
                    da.Fill(ds, "shyman");//把数据适配器中的数据读到数据集中的一个表中（此处表名为shyman，可以任取表名）
                                          //指定datagridview1的数据源为数据集ds的第一张表（也就是shyman表），也可以写ds.Table["shyman"]
                    dt = ds.Tables[0];
                }
                catch (Exception ex)
                {
                    dt = null;
                    MessageBox.Show(ex.Message);//捕捉异常
                }
            }
        }
        /// <summary>
        /// 导出execl文件
        /// </summary>
        /// <param name="dt"></param>
        public static void writeexecl(DataGridView dataGridView1)
        {

            if (dataGridView1.Rows.Count <= 0)
            {
                MessageBox.Show("无数据！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return;
            }
            string filePath = "";
            SaveFileDialog s = new SaveFileDialog();
            s.Title = "保存Excel文件";
            s.Filter = "Excel文件(*.xls)|*.xls";
            s.FilterIndex = 1;
            if (s.ShowDialog() == DialogResult.OK)
                filePath = s.FileName;
            else
                return;

            //第一步：将dataGridView转化为dataTable,这样可以过滤掉dataGridView中的隐藏列  

            DataTable tmpDataTable = new DataTable("tmpDataTable");
            DataTable modelTable = new DataTable("ModelTable");
            for (int column = 0; column < dataGridView1.Columns.Count; column++)
            {
                if (dataGridView1.Columns[column].Visible == true)
                {
                    DataColumn tempColumn = new DataColumn(dataGridView1.Columns[column].HeaderText, typeof(string));
                    tmpDataTable.Columns.Add(tempColumn);
                    DataColumn modelColumn = new DataColumn(dataGridView1.Columns[column].Name, typeof(string));
                    modelTable.Columns.Add(modelColumn);
                }
            }
            for (int row = 0; row < dataGridView1.Rows.Count; row++)
            {
                if (dataGridView1.Rows[row].Visible == false)
                    continue;
                DataRow tempRow = tmpDataTable.NewRow();
                for (int i = 0; i < tmpDataTable.Columns.Count; i++)
                    tempRow[i] = dataGridView1.Rows[row].Cells[modelTable.Columns[i].ColumnName].Value;
                tmpDataTable.Rows.Add(tempRow);
            }
            if (tmpDataTable == null)
            {
                return;
            }
            
            //第二步：导出dataTable到Excel  
            long rowNum = tmpDataTable.Rows.Count;//行数  
            int columnNum = tmpDataTable.Columns.Count;//列数  
            Microsoft.Office.Interop.Excel.Application m_xlApp = new Microsoft.Office.Interop.Excel.Application();
            m_xlApp.DisplayAlerts = false;//不显示更改提示  
            m_xlApp.Visible = false;

            Microsoft.Office.Interop.Excel.Workbooks workbooks = m_xlApp.Workbooks;
            Microsoft.Office.Interop.Excel.Workbook workbook = workbooks.Add(Microsoft.Office.Interop.Excel.XlWBATemplate.xlWBATWorksheet);
            Microsoft.Office.Interop.Excel.Worksheet worksheet = (Microsoft.Office.Interop.Excel.Worksheet)workbook.Worksheets[1];//取得sheet1  

            try
            {
                string[,] datas = new string[rowNum + 1, columnNum];
                for (int i = 0; i < columnNum; i++) //写入字段  
                    datas[0, i] = tmpDataTable.Columns[i].Caption;
                //Excel.Range range = worksheet.get_Range(worksheet.Cells[1, 1], worksheet.Cells[1, columnNum]);  
                Microsoft.Office.Interop.Excel.Range range = m_xlApp.Range[worksheet.Cells[1, 1], worksheet.Cells[1, columnNum]];
                range.Interior.ColorIndex = 15;//15代表灰色  
                range.Font.Bold = true;
                range.Font.Size = 10;

                int r = 0;
                for (r = 0; r < rowNum; r++)
                {
                    for (int i = 0; i < columnNum; i++)
                    {
                        object obj = tmpDataTable.Rows[r][tmpDataTable.Columns[i].ToString()];
                        datas[r + 1, i] = obj == null ? "" : obj.ToString().Trim();// "'" + obj.ToString().Trim();//在obj.ToString()前加单引号是为了防止自动转化格式
                    }
                    System.Windows.Forms.Application.DoEvents();
                    //添加进度条  
                }
                //Excel.Range fchR = worksheet.get_Range(worksheet.Cells[1, 1], worksheet.Cells[rowNum + 1, columnNum]);  
                Microsoft.Office.Interop.Excel.Range fchR = m_xlApp.Range[worksheet.Cells[1, 1], worksheet.Cells[rowNum + 1, columnNum]];
                fchR.Value2 = datas;

                worksheet.Columns.EntireColumn.AutoFit();//列宽自适应。  
                                                         //worksheet.Name = "dd";  

                //m_xlApp.WindowState = Excel.XlWindowState.xlMaximized;  
                m_xlApp.Visible = false;

                // = worksheet.get_Range(worksheet.Cells[1, 1], worksheet.Cells[rowNum + 1, columnNum]);  
                range = m_xlApp.Range[worksheet.Cells[1, 1], worksheet.Cells[rowNum + 1, columnNum]];

                //range.Interior.ColorIndex = 15;//15代表灰色  
                range.Font.Size = 9;
                range.RowHeight = 14.25;
                range.Borders.LineStyle = 1;
                range.HorizontalAlignment = 1;
                workbook.Saved = true;
                workbook.SaveCopyAs(filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("导出异常：" + ex.Message, "导出异常", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                EndReport();
            }

            m_xlApp.Workbooks.Close();
            m_xlApp.Workbooks.Application.Quit();
            m_xlApp.Application.Quit();
            m_xlApp.Quit();
            MessageBox.Show("导出成功！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        /// <summary>
        /// 导出execl文件
        /// </summary>
        /// <param name="dt"></param>
        public static void writeexecl(DataTable dt)
        {

            if (dt.Rows.Count <= 0)
            {
                MessageBox.Show("无数据！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return;
            }
            string filePath = "";
            SaveFileDialog s = new SaveFileDialog();
            s.Title = "保存Excel文件";
            s.Filter = "Excel文件(*.xls)|*.xls";
            s.FilterIndex = 1;
            if (s.ShowDialog() == DialogResult.OK)
                filePath = s.FileName;
            else
                return;

            //第一步：将dataGridView转化为dataTable,这样可以过滤掉dataGridView中的隐藏列  

            DataTable tmpDataTable = dt;
            

            //第二步：导出dataTable到Excel  
            long rowNum = tmpDataTable.Rows.Count;//行数  
            int columnNum = tmpDataTable.Columns.Count;//列数  
            Microsoft.Office.Interop.Excel.Application m_xlApp = new Microsoft.Office.Interop.Excel.Application();
            m_xlApp.DisplayAlerts = false;//不显示更改提示  
            m_xlApp.Visible = false;

            Microsoft.Office.Interop.Excel.Workbooks workbooks = m_xlApp.Workbooks;
            Microsoft.Office.Interop.Excel.Workbook workbook = workbooks.Add(Microsoft.Office.Interop.Excel.XlWBATemplate.xlWBATWorksheet);
            Microsoft.Office.Interop.Excel.Worksheet worksheet = (Microsoft.Office.Interop.Excel.Worksheet)workbook.Worksheets[1];//取得sheet1  

            try
            {
                string[,] datas = new string[rowNum + 1, columnNum];
                for (int i = 0; i < columnNum; i++) //写入字段  
                    datas[0, i] = tmpDataTable.Columns[i].Caption;
                //Excel.Range range = worksheet.get_Range(worksheet.Cells[1, 1], worksheet.Cells[1, columnNum]);  
                Microsoft.Office.Interop.Excel.Range range = m_xlApp.Range[worksheet.Cells[1, 1], worksheet.Cells[1, columnNum]];
                range.Interior.ColorIndex = 15;//15代表灰色  
                range.Font.Bold = true;
                range.Font.Size = 10;

                int r = 0;
                for (r = 0; r < rowNum; r++)
                {
                    for (int i = 0; i < columnNum; i++)
                    {
                        object obj = tmpDataTable.Rows[r][tmpDataTable.Columns[i].ToString()];
                        datas[r + 1, i] = obj == null ? "" : obj.ToString().Trim();// "'" + obj.ToString().Trim();//在obj.ToString()前加单引号是为了防止自动转化格式
                    }
                    System.Windows.Forms.Application.DoEvents();
                    //添加进度条  
                }
                //Excel.Range fchR = worksheet.get_Range(worksheet.Cells[1, 1], worksheet.Cells[rowNum + 1, columnNum]);  
                Microsoft.Office.Interop.Excel.Range fchR = m_xlApp.Range[worksheet.Cells[1, 1], worksheet.Cells[rowNum + 1, columnNum]];
                fchR.Value2 = datas;

                worksheet.Columns.EntireColumn.AutoFit();//列宽自适应。  
                                                         //worksheet.Name = "dd";  

                //m_xlApp.WindowState = Excel.XlWindowState.xlMaximized;  
                m_xlApp.Visible = false;

                // = worksheet.get_Range(worksheet.Cells[1, 1], worksheet.Cells[rowNum + 1, columnNum]);  
                range = m_xlApp.Range[worksheet.Cells[1, 1], worksheet.Cells[rowNum + 1, columnNum]];

                //range.Interior.ColorIndex = 15;//15代表灰色  
                range.Font.Size = 9;
                range.RowHeight = 14.25;
                range.Borders.LineStyle = 1;
                range.HorizontalAlignment = 1;
                workbook.Saved = true;
                workbook.SaveCopyAs(filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("导出异常：" + ex.Message, "导出异常", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                EndReport();
            }

            m_xlApp.Workbooks.Close();
            m_xlApp.Workbooks.Application.Quit();
            m_xlApp.Application.Quit();
            m_xlApp.Quit();
            MessageBox.Show("导出成功！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            System.Diagnostics.Process.Start(filePath);
        }
        /// <summary>
        /// 关闭进程清理垃圾
        /// </summary>
        private static void EndReport()
        {
            object missing = System.Reflection.Missing.Value;
            try
            {
                //m_xlApp.Workbooks.Close();  
                //m_xlApp.Workbooks.Application.Quit();  
                //m_xlApp.Application.Quit();  
                //m_xlApp.Quit();  
            }
            catch { }
            finally
            {
                try
                {
                    Microsoft.Office.Interop.Excel.Application m_xlApp = new Microsoft.Office.Interop.Excel.Application();
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(m_xlApp.Workbooks);
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(m_xlApp.Application);
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(m_xlApp);
                    m_xlApp = null;
                }
                catch { }
                try
                {
                    //清理垃圾进程  
                    killProcessThread();
                }
                catch { }
                GC.Collect();
            }
        }
        /// <summary>
        /// 清理垃圾进程
        /// </summary>
        private static void killProcessThread()
        {
            ArrayList myProcess = new ArrayList();
            for (int i = 0; i < myProcess.Count; i++)
            {
                try
                {
                    System.Diagnostics.Process.GetProcessById(int.Parse((string)myProcess[i])).Kill();
                }
                catch { }
            }
        }
    }
}
