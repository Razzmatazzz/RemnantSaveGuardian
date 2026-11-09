using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using RemnantSaveGuardian.Models;
using RemnantSaveGuardian.Properties;
using RemnantSaveGuardian.Services;

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

using MudBlazor.Services;

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
        private static readonly IHost _host = Host
            .CreateDefaultBuilder()
            .ConfigureAppConfiguration(c => { c.SetBasePath(AppContext.BaseDirectory); })
            .ConfigureServices((context, services) =>
            {
                // App Host
                services.AddHostedService<ApplicationHostService>();

                // Blazor UI hosted in the main window
                services.AddWpfBlazorWebView();
#if DEBUG
                services.AddBlazorWebViewDeveloperTools();
#endif
                services.AddMudServices(config =>
                {
                    config.SnackbarConfiguration.PositionClass = MudBlazor.Defaults.Classes.Position.BottomRight;
                    config.SnackbarConfiguration.VisibleStateDuration = 10000;
                    config.SnackbarConfiguration.PreventDuplicates = false;
                });
                services.AddSingleton<AppState>();
                services.AddSingleton<BackupService>();

                // Main window
                services.AddSingleton<MainWindow>();

                // Configuration
                services.Configure<AppConfig>(context.Configuration.GetSection(nameof(AppConfig)));
            }).Build();

        /// <summary>
        /// Gets registered service.
        /// </summary>
        /// <typeparam name="T">Type of the service to get.</typeparam>
        /// <returns>Instance of the service or <see langword="null"/>.</returns>
        public static T GetService<T>()
            where T : class
        {
            return (_host.Services.GetService(typeof(T)) as T)!;
        }

        public static IServiceProvider Services => _host.Services;

        /// <summary>
        /// Occurs when the application is loading.
        /// </summary>
        private async void OnStartup(object sender, StartupEventArgs e)
        {
            var culture = CultureInfo.GetCultureInfo(GetUserDefaultUILanguage());
            var cultures = EnumerateSupportedCultures();
            Current.Properties["langs"] = cultures;
            if (!cultures.Contains(culture) && cultures.Contains(culture.Parent))
            {
                culture = culture.Parent;
            }
            if (Settings.Default.Language != "")
            {
                culture = cultures.First(e => e.Name == Settings.Default.Language);
            }

            Thread.CurrentThread.CurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            Loc.Culture = culture;
            await _host.StartAsync();
        }

        [DllImport("Kernel32.dll", CharSet = CharSet.Auto)]
        static extern ushort GetUserDefaultUILanguage();

        private CultureInfo[] EnumerateSupportedCultures()
        {
            CultureInfo[] culture = CultureInfo.GetCultures(CultureTypes.AllCultures);

            string exeLocation = AppContext.BaseDirectory;

            var c = culture.Where(cultureInfo => Directory.Exists(Path.Combine(exeLocation, cultureInfo.Name)) && cultureInfo.Name != "")
                .Prepend(CultureInfo.GetCultureInfo("en"))
                .ToArray();

            return c;
        }

        /// <summary>
        /// Occurs when the application is closing.
        /// </summary>
        private async void OnExit(object sender, ExitEventArgs e)
        {
            await _host.StopAsync();

            _host.Dispose();
        }

        /// <summary>
        /// Occurs when an exception is thrown by an application but not handled.
        /// </summary>
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Logger.Error($"Unhandled error: {e.Exception}");
            e.Handled = true;
        }
    }
}