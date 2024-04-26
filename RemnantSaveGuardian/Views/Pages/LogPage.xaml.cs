//using System.Drawing;
using System;
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
            foreach (LogMessage logMessage in Logger.Messages)
            {
                AddMessage(logMessage.Message, logMessage.LogType);
            }
        }

        private void Logger_MessageLogged(object? sender, MessageLoggedEventArgs e)
        {
            AddMessage(e.Message, e.LogType);
        }

        private void AddMessage(string message, LogType logType)
        {
            Dispatcher.Invoke(delegate {
                InfoBar infoBar = new()
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
                infoBar.ContextMenu = new System.Windows.Controls.ContextMenu();
                MenuItem menuCopyMessage = new()
                {
                    Header = Loc.T("Copy"),
                    SymbolIcon = Wpf.Ui.Common.SymbolRegular.Copy24
                };
                menuCopyMessage.Click += (_, _) =>
                {
                    System.Windows.Clipboard.SetDataObject(message);
                };
                infoBar.ContextMenu.Items.Add(menuCopyMessage);
                stackLogs.Children.Insert(0, infoBar);
            });
        }
    }
}
