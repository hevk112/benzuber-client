using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net;
using System.IO.Compression;

namespace ProjectSummer.Repository
{
    public class Logger
    {
        #region Приватные переменные класса
        private static string _logDir = AppDomain.CurrentDomain.BaseDirectory;

        static Logger loger_sender = new Logger("loggerSender", LogLevels.Debug );
        private string path = (AppDomain.CurrentDomain.BaseDirectory != _logDir) ? _logDir : Path.Combine(_logDir, "log");

        private List<string> logMemory = new List<string>();

        #endregion

        #region Публичные свойства класса
        /// <summary>
        /// Путь до директории для хранения лог файлов
        /// </summary>
        public static string LogDir
        {
            get
            {
                return _logDir;
            }
            set
            {
                _logDir = Path.GetFullPath(value);
            }
        }

        /// <summary>
        /// Дата/время последней отчистки архива
        /// </summary>
        public DateTime LastArchiveClear
        {
            get;
            private set;
        }

        /// <summary>
        /// Текущий уровень логирования
        /// </summary>
        public LogLevels LogLevel
        {
            get;
            set;
        }

        /// <summary>
        /// Включение/отключение записи логов на диск
        /// </summary>
        public bool LogEnable
        {
            get;
            set;
        }

        /// <summary>
        /// Глубина архива логов (дней)
        /// </summary>
        public int ArchiveDepth
        {
            get;
            set;
        }

        /// <summary>
        /// Имя экземпляра логера
        /// </summary>
        public string LoggerName
        {
            get;
            private set;
        }
        #endregion

        #region Структуры класса
        /// <summary>
        /// Строка лога
        /// </summary>
        public struct ParsedLog
        {
            /// <summary>
            /// Дата/время записи
            /// </summary>
            public DateTime DateTime;
            /// <summary>
            /// Текст записи
            /// </summary>
            public string Message;
            /// <summary>
            /// Уровень важности записи
            /// </summary>
            public string Context;
        }
        #endregion

        /// <summary>Конструктор класса логирования</summary>
        /// <param name="ModuleName">Имя экземпляра лога</param>
        /// <param name="LogEnable">Включение/отключение записи лога на диск</param>         
        public Logger(string ModuleName, LogLevels logLevel)
        {
            this.LogEnable = LogEnable;
            this.LogLevel = logLevel;
            this.LastArchiveClear = DateTime.Now.Date.AddDays(-1);
            this.ArchiveDepth = 60;

            if (ModuleName != System.IO.Path.GetFullPath(ModuleName))
            {
                LoggerName = ModuleName;
                path = System.IO.Path.GetFullPath(System.IO.Path.Combine(path, ModuleName));
            }
            else
            {
                path = ModuleName;
            }
            try
            {
                if (!loggers.ContainsKey(ModuleName))
                    loggers.Add(ModuleName, this);
            }
            catch { }
        }

        static Dictionary<string, Logger> loggers = new Dictionary<string, Logger>();

        string fileName = "";

        #region Функции записи информации в лог
        /// <summary>
        /// Запись всех не сохраненых записей из памяти на диск
        /// </summary>
        /// <returns></returns>
        public bool Save()
        {
            try
            {
                if (fileName != GetFileName(DateTime.Today))
                    fileName = GetFileName(DateTime.Today);
                lock (fileName)
                {
                    if (!System.IO.Directory.Exists(path))
                        System.IO.Directory.CreateDirectory(path);


                    using (FileStream fileSteam = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete | FileShare.Read | FileShare.Write))
                    {
                        using (System.IO.StreamWriter str = new StreamWriter(fileSteam, Encoding.Unicode))
                        {
                            lock (logMemory)
                            {
                                foreach (var item in logMemory)
                                    str.WriteLine(item);
                                logMemory.Clear();
                            }
                        }
                        fileSteam.Close();
                    }
                    сlearArhive();
                    return true;
                }
            }
            catch
            {
                return false;
            }

        }

        /// <summary>
        /// Добавление записи в лог, с возможностью форматирования.
        /// Аналог string.Format()
        /// </summary>
        /// <param name="format">Строка составного форматирования</param>
        /// <param name="args">Объекты для форматирования</param>
        /// <returns></returns>
        public void WriteFormated(string format, params object[] args)
        {
            Write(string.Format(format, args));
        }

