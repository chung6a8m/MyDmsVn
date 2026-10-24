using System;

namespace MyDmsVn.Desktop.WinForms;

// The app owns candidate eligibility. SourceGrid still owns BeginEdit/EndEdit and
// each document adapter owns parsing, edit admission, and the model setter.
internal sealed class InlineEditSession<TLine> where TLine : class
{
    private InlineEditTarget<TLine>? _current;

    internal InlineEditTarget<TLine> Begin(InlineValidationAddress address, TLine line, object? originalValue, string originalRaw)
    {
        if (line == null) throw new ArgumentNullException(nameof(line));
        if (!ReferenceEquals(address.LineIdentity, line)) throw new ArgumentException("The address must identify the edited line.", nameof(address));
        Invalidate();
        return _current = new InlineEditTarget<TLine>(address, line, originalValue, originalRaw);
    }

    internal void InvalidateValidation(InlineEditTarget<TLine> target)
    {
        if (IsCurrent(target)) target.CandidateValidated = false;
    }

    internal bool RecordValidatedCandidate(InlineEditTarget<TLine> target, object? value)
    {
        if (!IsCurrent(target)) return false;
        target.ValidatedValue = value;
        target.CandidateValidated = true;
        return true;
    }

    internal bool HasValidatedCandidate(InlineEditTarget<TLine> target) => IsCurrent(target) && target.CandidateValidated;

    internal bool TryApplyValidated(InlineEditTarget<TLine> target, Func<object?, bool> admitCurrent, Func<object?, bool> applyOnce)
    {
        if (admitCurrent == null) throw new ArgumentNullException(nameof(admitCurrent));
        if (applyOnce == null) throw new ArgumentNullException(nameof(applyOnce));
        if (!HasValidatedCandidate(target)) return false;
        var value = target.ValidatedValue;
        target.CandidateValidated = false; // Consume before either callback can reenter.
        return admitCurrent(value) && applyOnce(value);
    }

    internal void Invalidate()
    {
        if (_current != null) _current.CandidateValidated = false;
        _current = null;
    }

    private bool IsCurrent(InlineEditTarget<TLine> target) => target != null &&
        ReferenceEquals(_current, target) && _current.Address.Equals(target.Address) &&
        ReferenceEquals(target.Address.LineIdentity, target.Line);
}

internal sealed class InlineEditTarget<TLine> where TLine : class
{
    internal InlineEditTarget(InlineValidationAddress address, TLine line, object? originalValue, string originalRaw)
    {
        Address = address;
        Line = line;
        OriginalValue = originalValue;
        OriginalRaw = originalRaw ?? throw new ArgumentNullException(nameof(originalRaw));
        RawCandidate = originalRaw;
    }

    internal InlineValidationAddress Address { get; }
    internal TLine Line { get; }
    internal object? OriginalValue { get; }
    internal string OriginalRaw { get; private set; }
    internal string RawCandidate { get; set; }
    internal bool CandidateValidated { get; set; }
    internal object? ValidatedValue { get; set; }

    // Native OnEditStarted is the first point at which the editor's initial text is available.
    internal void CaptureOriginalRaw(string raw)
    {
        OriginalRaw = raw ?? throw new ArgumentNullException(nameof(raw));
        RawCandidate = raw;
    }
}
