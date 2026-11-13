using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

internal interface IWeb6CutoverRuntime
{
    Task Switch(Web6CutoverPair pair, bool cached, Guid epoch, Web6CutoverDeadline deadline, CancellationToken token);
    Task VerifyStableAdmission(Web6CutoverPair pair, bool cached, Guid epoch, Web6CutoverDeadline deadline, CancellationToken token);
    Task VerifyOperation(Guid operation, bool committed, Web6CutoverDeadline deadline, CancellationToken token);
    Task VerifyCommittedSetup(Web6CommittedSetup proof, Web6CutoverDeadline deadline, CancellationToken token);
    Task VerifyLifecycle(Web6CutoverDeadline deadline, CancellationToken token);
    Task SetGrant(bool granted, Web6CutoverDeadline deadline, CancellationToken token);
    Task MakeCommittedUnavailable(Guid operation, Web6CutoverDeadline deadline, CancellationToken token);
    Task VerifyZeroMutation(Guid operation, Web6CutoverDeadline deadline, CancellationToken token);
}

internal enum Web6CutoverJournalState { Stable, Transitioning, Uncertain }

// Called only by the serialized owned pipe. Browser markers alone never constitute host evidence.
internal sealed class Web6CutoverSession
{
    private readonly Web6CandidateCutoverPlan _plan;
    private readonly IWeb6CutoverRuntime _runtime;
    private readonly HashSet<string> _observed = new HashSet<string>(StringComparer.Ordinal);
    private readonly object _journalLock = new object();
    private Web6CutoverJournalState _journalState = Web6CutoverJournalState.Uncertain;
    private (Web6CutoverPair Pair, bool Cached, Guid Epoch)? _stableHost;
    private (Web6CutoverPair Pair, bool Cached, Guid Epoch) _transitionTarget;
    private bool _begun;
    private bool _revoked;
    private Guid? _protectedOperation;
    internal Web6CutoverSession(Web6CandidateCutoverPlan plan, IWeb6CutoverRuntime runtime)
    { _plan = plan; _runtime = runtime; }

    internal Web6CutoverJournalState JournalState
    {
        get { lock (_journalLock) return _journalState; }
    }

    private (Web6CutoverPair Pair, bool Cached, Guid Epoch) Current => (Web6CutoverPair.Current, false, _plan.CurrentEpoch);

    internal async Task Begin(Web6CutoverDeadline deadline, CancellationToken token)
    {
        EnterTransition(begin: true);
        try
        {
            await Switch(Current, deadline, token).ConfigureAwait(false);
            await VerifyPostLifecycle(deadline, token).ConfigureAwait(false);
            MarkStable(deadline, token);
        }
        catch
        {
            MarkUncertain();
            throw;
        }
    }

    internal Web6CommittedSetup? CommittedSetup { get; private set; }

    internal async Task<JObject> Handle(Web6CutoverRequest request, Web6CutoverDeadline deadline, CancellationToken token)
    {
        var host = EnterTransition();
        try
        {
            var (marker, epoch) = await Execute(request, host, deadline, token).ConfigureAwait(false);
            await VerifyPostLifecycle(deadline, token).ConfigureAwait(false);
            var reply = new JObject();
            if (marker != null) reply["marker"] = marker;
            if (epoch.HasValue) reply["deploymentEpoch"] = epoch.Value.ToString("D");
            MarkStable(deadline, token);
            return reply;
        }
        catch
        {
            MarkUncertain();
            throw;
        }
    }

