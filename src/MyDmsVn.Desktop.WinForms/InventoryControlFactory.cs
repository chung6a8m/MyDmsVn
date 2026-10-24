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
        private readonly IDesktopExceptionReporter _exceptionReporter;
        private readonly IAsyncDelay _delay;
        private readonly IDesktopSession? _session;

        public InventoryControlFactory(
            IInventoryApiClient inventory,
            IUiDispatcher dispatcher,
            IDesktopNotificationService notifications,
            IDesktopExceptionReporter exceptionReporter,
            IAsyncDelay delay,
            IDesktopSession? session = null)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            if (notifications == null) throw new ArgumentNullException(nameof(notifications));
            _exceptionReporter = exceptionReporter ??
                throw new ArgumentNullException(nameof(exceptionReporter));
            _delay = delay ?? throw new ArgumentNullException(nameof(delay));
            _session = session;
        }

        public Control CreateStockBalances()
        {
            var feedback = CreateFeedback();
            return new StockBalanceControl(
                new StockBalanceViewModel(
                    _inventory, _dispatcher, feedback, _delay, exceptionReporter: _exceptionReporter), feedback);
        }

        public Control CreateStockCard()
        {
            var feedback = CreateFeedback();
            return new StockCardControl(
                new StockCardViewModel(_inventory, _dispatcher, feedback, _exceptionReporter), feedback);
        }

        private WorkspaceFeedbackNotificationService CreateFeedback() =>
            new WorkspaceFeedbackNotificationService(new WorkspaceFeedbackScope(
                _session?.Version ?? 0, _session == null ? null : () => _session.Version));
    }
}
