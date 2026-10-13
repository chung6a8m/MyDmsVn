using System;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.Messaging;
using MyDmsVn.Desktop.Application;

namespace MyDmsVn.Desktop.WinForms
{
    internal sealed class GoodsReceiptControlFactory : IGoodsReceiptControlFactory
    {
        private readonly IGoodsReceiptApiClient _receipts;
        private readonly IWarehouseApiClient _warehouses;
        private readonly IEmployeeApiClient _employees;
        private readonly IProductApiClient _products;
        private readonly IMessenger _messenger;
        private readonly IUiDispatcher _dispatcher;
        private readonly IDesktopNotificationService _notifications;
        private readonly IDesktopExceptionReporter _exceptionReporter;
        private readonly IAsyncDelay _delay;

        public GoodsReceiptControlFactory(
            IGoodsReceiptApiClient receipts,
            IWarehouseApiClient warehouses,
            IEmployeeApiClient employees,
            IProductApiClient products,
            IMessenger messenger,
            IUiDispatcher dispatcher,
            IDesktopNotificationService notifications,
            IDesktopExceptionReporter exceptionReporter,
            IAsyncDelay delay)
        {
            _receipts = receipts ?? throw new ArgumentNullException(nameof(receipts));
            _warehouses = warehouses ?? throw new ArgumentNullException(nameof(warehouses));
            _employees = employees ?? throw new ArgumentNullException(nameof(employees));
            _products = products ?? throw new ArgumentNullException(nameof(products));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            _exceptionReporter = exceptionReporter ??
                throw new ArgumentNullException(nameof(exceptionReporter));
            _delay = delay ?? throw new ArgumentNullException(nameof(delay));
        }

        public Control Create()
        {
            var list = new GoodsReceiptListViewModel(
                _receipts,
                _dispatcher,
                _notifications,
                _delay,
                exceptionReporter: _exceptionReporter);
            var editor = new GoodsReceiptEditorViewModel(
                _receipts,
                new CatalogLookupViewModel(
                    new WarehouseCatalogLookupSource(_warehouses),
                    _messenger,
                    _dispatcher,
                    _notifications,
                    _delay,
                    exceptionReporter: _exceptionReporter),
                new CatalogLookupViewModel(
                    new EmployeeCatalogLookupSource(_employees),
                    _messenger,
                    _dispatcher,
                    _notifications,
                    _delay,
                    exceptionReporter: _exceptionReporter),
                new GoodsReceiptProductOptionsViewModel(
                    new ProductCatalogLookupSource(_products),
                    _messenger,
                    _dispatcher,
                    _notifications,
                    _delay,
                    exceptionReporter: _exceptionReporter),
                _dispatcher,
                _notifications,
                _exceptionReporter);
            return new GoodsReceiptControl(list, editor);
        }
    }
}
