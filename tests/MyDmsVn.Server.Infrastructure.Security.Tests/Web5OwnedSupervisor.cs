using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

// The implementation owns all concrete identities. Quarantine writes a private
// record only; it must not destroy resources or assume a timed-out task drained.
internal interface IWeb5OwnedResources
{
    Task Allocate(CancellationToken token);
    Task Start(CancellationToken token);
    Task<bool> VerifyHost(CancellationToken token);
    Task Admit(CancellationToken token);
    Task StopAdmission(CancellationToken token);
    Task Drain(CancellationToken token);
    Task<bool> VerifyOwnershipAndQuiescence(CancellationToken token);
    Task Oracle(CancellationToken token);
    Task RemoveOwned(CancellationToken token);
    Task<bool> VerifyAbsent(CancellationToken token);
    Task Quarantine(CancellationToken token);
}
internal enum Web5SupervisorState { New, Running, Complete, Quarantined }
internal sealed class Web5OwnedSupervisor
{
    private readonly IWeb5OwnedResources _resources;
    private readonly TimeSpan _budget;
    private int _busy;
    public Web5SupervisorState State { get; private set; } = Web5SupervisorState.New;
    public Web5OwnedSupervisor(IWeb5OwnedResources resources, TimeSpan budget)
    {
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
        if (budget <= TimeSpan.Zero || budget > TimeSpan.FromMinutes(10)) throw new ArgumentOutOfRangeException(nameof(budget));
        _budget = budget;
    }
    public async Task Start()
    {
        Enter(Web5SupervisorState.New);
        try
        {
            await Run(_resources.Allocate); await Run(_resources.Start);
            if (!await Run(_resources.VerifyHost)) throw new InvalidDataException("Owned host identity is uncertain.");
            await Run(_resources.Admit); State = Web5SupervisorState.Running;
        }
        catch (Exception failure) { await Quarantine(failure); throw; }
        finally { Volatile.Write(ref _busy, 0); }
    }
    public async Task Finish()
    {
        Enter(Web5SupervisorState.Running);
        try
        {
            await Run(_resources.StopAdmission); await Run(_resources.Drain);
            if (!await Run(_resources.VerifyOwnershipAndQuiescence)) throw new InvalidDataException("Owned resource identity or drain is uncertain.");
            await Run(_resources.Oracle); await Run(_resources.RemoveOwned);
            if (!await Run(_resources.VerifyAbsent)) throw new InvalidDataException("Owned cleanup was not verified.");
            State = Web5SupervisorState.Complete;
        }
        catch (Exception failure) { await Quarantine(failure); throw; }
        finally { Volatile.Write(ref _busy, 0); }
    }
    private void Enter(Web5SupervisorState expected)
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) throw new InvalidOperationException("A supervisor action is already in progress.");
        if (State != expected) { Volatile.Write(ref _busy, 0); throw new InvalidOperationException("Invalid supervisor state."); }
    }
    private async Task Quarantine(Exception failure)
    {
        State = Web5SupervisorState.Quarantined;
        try { await Run(_resources.Quarantine); }
        catch (Exception recording) { throw new AggregateException("Fixture failed and quarantine record was not confirmed.", failure, recording); }
    }
    private async Task Run(Func<CancellationToken, Task> action) => await Run(async token => { await action(token); return true; });
    private async Task<T> Run<T>(Func<CancellationToken, Task<T>> action)
    {
        using var timeout = new CancellationTokenSource();
        var task = action(timeout.Token);
        var delay = Task.Delay(_budget);
        if (await Task.WhenAny(task, delay) != task)
        {
            timeout.Cancel();
            // No later supervisor stage may run when a worker ignored cancellation.
            // The in-flight action may still complete: quarantine is an uncertain outcome,
            // not a claim that resources remain or have been safely removed.
            _ = task.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            throw new TimeoutException("Fixture action timed out; resource ownership is uncertain.");
        }
        return await task;
    }
}
