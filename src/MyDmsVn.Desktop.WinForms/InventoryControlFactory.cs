using System;
using System.Windows.Forms;
using MyDmsVn.Desktop.Application;

namespace MyDmsVn.Desktop.WinForms
{
    public interface IInventoryControlFactory
    {
        Control CreateStockBalances();
        Control CreateStockCard();
    }

    internal sealed class InventoryControlFactory : IInventoryControlFactory
    {
        private readonly IInventoryApiClient _inventory;
        private readonly IUiDispatcher _dispatcher;
        private readonly IDesktopNotificationService _notifications;
        private readonly IAsyncDelay _delay;

        public InventoryControlFactory(
            IInventoryApiClient inventory,
            IUiDispatcher dispatcher,
            IDesktopNotificationService notifications,
            IAsyncDelay delay)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            _delay = delay ?? throw new ArgumentNullException(nameof(delay));
        }

        public Control CreateStockBalances() =>
            new StockBalanceControl(
                new StockBalanceViewModel(
                    _inventory,
                    _dispatcher,
                    _notifications,
                    _delay));

        public Control CreateStockCard() =>
            new StockCardControl(
                new StockCardViewModel(
                    _inventory,
                    _dispatcher,
                    _notifications));
    }
}