    private async Task<(string? Marker, Guid? Epoch)> Execute(Web6CutoverRequest request,
        (Web6CutoverPair Pair, bool Cached, Guid Epoch) host, Web6CutoverDeadline deadline, CancellationToken token)
    {
        deadline.ThrowIfExpired(token);
        var operation = request.Operation;
        Guid Required() => operation.HasValue && operation != Guid.Empty ? operation.Value : throw new InvalidDataException("Cutover operation is required.");
        if (request.Action != "committedUnavailable" && request.Action != "resetCutover")
        {
            if (request.CommittedSetup != null) throw new InvalidDataException("Unexpected committed proof.");
            await _runtime.VerifyLifecycle(deadline, token).ConfigureAwait(false);
            deadline.ThrowIfExpired(token);
        }
        switch (request.Action)
        {
            case "resetCutover":
                if (_revoked) throw new InvalidDataException("Cutover reset requires the original write grant.");
                if (host.Cached || host.Pair == Web6CutoverPair.Old)
                    await Switch(Current, deadline, token).ConfigureAwait(false);
                else
                    await _runtime.VerifyStableAdmission(host.Pair, host.Cached, host.Epoch, deadline, token).ConfigureAwait(false);
                break;
            case "oldClientNewHostOn":
                await Switch((Web6CutoverPair.Current, true, host.Epoch), deadline, token).ConfigureAwait(false);
                return ("old-client-new-host", null);
            case "oldClientNewHostOff":
                if (host.Cached) await Switch(Current, deadline, token).ConfigureAwait(false);
                break;
            case "newClientOldHostOn":
                await Switch((Web6CutoverPair.Old, false, Guid.Empty), deadline, token).ConfigureAwait(false);
                return ("new-client-old-host", null);
            case "newClientOldHostOff":
                if (host.Pair == Web6CutoverPair.Old) await Switch(Current, deadline, token).ConfigureAwait(false);
                break;
            case "sameEpochRestart":
                if (host != Current || _protectedOperation.HasValue) throw new InvalidDataException("Cutover restart order changed.");
                _protectedOperation = Required();
                await _runtime.VerifyOperation(Required(), false, deadline, token).ConfigureAwait(false);
                await Switch(Current, deadline, token).ConfigureAwait(false);
                await _runtime.VerifyOperation(Required(), false, deadline, token).ConfigureAwait(false);
                _observed.Add("restart");
                return (null, host.Epoch);
            case "revokeGrantOn":
                if (_protectedOperation != Required()) throw new InvalidDataException("Cutover operation changed.");
                await _runtime.SetGrant(false, deadline, token).ConfigureAwait(false);
                _revoked = true;
                _observed.Add("revoke");
                break;
            case "revokeGrantOff":
                if (operation.HasValue && _protectedOperation != operation) throw new InvalidDataException("Cutover operation changed.");
                if (_revoked)
                {
                    await _runtime.SetGrant(true, deadline, token).ConfigureAwait(false);
                    _revoked = false;
                    _observed.Add("grant-restored");
                }
                break;
            case "committedUnavailable":
                if (CommittedSetup != null || host != Current || _protectedOperation.HasValue) throw new InvalidDataException("Committed setup order changed.");
                var proof = Web6CommittedSetup.Parse(request.CommittedSetup, Required());
                await _runtime.VerifyCommittedSetup(proof, deadline, token).ConfigureAwait(false);
                deadline.ThrowIfExpired(token);
                await _runtime.MakeCommittedUnavailable(Required(), deadline, token).ConfigureAwait(false);
                CommittedSetup = proof;
                _observed.Add("unavailable");
                return ("committed-unavailable", null);
            case "restoreEpoch":
                if (_protectedOperation != Required() || !_observed.Contains("restart") || host != Current ||
                    _revoked || !_observed.Contains("grant-restored"))
                    throw new InvalidDataException("Cutover restore order or identity changed.");
                await Switch((Web6CutoverPair.Restored, false, _plan.RestoredEpoch), deadline, token).ConfigureAwait(false);
                _observed.Add("restore");
                return (null, _plan.RestoredEpoch);
            case "verifyNoCutoverMutation":
                await _runtime.VerifyZeroMutation(Required(), deadline, token).ConfigureAwait(false);
                _observed.Add(host.Cached ? "old-client" : host.Pair == Web6CutoverPair.Old ? "old-host" :
                    CommittedSetup?.Operation == operation ? "committed" :
                    _protectedOperation == operation && host.Pair == Web6CutoverPair.Restored ? "protected" : "invalid");
                break;
            default: throw new InvalidDataException("Unknown cutover action.");
        }
        return (null, null);
    }

    private (Web6CutoverPair Pair, bool Cached, Guid Epoch) EnterTransition(bool begin = false)
    {
        lock (_journalLock)
        {
            if (begin ? _begun : _journalState != Web6CutoverJournalState.Stable)
                throw new InvalidDataException("Cutover session has no joined, stable admission.");
            _begun = true;
            _journalState = Web6CutoverJournalState.Transitioning;
            _transitionTarget = _stableHost ?? Current;
            return _transitionTarget;
        }
    }

