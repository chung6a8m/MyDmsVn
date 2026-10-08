using System;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.Infrastructure.Local;
using MyDmsVn.Desktop.WinForms;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Infrastructure;

namespace MyDmsVn.Desktop.App
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            using (var provider = CreateServiceProvider())
            {
                System.Windows.Forms.Application.Run(provider.GetRequiredService<FoundationShellForm>());
            }
        }

        private static ServiceProvider CreateServiceProvider()
        {
            var services = new ServiceCollection();
            services.AddServerApplication();
            services.AddServerInfrastructure();
            services.AddLocalDesktopAdapter();
            services.AddTransient<FoundationViewModel>();
            services.AddTransient<FoundationShellForm>();
            return services.BuildServiceProvider();
        }
    }
}
