using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;

namespace MyDmsVn.Desktop.UiLab.Scenarios;

public enum SalesScenarioId
{
    S01Draft,
    S02Validation,
    S03LookupRace,
    S04DateCaret,
    S05DirtyClose,
    S06UnknownPost,
}

public sealed class ScenarioJournal
{
    private readonly object _sync = new object();
    private readonly List<ScenarioJournalEntry> _entries = new List<ScenarioJournalEntry>();

    public IReadOnlyList<ScenarioJournalEntry> Entries
    {
        get { lock (_sync) return _entries.ToArray(); }
    }

    public void Record(string operation, object? request = null)
    {
        lock (_sync) _entries.Add(new ScenarioJournalEntry(_entries.Count + 1, operation, request));
    }
}

public sealed class ScenarioJournalEntry
{
    public ScenarioJournalEntry(int sequence, string operation, object? request)
    {
        Sequence = sequence;
        Operation = operation;
        Request = request;
    }

    public int Sequence { get; }
    public string Operation { get; }
    public object? Request { get; }
}

public sealed class ScenarioGate<T>
{
    private readonly TaskCompletionSource<T> _completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool IgnoreCancellation { get; set; }
    public bool IsCompleted => _completion.Task.IsCompleted;

    public Task<T> WaitAsync(CancellationToken cancellationToken)
    {
        if (IgnoreCancellation || !cancellationToken.CanBeCanceled) return _completion.Task;
        if (cancellationToken.IsCancellationRequested) return Task.FromCanceled<T>(cancellationToken);
        return WaitWithCancellationAsync(cancellationToken);
    }

    private async Task<T> WaitWithCancellationAsync(CancellationToken cancellationToken)
    {
        var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (cancellationToken.Register(() => canceled.TrySetResult(true)))
        {
            var winner = await Task.WhenAny(_completion.Task, canceled.Task).ConfigureAwait(false);
            if (winner == canceled.Task) cancellationToken.ThrowIfCancellationRequested();
            return await _completion.Task.ConfigureAwait(false);
        }
    }

    public bool Complete(T result) => _completion.TrySetResult(result);
    public bool Cancel() => _completion.TrySetCanceled();
}

internal sealed class ScenarioTaskTracker
{
    private readonly object _sync = new object();
    private readonly HashSet<Task> _tasks = new HashSet<Task>();

    public IReadOnlyList<Task> Outstanding
    {
        get { lock (_sync) return _tasks.ToArray(); }
    }

    public Task<T> Track<T>(Task<T> task)
    {
        lock (_sync) _tasks.Add(task);
        _ = task.ContinueWith(completed => { lock (_sync) _tasks.Remove(completed); },
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return task;
    }
}

public sealed class ScenarioDesktopSession : IDesktopSession
{
    private CurrentUserDto? _user = new CurrentUserDto(1, "ui-lab", "UI Lab");

    public event EventHandler? SessionChanged;
    public bool IsAuthenticated => _user != null;
    public DesktopSessionState State => IsAuthenticated ? DesktopSessionState.Authenticated : DesktopSessionState.Unauthenticated;
    public CurrentUserDto? CurrentUser => _user;
    public long Version { get; private set; } = 1;

    public void SignOut()
    {
        _user = null;
        Version++;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Replace(CurrentUserDto? user = null)
    {
        _user = user ?? new CurrentUserDto(2, "ui-lab-new", "UI Lab New Session");
        Version++;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }
}

public sealed class ScenarioPreviewCalculator : ISalesReceiptPreviewCalculator
{
    public decimal CalculateRevenue(decimal qty, decimal unitPrice, bool isGift)
    {
        if (qty < 0 || qty > 10000 || unitPrice < 0 || unitPrice > 1000000)
            throw new ArgumentOutOfRangeException(nameof(qty), "Outside the UI Lab demo range.");
        return isGift ? 0m : decimal.Round(qty * unitPrice, 4, MidpointRounding.AwayFromZero);
    }

    public decimal SumAmounts(IEnumerable<decimal> amounts) => amounts.Aggregate(0m, (sum, amount) => checked(sum + amount));
}

public static class ScenarioErrors
{
    public static ApiResponse<T> Unsupported<T>(string operation) => ApiResponse<T>.Failure(
        new ApiError(ApiStatusCode.BadRequest, "UiLab.Unsupported", operation + " is outside this scenario fixture."));

    public static ApiResponse<T> SessionChanged<T>() => ApiResponse<T>.Failure(
        new ApiError(ApiStatusCode.Unauthorized, "Auth.SessionChanged", "The UI Lab session changed."));
}
