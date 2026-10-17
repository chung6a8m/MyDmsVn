using System;
using System.Threading;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.Application.Security;
using MyDmsVn.Desktop.WinForms.Features.Security;

namespace MyDmsVn.Desktop.WinForms
{
    public static class DesktopServiceCollectionExtensions
    {
        public static IServiceCollection AddMyDmsVnDesktopWinForms(
            this IServiceCollection services,
            SynchronizationContext uiContext)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (uiContext == null)
            {
                throw new ArgumentNullException(nameof(uiContext));
            }

            services.RemoveAll<IUiDispatcher>();
            services.AddSingleton<IUiDispatcher>(
                new SynchronizationContextUiDispatcher(uiContext));
            services.AddTransient<FoundationViewModel>();
            services.AddTransient<LoginViewModel>();
            services.AddTransient<LoginForm>();
            services.AddTransient<ICatalogControlFactory, CatalogControlFactory>();
            services.AddTransient<IGoodsReceiptControlFactory, GoodsReceiptControlFactory>();
            services.AddTransient<IInventoryControlFactory, InventoryControlFactory>();
            services.AddTransient<ISecurityAdministrationControlFactory, SecurityAdministrationControlFactory>();
            services.AddTransient<FoundationShellForm>();
            return services;
        }
    }

    internal sealed class SecurityAdministrationControlFactory : ISecurityAdministrationControlFactory
    {
        private readonly ISecurityAdministrationApiClient _administration;
        private readonly IPermissionCatalogApiClient _catalog;
        private readonly IDesktopSession _session;
        private readonly IDesktopNotificationService _notifications;
        private readonly IDesktopExceptionReporter? _exceptionReporter;

        public SecurityAdministrationControlFactory(
            ISecurityAdministrationApiClient administration,
            IPermissionCatalogApiClient catalog,
            IDesktopSession session,
            IDesktopNotificationService notifications,
            IDesktopExceptionReporter? exceptionReporter = null)
        {
            _administration = administration;
            _catalog = catalog;
            _session = session;
            _notifications = notifications;
            _exceptionReporter = exceptionReporter;
        }

        public Control Create() => new SecurityAdministrationControl(
            new SecurityAdministrationViewModel(
                _administration,
                _catalog,
                _session,
                _notifications,
                _exceptionReporter));
    }

    internal sealed class CatalogControlFactory : ICatalogControlFactory
    {
        private readonly IProductApiClient _products;
        private readonly IWarehouseApiClient _warehouses;
        private readonly IEmployeeApiClient _employees;
        private readonly ICustomerApiClient _customers;
        private readonly IMessenger _messenger;
        private readonly IUiDispatcher _dispatcher;
        private readonly IDesktopNotificationService _notifications;
        private readonly IDesktopExceptionReporter _exceptionReporter;
        private readonly IAsyncDelay _delay;

        public CatalogControlFactory(
            IProductApiClient products,
            IWarehouseApiClient warehouses,
            IEmployeeApiClient employees,
            ICustomerApiClient customers,
            IMessenger messenger,
            IUiDispatcher dispatcher,
            IDesktopNotificationService notifications,
            IDesktopExceptionReporter exceptionReporter,
            IAsyncDelay delay)
        {
            _products = products;
            _warehouses = warehouses;
            _employees = employees;
            _customers = customers;
            _messenger = messenger;
            _dispatcher = dispatcher;
            _notifications = notifications;
            _exceptionReporter = exceptionReporter;
            _delay = delay;
        }

        public Control Create(CatalogKind kind)
        {
            switch (kind)
            {
                case CatalogKind.Product:
                    return new CatalogControl(
                        new ProductCatalogViewModel(
                            _products,
                            _messenger,
                            _dispatcher,
                            _notifications,
                            _delay,
                            exceptionReporter: _exceptionReporter));
                case CatalogKind.Warehouse:
                    return new CatalogControl(
                        new WarehouseCatalogViewModel(
                            _warehouses,
                            _messenger,
                            _dispatcher,
                            _notifications,
                            _delay,
                            exceptionReporter: _exceptionReporter));
                case CatalogKind.Employee:
                    return new CatalogControl(
                        new EmployeeCatalogViewModel(
                            _employees,
                            _messenger,
                            _dispatcher,
                            _notifications,
                            _delay,
                            exceptionReporter: _exceptionReporter));
                case CatalogKind.Customer:
                    return new CatalogControl(
                        new CustomerCatalogViewModel(
                            _customers,
                            _messenger,
                            _dispatcher,
                            _notifications,
                            _delay,
                            exceptionReporter: _exceptionReporter));
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
    }
}