        /// <summary>
        /// Добавление записи в лог
        /// </summary>
        /// <param name="message">Сообщение для добавления в лог</param>
        /// <param name="messageLevel">Уровень важности сообщения, чем ниже заначение - тем выше важность.</param>
        /// <param name="sendToConsole">Флаг отправки сообщения в отладочную консоль</param>
        /// <param name="forceSave">Флаг форсирования записи лога на диск</param>
        /// <returns></returns>
        private void Write(string message, int messageLevel = 0, bool sendToConsole = true, bool forceSave = true, LogLevels logLevel = LogLevels.Debug)
        {
            var level = logLevel.ToString().PadRight(5);

            message = message.Replace("\r", "\\r").Replace("\n", "\\n");
            var line = $"{DateTime.Now:dd/MM/yy HH:mm:ss.fff}>>[{level}] {message}";
            if (sendToConsole)
                Console.WriteLine(LoggerName + ">>" + line);
            if (LogEnable)
            {
                lock (logMemory)
                    logMemory.Add(line);
                if (forceSave)
                    Save();
            }
            return;
        }

        public void Debug(string message)
        {
            Write(message, logLevel: LogLevels.Debug);
        }
        public void Info(string message)
        {
            Write(message, logLevel: LogLevels.Error);
        }
        public void Error(string message)
        {
            Write(message, logLevel: LogLevels.Error);
        }

        public void Log(string message, LogLevels logLevel)
        {
            Write(message, logLevel: logLevel);
        }

        public enum LogLevels
        {
            Debug, Info, Error
        }
        #endregion


        #region Методы чтения лога с диска
        /// <summary>
        /// Прочитать лог за текущй день
        /// </summary>
        /// <returns>Массив записей лога, за текущий день</returns>
        public ParsedLog[] ParseLog(string Context = null)
        {
            return ParseLog(DateTime.Today, DateTime.Today.AddDays(1).AddMilliseconds(-1), Context);
        }


        public static ParsedLog[] ParseLogs(string Context = null)
        {
            return ParseLogs(DateTime.Today, DateTime.Today.AddDays(1).AddMilliseconds(-1), Context);
        }


