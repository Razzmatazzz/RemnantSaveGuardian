//using System.Drawing;
using System;
using System.Diagnostics;
using System.Windows.Media;
using Wpf.Ui.Common.Interfaces;
using Wpf.Ui.Controls;

namespace RemnantSaveGuardian.Views.Pages
{
    /// <summary>
    /// Interaction logic for LogView.xaml
    /// </summary>
    public partial class LogPage : INavigableView<ViewModels.LogViewModel>
    {
        public ViewModels.LogViewModel ViewModel
        {
            get;
        }

        public LogPage(ViewModels.LogViewModel viewModel)
        {
            ViewModel = viewModel;

            InitializeComponent();
            Logger.MessageLogged += Logger_MessageLogged;
            for (var i = Logger.Messages.Count - 1; i >= 0; i--)
            {
                var logMessage = Logger.Messages[i];
                addMessage(logMessage.Message, logMessage.LogType);
            }
        }

        private void Logger_MessageLogged(object? sender, MessageLoggedEventArgs e)
        {
            addMessage(e.Message, e.LogType);
        }

        private void addMessage(string message, LogType logType)
        {
            Dispatcher.Invoke((Action)delegate {
                var infoBar = new InfoBar()
                {
                    Message = message,
                    IsOpen = true,
                    Title = DateTime.Now.ToString(),

                };
                if (logType == LogType.Error)
                {
                    infoBar.Severity = InfoBarSeverity.Error;
                }
                if (logType == LogType.Warning)
                {
                    infoBar.Severity = InfoBarSeverity.Warning;
                }
                if (logType == LogType.Success)
                {
                    infoBar.Severity = InfoBarSeverity.Success;
                }
                stackLogs.Children.Insert(0, infoBar);
            });
        }
    }
}
