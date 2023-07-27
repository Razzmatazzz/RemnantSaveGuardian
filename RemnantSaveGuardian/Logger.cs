using RemnantSaveGuardian.Views.Windows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Interop;

namespace RemnantSaveGuardian
{
    internal static class Logger
    {
        public static event EventHandler<MessageLoggedEventArgs> MessageLogged;
        private static List<LogMessage> messages = new ();
        public static List<LogMessage> Messages { get { return messages; } }
        static Logger()
        {
            if (Properties.Settings.Default.CreateLogFile)
            {
                CreateLog();
            }
            Properties.Settings.Default.PropertyChanged += Default_PropertyChanged;
        }

        private static void Default_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != "CreateLogFile")
            {
                return;
            }
            if (!Properties.Settings.Default.CreateLogFile)
            {
                return;
            }
            CreateLog();
        }

        private static void CreateLog()
        {
            File.WriteAllText("log.txt", DateTime.Now.ToString() + ": Version " + typeof(MainWindow).Assembly.GetName().Version + "\r\n");
        }

        public static void Log(string message, LogType logType)
        {
            MessageLogged?.Invoke(null, new (message, logType));
            messages.Add(new(message, logType));
            if (Properties.Settings.Default.CreateLogFile)
            {
                StreamWriter writer = File.AppendText("log.txt");
                writer.WriteLine(DateTime.Now.ToString() + ": " + message);
                writer.Close();
            }
        }
        public static void Log(string message)
        {
            Log(message, LogType.Normal);
        }
        public static void Success(string message)
        {
            Log(message, LogType.Success);
        }
        public static void Error(string message)
        {
            Log(message, LogType.Error);
        }
        public static void Warn(string message)
        {
            Log(message, LogType.Warning);
        }
    }

    public enum LogType
    {
        Normal,
        Success,
        Error,
        Warning,
    }

    public class MessageLoggedEventArgs : EventArgs
    {
        public string Message { get; set; }
        public LogType LogType { get; set;}
        public MessageLoggedEventArgs(string message, LogType logType)
        {
            Message = message;
            LogType = logType;
        }
        public MessageLoggedEventArgs(string message)
        {
            Message = message;
            LogType = LogType.Normal;
        }
    }
    public class LogMessage
    {
        public string Message { get; set; }
        public LogType LogType { get; set; }
        public LogMessage(string message, LogType logType)
        {
            Message = message;
            LogType = logType;
        }
    }
}
