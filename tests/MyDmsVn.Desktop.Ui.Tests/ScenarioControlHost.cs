using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.UiLab.Scenarios;
using MyDmsVn.Desktop.WinForms;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

internal sealed class ScenarioControlHost : IDisposable
{
    private readonly SynchronizationContext? _previousContext;
    private readonly WindowsFormsSynchronizationContext _context;
    private readonly CancellationToken _token;
    private bool _disposed;

    internal SalesScenarioData Data { get; }
    internal SalesReceiptControl Control { get; }
    internal Form Form { get; }

    internal ScenarioControlHost(SalesScenarioId id, CancellationToken token, Action<SalesScenarioData>? configure = null)
    {
        _token = token;
        _previousContext = SynchronizationContext.Current;
        Form = new Form { Width = 1200, Height = 800, StartPosition = FormStartPosition.CenterScreen };
        Form.Show();
        _context = new WindowsFormsSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(_context);
        Data = SalesScenarioData.Create(id);
        configure?.Invoke(Data);
        Control = new SalesReceiptControl(Data.SalesApi, Data.Session, new SynchronizationContextUiDispatcher(_context),
            Data.Preview, kind => Data.CreateLookup(kind, new SynchronizationContextUiDispatcher(_context)),
            Data.Permissions, Data.ProductApi) { Dock = DockStyle.Fill };
        Form.Controls.Add(Control);
        Wait(Control.ActivateAsync(token));
        if (Data.InitialSaleId.HasValue) Wait(Control.SelectReceiptAsync(Data.InitialSaleId.Value, token));
        else { Control.NewButton.PerformClick(); Pump(); }
        NativeLookupTypingDriver.Activate(Form);
    }

    internal void Pump()
    {
        _token.ThrowIfCancellationRequested();
        System.Windows.Forms.Application.DoEvents();
    }

    internal void Until(Func<bool> condition, string reason)
    {
        var clock = Stopwatch.StartNew();
        while (!condition() && clock.Elapsed < TimeSpan.FromSeconds(5)) { Pump(); Thread.Yield(); }
        Assert.True(condition(), reason);
        Pump();
    }

    internal void Wait(Task task)
    {
        Until(() => task.IsCompleted, "Scenario operation did not finish within five seconds.");
        task.GetAwaiter().GetResult();
    }

    internal void WaitLast() => Wait(Control.LastOperation);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            Data.CancelPending();
            Control.Dispose();
            Until(() => Data.SalesApi.OutstandingOperations.All(task => task.IsCompleted),
                "Scenario sales operations did not drain before disposal.");
            Form.Dispose();
            Assert.True(Form.IsDisposed);
        }
        finally
        {
            Data.Dispose();
            SynchronizationContext.SetSynchronizationContext(_previousContext);
            _context.Dispose();
        }
    }

    internal static void Run(SalesScenarioId id, Action<ScenarioControlHost, WinFormsTestGuard> action)
        => Run(id, null, action);

    internal static void Run(SalesScenarioId id, Action<SalesScenarioData>? configure,
        Action<ScenarioControlHost, WinFormsTestGuard> action)
    {
        StaTest.Run(token =>
        {
            using var guard = new WinFormsTestGuard();
            ExceptionDispatchInfo? error = null;
            using var loop = new ApplicationContext();
            using var startup = new Control();
            startup.CreateControl();
            startup.BeginInvoke(new Action(() =>
            {
                try
                {
                    using var host = new ScenarioControlHost(id, token, configure);
                    foreach (var lookup in new[] { host.Control.CustomerLookup, host.Control.WarehouseLookup,
                        host.Control.EmployeeLookup, host.Control.ProductLookup, host.Control.CustomerFilterLookup,
                        host.Control.WarehouseFilterLookup, host.Control.EmployeeFilterLookup, host.Control.ProductFilterLookup })
                        guard.Attach(lookup.ResultsGrid);
                    action(host, guard);
                }
                catch (Exception exception) { error = ExceptionDispatchInfo.Capture(exception); }
                finally { loop.ExitThread(); }
            }));
            System.Windows.Forms.Application.Run(loop);
            error?.Throw();
        }, TimeSpan.FromSeconds(35));
    }
}