    private async Task Switch((Web6CutoverPair Pair, bool Cached, Guid Epoch) target, Web6CutoverDeadline deadline, CancellationToken token)
    {
        deadline.ThrowIfExpired(token);
        _transitionTarget = target;
        await _runtime.Switch(target.Pair, target.Cached, target.Epoch, deadline, token).ConfigureAwait(false);
        deadline.ThrowIfExpired(token);
    }

    private async Task VerifyPostLifecycle(Web6CutoverDeadline deadline, CancellationToken token)
    {
        deadline.ThrowIfExpired(token);
        await _runtime.VerifyLifecycle(deadline, token).ConfigureAwait(false);
        deadline.ThrowIfExpired(token);
    }

    private void MarkStable(Web6CutoverDeadline deadline, CancellationToken token)
    {
        lock (_journalLock)
        {
            deadline.ThrowIfExpired(token);
            _stableHost = _transitionTarget;
            _journalState = Web6CutoverJournalState.Stable;
        }
    }

    private void MarkUncertain()
    {
        lock (_journalLock)
        {
            _stableHost = null;
            _journalState = Web6CutoverJournalState.Uncertain;
        }
    }

    internal void VerifyComplete()
    {
        lock (_journalLock)
        {
            if (_journalState != Web6CutoverJournalState.Stable || _stableHost != (Web6CutoverPair.Restored, false, _plan.RestoredEpoch) ||
                CommittedSetup == null || _revoked || !_observed.SetEquals(new[] { "old-client", "old-host", "restart", "revoke", "grant-restored", "unavailable", "committed", "restore", "protected" }))
                throw new InvalidDataException("Cutover admissions, transitions and zero-mutation evidence are incomplete.");
        }
    }
}

internal sealed class Web6CutoverCleanupGate
{
    private volatile bool _retainForContainment;

    internal bool RetainForContainment => _retainForContainment;
    internal void RetainAfterDeadline() => _retainForContainment = true;

    internal async Task Stop(Func<Task> stop, Func<bool> joined)
    {
        ThrowIfRetained();
        try { await stop().ConfigureAwait(false); }
        finally { if (!joined()) _retainForContainment = true; }
        ThrowIfRetained();
    }

    internal Task DrainHost(Func<Task> drain)
    {
        ThrowIfRetained();
        return drain();
    }

    internal void ThrowIfRetained()
    {
        if (_retainForContainment) throw new IOException("WEB6 cutover drain-uncertain; allocations remain retained.");
    }
}

internal static class Web6CutoverLifetime
{
    internal static AggregateException Combine(string message, params Exception[] failures)
    {
        var ordered = new List<Exception>();
        void Append(Exception failure)
        {
            // Flatten depth first: AggregateException.Flatten uses breadth first
            // and can move a later drain ahead of a nested browser primary.
            if (failure is AggregateException aggregate)
                foreach (var inner in aggregate.InnerExceptions) Append(inner);
            else ordered.Add(failure);
        }
        foreach (var failure in failures) Append(failure);
        return new AggregateException(message, ordered);
    }

    internal static async Task<T> RunWithStop<T>(Func<Task<T>> run, Func<Task> stop)
    {
        Exception? primary = null;
        T result = default!;
        try { result = await run().ConfigureAwait(false); }
        catch (Exception failure) { primary = failure; }
        try { await stop().ConfigureAwait(false); }
        catch (Exception failure)
        {
            if (primary != null) throw Combine("Cutover browser and controller stop failed.", primary, failure);
            throw;
        }
        if (primary != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primary).Throw();
        return result;
    }

    internal static async Task Run(Func<Task> execute, Func<Task<bool>> drain, IEnumerable<Func<Task>> disposeAllocations,
        bool stopOnCleanupFailure = false)
    {
        Exception? original = null;
        try { await execute().ConfigureAwait(false); }
        catch (Exception failure) { original = failure; }
        var failures = new List<Exception>();
        if (original != null) failures.Add(original);
        var drained = false;
        try
        {
            drained = await drain().ConfigureAwait(false);
            if (!drained) throw new IOException("Cutover drain is uncertain; all allocations remain quarantined.");
        }
        catch (Exception failure) { failures.Add(failure); }
        if (drained && (!stopOnCleanupFailure || original == null))
            foreach (var dispose in disposeAllocations)
                try { await dispose().ConfigureAwait(false); }
                catch (Exception failure) { failures.Add(failure); if (stopOnCleanupFailure) break; }
        if (failures.Count == 1) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw Combine("Cutover run and/or cleanup failed.", failures.ToArray());
    }
}
