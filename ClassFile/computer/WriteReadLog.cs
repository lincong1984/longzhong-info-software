using generaControlProcedure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MES_SCAF
{
    class WriteReadLog
    {

        /// <summary>
        /// 写入日志（自动添加时间戳，如果原内容没有的话）
        /// </summary>
        public void WriteLog(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            lock (publicAddress.locker)
            {
                try
                {
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string folder = Path.Combine(baseDir, "Log", DateTime.Now.ToString("yyyy-MM"));

                    if (!Directory.Exists(folder))
                        Directory.CreateDirectory(folder);

                    string filePath = Path.Combine(folder, $"log_{DateTime.Now:yyyy-MM-dd}.txt");

                    // 分割成多行处理
                    string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    StringBuilder processedText = new StringBuilder();

                    foreach (string line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            processedText.AppendLine();
                            continue;
                        }

                        // 支持：
                        // - YYYY-MM-DD HH:MM:SS
                        // - YYYY/MM/DD HH:MM:SS
                        // - YYYY-MM-DD HH：MM：SS（中文冒号）
                        // - 行首或任意位置
                        bool hasTimestamp = Regex.IsMatch(line, @"\d{4}[-/]\d{2}[-/]\d{2}\s+\d{2}[:：]\d{2}([:：]\d{2})?");

                        if (hasTimestamp)
                        {
                            // 已有时间戳，直接使用
                            processedText.AppendLine(line);
                        }
                        else
                        {
                            // 没有时间戳，自动添加
                            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                            processedText.AppendLine($"{timestamp}: {line}");
                        }
                    }

                    File.AppendAllText(filePath, processedText.ToString(), Encoding.GetEncoding("gb2312"));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"WriteLog_1 异常: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 删除超过指定天数的日志文件（安全模式：跳过当天文件）
        /// </summary>
        /// <param name="keepDays">保留最近多少天，默认30天</param>
        public void DeleteOldLogs(int keepDays = 30)
        {
            // 与写入共用锁，避免清理时正好有文件在被写入导致冲突
            lock (publicAddress.locker)
            {
                try
                {
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string logRoot = Path.Combine(baseDir, "Log");

                    if (!Directory.Exists(logRoot))
                        return;

                    // 计算截止日期（30天前）
                    DateTime cutoff = DateTime.Now.AddDays(-keepDays);

                    // 遍历所有月份子目录（格式如 2026-06）
                    foreach (string monthFolder in Directory.GetDirectories(logRoot))
                    {
                        // 简单校验文件夹名是否为日期格式，防止误删
                        if (!Path.GetFileName(monthFolder).Contains('-'))
                            continue;

                        // 获取该月下所有日志文件
                        foreach (string filePath in Directory.GetFiles(monthFolder, "log_*.txt"))
                        {
                            try
                            {
                                FileInfo fi = new FileInfo(filePath);

                                // 核心逻辑：如果文件最后修改时间早于30天前，则删除
                                if (fi.LastWriteTime < cutoff)
                                {
                                    // 绝对安全保护：如果文件修改时间是今天，无论怎样都不删（防止跨天边缘情况）
                                    if (fi.LastWriteTime.Date == DateTime.Now.Date)
                                        continue;

                                    fi.Delete();
                                }
                            }
                            catch (Exception ex)
                            {
                                // 单个文件删除失败不影响整体（比如文件被占用），只记录调试信息
                                System.Diagnostics.Debug.WriteLine($"删除旧日志失败 [{filePath}]: {ex.Message}");
                            }
                        }

                        // 可选：如果该月份文件夹下已经没有文件了，顺手删除空文件夹
                        if (Directory.GetFiles(monthFolder).Length == 0 && Directory.GetDirectories(monthFolder).Length == 0)
                        {
                            try { Directory.Delete(monthFolder); } catch { }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"清理日志目录异常: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 读取日志文件，返回倒序内容（最新在前）
        /// </summary>
        /// <param name="maxLines">最大读取行数，默认100行</param>
        /// <returns>倒序的日志行列表</returns>
        public List<string> ReadLogReversed(int maxLines = 100)
        {
            var result = new List<string>();
            string filePath = GetTodayLogFilePath();

            if (!File.Exists(filePath))
                return result;

            try
            {
                // 使用 ReadLines 流式读取，内存友好，不会一次性加载整个文件
                var lines = File.ReadLines(filePath, Encoding.GetEncoding("gb2312"));

                // 倒序取最新的 N 条
                foreach (var line in lines.Reverse().Take(maxLines))
                {
                    result.Add(line);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ReadLogReversed 异常: {ex.Message}");
                result.Add($"{DateTime.Now}: 读取日志失败 - {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 获取今天的日志文件路径
        /// </summary>
        private string GetTodayLogFilePath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string folder = Path.Combine(baseDir, "Log", DateTime.Now.ToString("yyyy-MM"));
            return Path.Combine(folder, $"log_{DateTime.Now:yyyy-MM-dd}.txt");
        }

        /// <summary>
        /// 读取指定日期的日志文件，返回倒序内容
        /// </summary>
        /// <param name="date">指定日期</param>
        /// <param name="maxLines">最大读取行数</param>
        /// <returns>倒序的日志行列表</returns>
        public List<string> ReadLogReversedByDate(DateTime date, int maxLines = 100)
        {
            var result = new List<string>();
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string folder = Path.Combine(baseDir, "Log", date.ToString("yyyy-MM"));
            string filePath = Path.Combine(folder, $"log_{date:yyyy-MM-dd}.txt");

            if (!File.Exists(filePath))
                return result;

            try
            {
                var lines = File.ReadLines(filePath, Encoding.GetEncoding("gb2312"));
                foreach (var line in lines.Reverse().Take(maxLines))
                {
                    result.Add(line);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ReadLogReversedByDate 异常: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 将日志加载到 ListBox 控件（倒序显示，最新在前）
        /// </summary>
        /// <param name="listBox1">目标 ListBox 控件</param>
        /// <param name="maxLines">最大显示行数</param>
        /// <returns>实际加载的行数</returns>
        public int AddTxtToList(System.Windows.Forms.ListBox listBox1, int maxLines = 60)
        {
            lock (publicAddress.locker)
            {
                listBox1.Items.Clear();

                var logs = ReadLogReversed(maxLines);

                foreach (var log in logs)
                {
                    listBox1.Items.Add(log);
                }
            }
            return listBox1.Items.Count;
        }

        /// <summary>
        /// 将日志加载到 ListBox 控件（支持指定日期）
        /// </summary>
        /// <param name="listBox1">目标 ListBox 控件</param>
        /// <param name="date">指定日期</param>
        /// <param name="maxLines">最大显示行数</param>
        /// <returns>实际加载的行数</returns>
        public int AddTxtToListByDate(System.Windows.Forms.ListBox listBox1, DateTime date, int maxLines = 60)
        {
            lock (publicAddress.locker)
            {
                listBox1.Items.Clear();

                var logs = ReadLogReversedByDate(date, maxLines);

                foreach (var log in logs)
                {
                    listBox1.Items.Add(log);
                }
            }
            return listBox1.Items.Count;
        }
    }
}