using Remnant2SaveAnalyzer.Views.Windows;
using System;
using System.Collections.Generic;
using System.IO;

namespace Remnant2SaveAnalyzer
{
    internal static class Logger
    {
        public static event EventHandler<MessageLoggedEventArgs>? MessageLogged;

        static Logger()
        {
            if (Properties.Settings.Default.CreateLogFile)
            {
                CreateLog();
            }
            Properties.Settings.Default.PropertyChanged += Default_PropertyChanged;
        }

        public static List<LogMessage> Messages { get; } = [];

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
            File.WriteAllText("log.txt", DateTime.Now + ": Version " + typeof(MainWindow).Assembly.GetName().Version + "\r\n");
        }

        public static void Log(object? message, LogType logType, bool silent = false)
        {
            message ??= "null";
            if (!silent)
            {
                MessageLogged?.Invoke(null, new(message.ToString() ?? "", logType));
            }
            Messages.Add(new(message.ToString() ?? "", logType));
            if (Properties.Settings.Default.CreateLogFile)
            {
                StreamWriter writer = File.AppendText("log.txt");
                writer.WriteLine(DateTime.Now + ": " + message);
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

        public static void WarnSilent(string message)
        {
            Log(message, LogType.Warning, true);
        }
    }

    public enum LogType
    {
        Normal,
        Success,
        Error,
        Warning,
    }

    public class MessageLoggedEventArgs(string message, LogType logType) : EventArgs
    {
        public string Message { get; set; } = message;
        public LogType LogType { get; set;} = logType;

        public MessageLoggedEventArgs(string message) : this(message, LogType.Normal)
        {
        }
    }
    public class LogMessage(string message, LogType logType)
    {
        public string Message { get; set; } = message;
        public LogType LogType { get; set; } = logType;
    }
}
