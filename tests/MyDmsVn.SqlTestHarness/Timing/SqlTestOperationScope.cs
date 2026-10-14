using System;
using System.Diagnostics;
using System.Threading;

namespace MyDmsVn.SqlTestHarness.Timing;

public sealed class SqlTestOperationScope : IDisposable
{
    private readonly SqlTestMeasurementContext _context;
    private readonly string _stage;
    private readonly string _operationId;
    private readonly string? _parentOperationId;
    private readonly long _startTimestamp;
    private SqlTestTimingOutcome _outcome = SqlTestTimingOutcome.Succeeded;
    private int _disposeState;

    internal SqlTestOperationScope(
        SqlTestMeasurementContext context,
        string stage,
        string operationId,
        string? parentOperationId)
    {
        _context = context;
        _stage = stage;
        _operationId = operationId;
        _parentOperationId = parentOperationId;
        _startTimestamp = Stopwatch.GetTimestamp();
    }

    public string OperationId => _operationId;

    public void Fail() => _outcome = SqlTestTimingOutcome.Failed;

    public void Cancel() => _outcome = SqlTestTimingOutcome.Canceled;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        var endTimestamp = Stopwatch.GetTimestamp();
        if (!_context.Enabled)
        {
            return;
        }

        var durationMs =
            (decimal)(endTimestamp - _startTimestamp) * 1000m / Stopwatch.Frequency;
        _context.Sink.Write(
            new SqlTestTimingEvent(
                _context.RunId,
                _context.TestCaseId,
                _operationId,
                _parentOperationId,
                _stage,
                _startTimestamp,
                endTimestamp,
                durationMs,
                _outcome,
                _context.Tfm,
                _context.AnonymizedDatabaseId));
    }
}
