using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RemnantSaveGuardian.Models;
using RemnantSaveGuardian.Properties;
using RemnantSaveGuardian.Services;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using Wpf.Ui.Mvvm.Contracts;
using Wpf.Ui.Mvvm.Services;

namespace RemnantSaveGuardian
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        // The.NET Generic Host provides dependency injection, configuration, logging, and other services.
        // https://docs.microsoft.com/dotnet/core/extensions/generic-host
        // https://docs.microsoft.com/dotnet/core/extensions/dependency-injection
        // https://docs.microsoft.com/dotnet/core/extensions/configuration
        // https://docs.microsoft.com/dotnet/core/extensions/logging
        private static readonly IHost Host = Microsoft.Extensions.Hosting.Host
            .CreateDefaultBuilder()
            .ConfigureAppConfiguration(c => { c.SetBasePath(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)); })
            .ConfigureServices((context, services) =>
            {
                // App Host
                services.AddHostedService<ApplicationHostService>();

                // Page resolver service
                services.AddSingleton<IPageService, PageService>();

                // Theme manipulation
                services.AddSingleton<IThemeService, ThemeService>();

                // TaskBar manipulation
                services.AddSingleton<ITaskBarService, TaskBarService>();

                // Service containing navigation, same as INavigationWindow... but without window
                services.AddSingleton<INavigationService, NavigationService>();

                // Main window with navigation
                services.AddScoped<INavigationWindow, Views.Windows.MainWindow>();
                services.AddScoped<ViewModels.MainWindowViewModel>();

                // Views and ViewModels
                services.AddScoped<Views.Pages.BackupsPage>();
                services.AddScoped<ViewModels.BackupsViewModel>();
                services.AddScoped<Views.Pages.LogPage>();
                services.AddScoped<ViewModels.LogViewModel>();
                services.AddScoped<Views.Pages.WorldAnalyzerPage>();
                services.AddScoped<ViewModels.WorldAnalyzerViewModel>();
                services.AddScoped<Views.Pages.SettingsPage>();
                services.AddScoped<ViewModels.SettingsViewModel>();

                // Configuration
                services.Configure<AppConfig>(context.Configuration.GetSection(nameof(AppConfig)));
            }).Build();


        /// <summary>
        /// Occurs when the application is loading.
        /// </summary>
        private async void OnStartup(object sender, StartupEventArgs e)
        {
            var culture = CultureInfo.CurrentCulture;
            var cultures = EnumerateSupportedCultures();
            Current.Properties["langs"] = cultures;
            if (!cultures.Contains(culture) && cultures.Contains(culture.Parent))
            {
                culture = culture.Parent;
            }
            if (Settings.Default.Language != "")
            {
                culture = cultures.First(x => x.Name == Settings.Default.Language);
            }

            Thread.CurrentThread.CurrentCulture = culture;
            WPFLocalizeExtension.Engine.LocalizeDictionary.Instance.Culture = culture;

            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));
            await Host.StartAsync();
        }

        private CultureInfo[] EnumerateSupportedCultures()
        {
            CultureInfo[] culture = CultureInfo.GetCultures(CultureTypes.AllCultures);
            string? exeLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Debug.Assert(exeLocation != null, nameof(exeLocation) + " != null");
            var c = culture.Where(cultureInfo => Directory.Exists(Path.Combine(exeLocation, cultureInfo.Name)) && cultureInfo.Name != "").ToArray();
            return c;
        }

        /// <summary>
        /// Occurs when the application is closing.
        /// </summary>
        private async void OnExit(object sender, ExitEventArgs e)
        {
            await Host.StopAsync();

            Host.Dispose();
        }

        /// <summary>
        /// Occurs when an exception is thrown by an application but not handled.
        /// </summary>
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // For more info see https://docs.microsoft.com/en-us/dotnet/api/system.windows.application.dispatcherunhandledexception?view=windowsdesktop-6.0
        }
    }
}