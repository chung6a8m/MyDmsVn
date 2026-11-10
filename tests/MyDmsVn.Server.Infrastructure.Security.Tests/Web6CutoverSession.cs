using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

internal interface IWeb6CutoverRuntime
{
    Task Switch(Web6CutoverPair pair, bool cached, Guid epoch);
    Task VerifyOperation(Guid operation, bool committed);
    Task VerifyCommittedSetup(Web6CommittedSetup proof);
    Task VerifyLifecycle();
    Task SetGrant(bool granted);
    Task MakeCommittedUnavailable(Guid operation);
    Task VerifyZeroMutation(Guid operation);
}

// Called only by the serialized owned pipe. Browser markers alone never constitute host evidence.
internal sealed class Web6CutoverSession
{
    private readonly Web6CandidateCutoverPlan _plan;
    private readonly IWeb6CutoverRuntime _runtime;
    private readonly HashSet<string> _observed = new HashSet<string>(StringComparer.Ordinal);
    private Guid _epoch;
    private bool _cached, _old, _revoked;
    private Guid? _protectedOperation;
    internal Web6CutoverSession(Web6CandidateCutoverPlan plan, IWeb6CutoverRuntime runtime)
    { _plan = plan; _runtime = runtime; _epoch = plan.CurrentEpoch; }
    internal Task Begin() => _runtime.Switch(Web6CutoverPair.Current, false, _epoch);

    internal Web6CommittedSetup? CommittedSetup { get; private set; }

    internal async Task<JObject> Handle(string action, Guid? operation, JObject? committedSetup = null)
    {
        var reply = new JObject();
        Guid Required() => operation.HasValue && operation != Guid.Empty ? operation.Value : throw new InvalidDataException("Cutover operation is required.");
        if (action != "committedUnavailable")
        {
            if (committedSetup != null) throw new InvalidDataException("Unexpected committed proof.");
            await _runtime.VerifyLifecycle();
        }
        switch (action)
        {
            case "oldClientNewHostOn":
                await _runtime.Switch(Web6CutoverPair.Current, true, _epoch); _cached = true; _old = false;
                reply["marker"] = "old-client-new-host"; break;
            case "oldClientNewHostOff":
                if (_cached) { await _runtime.Switch(Web6CutoverPair.Current, false, _epoch); _cached = false; }
                break;
            case "newClientOldHostOn":
                await _runtime.Switch(Web6CutoverPair.Old, false, Guid.Empty); _old = true; _cached = false;
                reply["marker"] = "new-client-old-host"; break;
            case "newClientOldHostOff":
                if (_old) { await _runtime.Switch(Web6CutoverPair.Current, false, _epoch); _old = false; }
                break;
            case "sameEpochRestart":
                if (_cached || _old || _protectedOperation.HasValue) throw new InvalidDataException("Cutover restart order changed.");
                _protectedOperation = Required();
                await _runtime.VerifyOperation(Required(), false);
                await _runtime.Switch(Web6CutoverPair.Current, false, _epoch);
                await _runtime.VerifyOperation(Required(), false);
                _observed.Add("restart"); reply["deploymentEpoch"] = _epoch.ToString("D"); break;
            case "revokeGrantOn":
                if (_protectedOperation != Required()) throw new InvalidDataException("Cutover operation changed.");
                await _runtime.SetGrant(false); _revoked = true; _observed.Add("revoke"); break;
            case "revokeGrantOff":
                if (operation.HasValue && _protectedOperation != operation) throw new InvalidDataException("Cutover operation changed.");
                if (_revoked) { await _runtime.SetGrant(true); _revoked = false; _observed.Add("grant-restored"); }
                break;
            case "committedUnavailable":
                if (CommittedSetup != null || _cached || _old || _protectedOperation.HasValue) throw new InvalidDataException("Committed setup order changed.");
                var proof = Web6CommittedSetup.Parse(committedSetup, Required());
                await _runtime.VerifyCommittedSetup(proof);
                await _runtime.MakeCommittedUnavailable(Required());
                CommittedSetup = proof; _observed.Add("unavailable"); reply["marker"] = "committed-unavailable"; break;
            case "restoreEpoch":
                if (_protectedOperation != Required() || !_observed.Contains("restart") || _epoch != _plan.CurrentEpoch)
                    throw new InvalidDataException("Cutover restore order or identity changed.");
                await _runtime.Switch(Web6CutoverPair.Restored, false, _plan.RestoredEpoch);
                _epoch = _plan.RestoredEpoch; _observed.Add("restore"); reply["deploymentEpoch"] = _epoch.ToString("D"); break;
            case "verifyNoCutoverMutation":
                await _runtime.VerifyZeroMutation(Required());
                _observed.Add(_cached ? "old-client" : _old ? "old-host" : CommittedSetup?.Operation == operation ? "committed" : _protectedOperation == operation && _epoch == _plan.RestoredEpoch ? "protected" : "invalid"); break;
            default: throw new InvalidDataException("Unknown cutover action.");
        }
        await _runtime.VerifyLifecycle();
        return reply;
    }

    internal void VerifyComplete()
    {
        if (CommittedSetup == null || _revoked || _cached || _old || !_observed.SetEquals(new[] { "old-client", "old-host", "restart", "revoke", "grant-restored", "unavailable", "committed", "restore", "protected" }))
            throw new InvalidDataException("Cutover admissions, transitions and zero-mutation evidence are incomplete.");
    }
}

internal static class Web6CutoverLifetime
{
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
        if (drained)
            foreach (var dispose in disposeAllocations)
                try { await dispose().ConfigureAwait(false); }
                catch (Exception failure) { failures.Add(failure); if (stopOnCleanupFailure) break; }
        if (failures.Count == 1) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new AggregateException("Cutover run and/or cleanup failed.", failures);
    }
}
