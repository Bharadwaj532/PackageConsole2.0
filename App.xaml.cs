using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using System.Windows;

namespace PackageConsole
{
    /// <summary>
    /// WPF App with Generic Host for DI/Config/Logging
    /// </summary>
    public partial class App : Application
    {
        public static IHost? HostInstance { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            // Build and start the Generic Host
            HostInstance = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((ctx, cfg) =>
                {
                    cfg.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                })
                .ConfigureLogging((ctx, logging) =>
                {
                    logging.ClearProviders();
                    logging.AddConfiguration(ctx.Configuration.GetSection("Logging"));
                    logging.AddDebug();
                    logging.AddConsole();
                    logging.AddNLog(); // Use NLog.config if present
                })
                .Build();

            HostInstance.Start();

            // Global exception handlers to prevent unexpected app shutdown
            this.DispatcherUnhandledException += (s, args) =>
            {
                var logger = NLog.LogManager.GetCurrentClassLogger();
                logger.Error(args.Exception, "DispatcherUnhandledException");
                MessageBox.Show($"Unexpected error: {args.Exception.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true; // keep app alive
            };
            AppDomain.CurrentDomain.UnhandledException += (s, args2) =>
            {
                var ex = args2.ExceptionObject as Exception;
                var logger = NLog.LogManager.GetCurrentClassLogger();
                logger.Fatal(ex, "AppDomain UnhandledException");
                // Cannot mark handled here, but at least log
            };
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, args3) =>
            {
                var logger = NLog.LogManager.GetCurrentClassLogger();
                logger.Error(args3.Exception, "UnobservedTaskException");
                args3.SetObserved();
            };

            var startupLogger = NLog.LogManager.GetCurrentClassLogger();
            startupLogger.Info("Application started");

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            var logger = NLog.LogManager.GetCurrentClassLogger();
            logger.Info("Application exited");

            HostInstance?.Dispose();
            NLog.LogManager.Shutdown();
            base.OnExit(e);
        }
    }
}
