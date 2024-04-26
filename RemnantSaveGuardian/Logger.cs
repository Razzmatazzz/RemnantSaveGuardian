using RemnantSaveGuardian.Views.Windows;
using System;
using System.Collections.Generic;
using System.IO;

namespace RemnantSaveGuardian
{
    internal static class Logger
    {
        public static event EventHandler<MessageLoggedEventArgs> MessageLogged;
        private static List<LogMessage> _messages = new ();
        public static List<LogMessage> Messages { get { return _messages; } }
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

        public static void Log(object message, LogType logType)
        {
            if (message == null)
            {
                message = "null";
            }
            MessageLogged?.Invoke(null, new (message.ToString(), logType));
            _messages.Add(new(message.ToString(), logType));
            if (Properties.Settings.Default.CreateLogFile)
            {
                StreamWriter writer = File.AppendText("log.txt");
                writer.WriteLine(DateTime.Now.ToString() + ": " + message);
                writer.Close();
            }
            //Debug.WriteLine(message);
        }
        public static void Log(object message)
        {
            Log(message, LogType.Normal);
        }
        public static void Success(object message)
        {
            Log(message, LogType.Success);
        }
        public static void Error(object message)
        {
            Log(message, LogType.Error);
        }
        public static void Warn(object message)
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
