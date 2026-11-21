using generaControlProcedure;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace MES_SCAF.ClassFile.computer
{
    class JsonToSql
    {
        public class ApiResponse
        {
            public string Prcvno { get; set; }
            public int State { get; set; }
            public string Mess { get; set; }
            public List<BomItem> List { get; set; }
            public string Partno { get; set; }

        }

        public class BomItem
        {
            public string Zjsl { get; set; }  // 注意实际类型可能需要转换为数值类型
            public string Zjno { get; set; }
            public string ljmc { get; set; }
            public string Zjwlbm { get; set; }
            public string Processno { get; set; }
        }
        /// <summary>
        /// 调用API获取数据
        /// </summary>
        public class BomService
        {
            public async Task<ApiResponse> GetBomDataAsync()
            {
                using (var httpClient = new HttpClient())
                {
                    var requestData = new
                    {
                        partno = publicAddress.Cinvaddcode,//图号
                        prcvno = publicAddress.Gylx//工艺路线
                        //processno = publicAddress.Gxbm//工序号编码

                    };

                    var jsonContent = JsonConvert.SerializeObject(requestData);
                    var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

                    var response = await httpClient.PostAsync("http://10.0.0.74:18088/sipmweb/web/searchgxbom", content);
                    response.EnsureSuccessStatusCode();

                    var responseJson = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<ApiResponse>(responseJson);
                }
            }
        }
        /// <summary>
        /// 调用API获取数据1，专门为获取210组件用
        /// </summary>
        public class BomService1
        {
            public async Task<ApiResponse> GetBomDataAsync()
            {
                using (var httpClient = new HttpClient())
                {
                    var requestData = new
                    {
                        partno = publicAddress.Zjno,//图号
                        prcvno = "10"//工艺路线
                        //processno = publicAddress.Gxbm//工序号编码

                    };

                    var jsonContent = JsonConvert.SerializeObject(requestData);
                    var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

                    var response = await httpClient.PostAsync("http://10.0.0.74:18088/sipmweb/web/searchgxbom", content);
                    response.EnsureSuccessStatusCode();

                    var responseJson = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<ApiResponse>(responseJson);
                }
            }
        }
        /// <summary>
        /// 数据库操作
        /// </summary>
        public class BomRepository
        {
            private readonly string _connectionString;

            public BomRepository(string connectionString)
            {
                _connectionString = connectionString;
            }

            public async Task SaveBomDataAsync(ApiResponse data)
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // 使用事务保证删除和插入的原子性
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // 删除旧数据
                            const string deleteSql = @"
                    DELETE FROM PlmT_partBom 
                    WHERE PartNo = @PartNo AND PrcvNo = @PrcvNo AND linenumber = @linenumber";
                            using (var deleteCommand = new SqlCommand(deleteSql, connection, transaction))
                            {
                                deleteCommand.Parameters.AddWithValue("@PartNo", data.Partno);
                                deleteCommand.Parameters.AddWithValue("@PrcvNo", data.Prcvno);
                                deleteCommand.Parameters.AddWithValue("@linenumber", publicAddress.lineNumber);
                                await deleteCommand.ExecuteNonQueryAsync();
                            }

                            // 批量插入新数据（仅在列表有数据时执行）
                            if (data.List?.Count > 0)
                            {
                                // 创建内存表结构
                                var dataTable = new DataTable();
                                dataTable.Columns.Add("PartNo", typeof(string));
                                dataTable.Columns.Add("PrcvNo", typeof(string));
                                dataTable.Columns.Add("ProcessNo", typeof(string));
                                dataTable.Columns.Add("Zjsl", typeof(int));
                                dataTable.Columns.Add("Zjno", typeof(string));
                                dataTable.Columns.Add("ljmc", typeof(string));
                                dataTable.Columns.Add("Zjwlbm", typeof(string));
                                dataTable.Columns.Add("LineNumber", typeof(string));

                                // 填充数据
                                foreach (var item in data.List)
                                {
                                    dataTable.Rows.Add(
                                        data.Partno,
                                        data.Prcvno,
                                        item.Processno,
                                        item.Zjsl,
                                        item.Zjno,
                                        item.ljmc,
                                        item.Zjwlbm,
                                        publicAddress.lineNumber
                                    );
                                }

                                // 使用 SqlBulkCopy 批量插入
                                using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction))
                                {
                                    bulkCopy.DestinationTableName = "PlmT_partBom";
                                    bulkCopy.BatchSize = 1000;  // 可选：调整批量大小

                                    // 列映射（确保与数据库列名一致）
                                    bulkCopy.ColumnMappings.Add("PartNo", "PartNo");
                                    bulkCopy.ColumnMappings.Add("PrcvNo", "PrcvNo");
                                    bulkCopy.ColumnMappings.Add("ProcessNo", "ProcessNo");
                                    bulkCopy.ColumnMappings.Add("Zjsl", "Zjsl");
                                    bulkCopy.ColumnMappings.Add("Zjno", "Zjno");
                                    bulkCopy.ColumnMappings.Add("ljmc", "ljmc");
                                    bulkCopy.ColumnMappings.Add("Zjwlbm", "Zjwlbm");
                                    bulkCopy.ColumnMappings.Add("LineNumber", "LineNumber");

                                    await bulkCopy.WriteToServerAsync(dataTable);
                                }
                            }

                            transaction.Commit();  // 提交事务
                        }
                        catch (Exception )
                        {

                        }
                    }
                }
            }
        }
        /// <summary>
        /// 数据库操作1，专门为获取210组件用
        /// </summary>

        public class BomRepository1
        {
            private readonly string _connectionString;

            public BomRepository1(string connectionString)
            {
                _connectionString = connectionString;
            }

            public async Task SaveBomDataAsync(ApiResponse data)
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // 使用事务保证删除和插入的原子性
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // 删除旧数据
                            const string deleteSql = @"
                    DELETE FROM PlmT_partBom 
                    WHERE PartNo = @PartNo AND PrcvNo = @PrcvNo AND linenumber = @linenumber and processNO='PTGX0005'";
                            using (var deleteCommand = new SqlCommand(deleteSql, connection, transaction))
                            {
                                deleteCommand.Parameters.AddWithValue("@PartNo", publicAddress.Cinvaddcode);
                                deleteCommand.Parameters.AddWithValue("@PrcvNo", publicAddress.Gylx);
                                deleteCommand.Parameters.AddWithValue("@linenumber", publicAddress.lineNumber);
                                await deleteCommand.ExecuteNonQueryAsync();
                            }

                            // 批量插入新数据（仅在列表有数据时执行）
                            if (data.List?.Count > 0)
                            {
                                // 创建内存表结构
                                var dataTable = new DataTable();
                                dataTable.Columns.Add("PartNo", typeof(string));
                                dataTable.Columns.Add("PrcvNo", typeof(string));
                                dataTable.Columns.Add("ProcessNo", typeof(string));
                                dataTable.Columns.Add("Zjsl", typeof(int));
                                dataTable.Columns.Add("Zjno", typeof(string));
                                dataTable.Columns.Add("ljmc", typeof(string));
                                dataTable.Columns.Add("Zjwlbm", typeof(string));
                                dataTable.Columns.Add("LineNumber", typeof(string));

                                // 填充数据
                                foreach (var item in data.List)
                                {
                                    dataTable.Rows.Add(
                                        publicAddress.Cinvaddcode,
                                        publicAddress.Gylx,
                                        "PTGX0005",
                                        item.Zjsl,
                                        item.Zjno,
                                        item.ljmc,
                                        item.Zjwlbm,
                                        publicAddress.lineNumber
                                    );
                                }

                                // 使用 SqlBulkCopy 批量插入
                                using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction))
                                {
                                    bulkCopy.DestinationTableName = "PlmT_partBom";
                                    bulkCopy.BatchSize = 1000;  // 可选：调整批量大小

                                    // 列映射（确保与数据库列名一致）
                                    bulkCopy.ColumnMappings.Add("PartNo", "PartNo");
                                    bulkCopy.ColumnMappings.Add("PrcvNo", "PrcvNo");
                                    bulkCopy.ColumnMappings.Add("ProcessNo", "ProcessNo");
                                    bulkCopy.ColumnMappings.Add("Zjsl", "Zjsl");
                                    bulkCopy.ColumnMappings.Add("Zjno", "Zjno");
                                    bulkCopy.ColumnMappings.Add("ljmc", "ljmc");
                                    bulkCopy.ColumnMappings.Add("Zjwlbm", "Zjwlbm");
                                    bulkCopy.ColumnMappings.Add("LineNumber", "LineNumber");

                                    await bulkCopy.WriteToServerAsync(dataTable);
                                }
                            }

                            transaction.Commit();  // 提交事务
                        }
                        catch (Exception )
                        {

                        }
                    }
                }
            }
        }
    }
}
