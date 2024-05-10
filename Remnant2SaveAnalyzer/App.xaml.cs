using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Remnant2SaveAnalyzer.Properties;
using Remnant2SaveAnalyzer.Services;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using Wpf.Ui.Mvvm.Contracts;
using Wpf.Ui.Mvvm.Services;

namespace Remnant2SaveAnalyzer
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
            .ConfigureAppConfiguration(c => { c.SetBasePath(AppContext.BaseDirectory); })
            .ConfigureServices((_, services) =>
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

            }).Build();


        /// <summary>
        /// Occurs when the application is loading.
        /// </summary>
        private async void OnStartup(object sender, StartupEventArgs e)
        {
            Logging.Log.InitialiseSerilog();
            CultureInfo culture = CultureInfo.CurrentCulture;
            List<CultureInfo>cultures = EnumerateSupportedCultures();
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

        private static List<CultureInfo> EnumerateSupportedCultures()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var name = assembly.GetName();
            ResourceManager rm = new($"{name.Name}.locales.Strings", assembly);
            ResourceManager rm2 = new($"{name.Name}.locales.GameStrings", assembly);

            List<CultureInfo> result = [CultureInfo.GetCultureInfo("en")];

            foreach (CultureInfo ci in CultureInfo.GetCultures(CultureTypes.AllCultures))
            {
                if (rm.GetResourceSet(ci, true, false) != null || rm2.GetResourceSet(ci, true, false) != null)
                {
                    if (!result.Exists(x => x.Name == ci.Name) && !string.IsNullOrEmpty(ci.Name))
                    {
                        result.Add(ci);
                    }
                }
            }

            return result;
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