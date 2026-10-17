using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.Infrastructure.Local;
using MyDmsVn.Desktop.WinForms;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Infrastructure;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Server.Infrastructure.Diagnostics;

namespace MyDmsVn.Desktop.App
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            var smokeTest = IsSmokeTest(args);
            string superuserUsername;
            SqlConnectionConfiguration databaseConfiguration;
            try
            {
                superuserUsername = new SuperuserConfigurationLoader().Load();
                databaseConfiguration = new DesktopDatabaseStartupCoordinator()
                    .PrepareAsync(superuserUsername, CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (OperationalConfigurationException exception)
            {
                return ReportStartupFailure(exception.Code, exception.SafeMessage, smokeTest);
            }
            catch (DesktopStartupException exception)
            {
                return ReportStartupFailure(exception.Code, exception.SafeMessage, smokeTest);
            }

            var uiContext = SynchronizationContext.Current ??
                new System.Windows.Forms.WindowsFormsSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(uiContext);
            using (var provider = CreateServiceProvider(
                uiContext,
                databaseConfiguration.ConnectionString,
                superuserUsername))
            {
                return DesktopHostRunner.Run(
                    provider.GetRequiredService<IServiceScopeFactory>(),
                    provider.GetRequiredService<IDesktopSession>(),
                    smokeTest);
            }
        }

        private static bool IsSmokeTest(string[] args)
        {
            return Array.Exists(
                args ?? Array.Empty<string>(),
                argument => string.Equals(argument, "--smoke-test", StringComparison.Ordinal));
        }

        private static ServiceProvider CreateServiceProvider(
            SynchronizationContext uiContext,
            string connectionString,
            string superuserUsername)
        {
            var services = new ServiceCollection();
            services.AddServerApplication(superuserUsername);
            services.AddServerInfrastructure();
            services.AddSqlPersistence(connectionString);
            services.AddLocalDesktopAdapter();
            services.AddDesktopProductionDiagnostics(
                DesktopDiagnosticsOptions.CreateDefault("MyDmsVn.Desktop.App"));
            services.AddMyDmsVnDesktopWinForms(uiContext);
            return services.BuildServiceProvider();
        }

        private static int ReportStartupFailure(
            string code,
            string safeMessage,
            bool smokeTest)
        {
            var message = code + Environment.NewLine + safeMessage;
            if (smokeTest)
            {
                Console.Error.WriteLine(message);
            }
            else
            {
                System.Windows.Forms.MessageBox.Show(
                    message,
                    "MyDmsVn",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
            }

            return 2;
        }
    }
}
