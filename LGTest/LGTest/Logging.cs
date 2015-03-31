using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Collections.Concurrent;
using System.IO;

using FreshAddress.Files;

namespace FreshAddress.Logging
{

    public class LogWriter
    {
        private static ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();
        private static string logDir = "";
        private static string logName = "";
        private Timer logUpdater;

        public LogWriter(string dir, string file)
        {
            setDirectory(dir);
            logName = file;
            logUpdater = new Timer(TimeSpan.FromSeconds(2).TotalMilliseconds);
            logUpdater.Elapsed += logUpdater_Elapsed;
            logUpdater.Enabled = true;
        }

        void logUpdater_Elapsed(object sender, ElapsedEventArgs e)
        {
            updateLog();
        }

        public void setDirectory(string dir)
        {
            logDir = dir;
            if (!logDir.EndsWith("\\"))
            {
                logDir = logDir + "\\";
            }
        }

        public void Log(string message)
        {
            messageQueue.Enqueue(message + "\t" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        }

        public static void updateLog()
        {
            if (messageQueue.Count == 0)
            {
                return;
            }
            else
            {
                FileInfo fi = new FileInfo(logDir + logName);
                try
                {
                    if (FileTools.IsFileLocked(fi))
                    {
                        return;
                    }
                    else
                    {
                        using (var sw = new StreamWriter(logDir + logName + "_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt", true))
                        {
                            do
                            {
                                var msg = "";
                                if (!messageQueue.TryDequeue(out msg))
                                {
                                    break;
                                }
                                sw.WriteLine(msg);
                            }
                            while (messageQueue.Count > 0);
                        }
                    }
                }
                catch { }
                
            }
        }
    }

    public class Logger
    {

        public string applicationName;
        public string applicationDirectory;

        public Logger(string appName, string appDir)
        {
            init(appName, appDir);
        }
        public Logger(string appName, string appDir, string logMessage)
        {
            init(appName, appDir);
            logToFile(logMessage, "");
        }
        public Logger(string appName, string appDir, string logMessage, string extra)
        {
            init(appName, appDir);
            logToFile(logMessage, extra);
        }

        public void init(string appName, string appDir)
        {
            applicationName = appName;
            applicationDirectory = appDir;
        }

        public void logToFile(string message, string extraInfo)
        {
            int tryCount = 0;
            bool logged = false;
            while (tryCount < 5 && logged == false)
            {
                try
                {
                    string dateTime = DateTime.Now.ToString("yyyyMMdd");
                    string logDir = "logs\\";
                    string logFile = applicationName + "_" + dateTime + "_Log.html";
#if DEBUG
                    logFile = logFile.Replace(applicationName + "_", applicationName + "_TEST_");
#endif
                    StreamWriter output = new StreamWriter(applicationDirectory + logDir + logFile, true);
                    if (extraInfo != "") { extraInfo = " - " + extraInfo; }
                    output.WriteLine("<div><b>" + DateTime.Now.ToString() + "</b>" + extraInfo + "</div>");
                    output.WriteLine("<code>");
                    output.WriteLine(message.Replace(Environment.NewLine, "<br>"));
                    output.WriteLine("</code>");
                    output.WriteLine("<br><br>");
                    output.Close();
                    logged = true;
                }
                catch (Exception error)
                {
                    Console.WriteLine("Could not log to file: " + message);
                    Console.WriteLine(error.Message.ToString());
                    logged = false;
                }
                tryCount++;
            }
        }



    }

    //build time from assembly
    public static class AppData
    {
        //stackoverflow.com/questions/1600962/displaying-the-build-date
        public static DateTime BuildTime()
        {
            string filePath = System.Reflection.Assembly.GetCallingAssembly().Location;
            const int c_PeHeaderOffset = 60;
            const int c_LinkerTimestampOffset = 8;
            byte[] b = new byte[2048];
            System.IO.Stream s = null;

            try
            {
                s = new System.IO.FileStream(filePath, System.IO.FileMode.Open, System.IO.FileAccess.Read);
                s.Read(b, 0, 2048);
            }
            finally
            {
                if (s != null)
                {
                    s.Close();
                }
            }

            int i = System.BitConverter.ToInt32(b, c_PeHeaderOffset);
            int secondsSince1970 = System.BitConverter.ToInt32(b, i + c_LinkerTimestampOffset);
            DateTime dt = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            dt = dt.AddSeconds(secondsSince1970);
            dt = dt.ToLocalTime();
            return dt;
        }
    }
}