        public static ParsedLog[] ParseLogs(DateTime From, DateTime To, string Context = null)
        {
            List<ParsedLog> ret = new List<ParsedLog>();
            foreach (var log in loggers ?? new Dictionary<string, Logger>())
            {
                ret.AddRange(log.Value.ParseLog(From, To, Context));
            }
            return ret.ToArray();
        }
        /// <summary>
        /// Прочитать лог за период.
        /// </summary>
        /// <param name="From">Начало периода</param>
        /// <param name="To">Конец периода</param>
        /// <returns>Массив записей лога, за указаный период</returns>
        public ParsedLog[] ParseLog(DateTime From, DateTime To, string Context = null)
        {
            List<ParsedLog> ret = new List<ParsedLog>();
            try
            {
                for (DateTime z = From; z < To.AddDays(1); z = z.AddDays(1))
                {
                    string tmp = readDay(z);
                    if (tmp != "")
                    {
                        var tmpArr = tmp.Split('\n');
                        for (int q = 0; q < tmpArr.Length; q++)
                        {
                            try
                            {
                                var data = tmpArr[q].Split('>');
                                if (data.Length > 2)
                                {
                                    ParsedLog item = new ParsedLog()
                                    {
                                        DateTime = DateTime.Parse(data[0]),
                                        Context = data[1].Replace("[", "").Replace("]", ""),
                                        Message = string.Join(">", data, 2, data.Length - 2)
                                    };

                                    if ((item.DateTime >= From) && (item.DateTime <= To) && (Context == null || item.Context == Context))
                                    {
                                        ret.Add(item);
                                    }


                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }
            return ret.ToArray();
        }
        #endregion

        #region Вспомогательные методы
        private string GetFileName(DateTime DateAddon)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            string tmpFileName = string.Format("{0}_{1:0000}{2:00}{3:00}.log", LoggerName, DateAddon.Year, DateAddon.Month, DateAddon.Day);
            return Path.Combine(path, tmpFileName);
        }

        private string readDay(DateTime dTime)
        {
            string strret = "";
            try
            {
                if (!System.IO.Directory.Exists(path))
                    System.IO.Directory.CreateDirectory(path);
                string fileName = GetFileName(dTime);
                if (!File.Exists(fileName))
                    return "";
                using (FileStream fileSteam = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    using (System.IO.StreamReader str = new StreamReader(fileSteam, Encoding.Unicode))
                    {
                        strret = str.ReadToEnd();
                        fileSteam.Close();
                    }
                }
                return strret;
            }
            catch
            {
                return strret;
            }

        }
        private void сlearArhive()
        {
            if (LastArchiveClear != DateTime.Now.Date)
            {

                LastArchiveClear = DateTime.Now.Date;

                if (path == "")
                    return;
                var dirInfo = new DirectoryInfo(path);
                foreach (var file in dirInfo.GetFiles())
                {
                    try
                    {
                        if (file.LastWriteTime.Ticks < DateTime.Today.AddDays(-1 * ArchiveDepth).Ticks)
                        {
                            file.Delete();
                        }
                    }
                    catch { }
                }
            }
        }
        #endregion




        public static void ArchLogs(DateTime Date)
        {
            string dirpath = _logDir;

            DirectoryInfo di = new DirectoryInfo(dirpath);

            // Compress the directory's files.
            foreach (FileInfo fi in di.GetFiles($"*{Date.ToString("yyyyMMdd")}.log", SearchOption.AllDirectories))
            {
                Compress(fi, dirpath + "arch\\");
            }
        }

        public static void SendLogs(DateTime Date, bool DeleteAfterSend)
        {
            string dirpath = @"f:\logs\";

            DirectoryInfo di = new DirectoryInfo(dirpath);

            // Compress the directory's files.
            foreach (FileInfo fi in di.GetFiles($"*{Date.ToString("yyyyMMdd")}.log.gz", SearchOption.AllDirectories))
            {

                if (PutFileFTP("192.168.0.244", "hevk", "md8yir1cD1", "/Log", fi) && DeleteAfterSend)
                    fi.Delete();
            }
        }


        public static bool PutFileFTP(string server, string username, string password, string path, FileInfo fi)
        {
            try
            {
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create("ftp://" + server + path + "/" + fi.Name);

                request.Credentials = new NetworkCredential(username, password);
                request.UsePassive = true;
                request.Method = WebRequestMethods.Ftp.UploadFile;
                //request.EnableSsl = true; // если используется ssl

                byte[] file_to_bytes = new byte[fi.Length];
                using (FileStream uploadedFile = fi.OpenRead())
                    uploadedFile.Read(file_to_bytes, 0, file_to_bytes.Length);

                if (file_to_bytes?.Length > 0)
                {
                    using (Stream writer = request.GetRequestStream())
                        writer.Write(file_to_bytes, 0, file_to_bytes.Length);
                    return true;
                }
                else
                    request.Abort();
            }
            catch
            {

            }
            return false;

        }

        public static bool Compress(FileInfo fi, string pathOut)
        {
            try
            {
                if (!Directory.Exists(pathOut)) Directory.CreateDirectory(pathOut);
                string file = Path.Combine(pathOut, fi.Name/*+(timeaddon?DateTime.Now.ToString("HHmmss"):"")*/ + ".gz");
                if (File.Exists(file))
                    File.Delete(file);
                // Get the stream of the source file.
                using (FileStream inFile = fi.OpenRead())
                {
                    // Prevent compressing hidden and 
                    // already compressed files.
                    if ((File.GetAttributes(fi.FullName)
                        & FileAttributes.Hidden)
                        != FileAttributes.Hidden & fi.Extension != ".gz")
                    {
                        // Create the compressed file.

                        using (FileStream outFile =
                                    File.Create(file))
                        {
                            using (GZipStream Compress =
                                new GZipStream(outFile,
                                CompressionMode.Compress))
                            {
                                // Copy the source file into 
                                // the compression stream.
                                inFile.CopyTo(Compress);

                                Console.WriteLine("Compressed {0} from {1} to {2} bytes.",
                                    fi.Name, fi.Length.ToString(), outFile.Length.ToString());
                            }
                        }
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
