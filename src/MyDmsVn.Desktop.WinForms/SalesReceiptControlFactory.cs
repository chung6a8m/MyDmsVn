using System;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.Messaging;
using MyDmsVn.Desktop.Application;

namespace MyDmsVn.Desktop.WinForms;

internal sealed class SalesReceiptControlFactory : ISalesReceiptControlFactory
{
    private readonly ISalesReceiptApiClient _sales;
    private readonly IDesktopSession _session;
    private readonly IUiDispatcher _ui;
    private readonly ISalesReceiptPreviewCalculator _calculator;
    private readonly IPermissionApiClient _permissions;
    private readonly ICustomerApiClient _customers;
    private readonly IWarehouseApiClient _warehouses;
    private readonly IEmployeeApiClient _employees;
    private readonly IProductApiClient _products;
    private readonly IMessenger _messenger;
    private readonly IDesktopNotificationService _notifications;
    private readonly IDesktopExceptionReporter _reporter;
    private readonly IAsyncDelay _delay;
    public SalesReceiptControlFactory(ISalesReceiptApiClient sales, IDesktopSession session, IUiDispatcher ui,
        ISalesReceiptPreviewCalculator calculator, IPermissionApiClient permissions, ICustomerApiClient customers,
        IWarehouseApiClient warehouses, IEmployeeApiClient employees, IProductApiClient products, IMessenger messenger,
        IDesktopNotificationService notifications, IDesktopExceptionReporter reporter, IAsyncDelay delay)
    {
        _sales = sales; _session = session; _ui = ui; _calculator = calculator; _permissions = permissions;
        _customers = customers; _warehouses = warehouses; _employees = employees; _products = products;
        _messenger = messenger; _notifications = notifications; _reporter = reporter; _delay = delay;
    }
    public Control Create() => new SalesReceiptControl(_sales, _session, _ui, _calculator, Lookup, _permissions, _products);
    private CatalogLookupViewModel Lookup(CatalogKind kind)
    {
        ICatalogLookupSource source;
        switch (kind)
        {
            case CatalogKind.Customer: source = new CustomerCatalogLookupSource(_customers); break;
            case CatalogKind.Warehouse: source = new WarehouseCatalogLookupSource(_warehouses); break;
            case CatalogKind.Employee: source = new EmployeeCatalogLookupSource(_employees); break;
            case CatalogKind.Product: source = new ProductCatalogLookupSource(_products); break;
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }
        return new CatalogLookupViewModel(source, _messenger, _ui, _notifications, _delay, exceptionReporter: _reporter);
    }
}
