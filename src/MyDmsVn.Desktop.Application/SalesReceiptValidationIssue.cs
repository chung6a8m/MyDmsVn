using System;

namespace MyDmsVn.Desktop.Application;

/// <summary>A current response detail bound to the submitted line reference, when possible.</summary>
public sealed class SalesReceiptValidationIssue
{
    public SalesReceiptValidationIssue(SalesReceiptLineViewModel? line, string field, string code, string message, string source)
    {
        Line = line;
        Field = field ?? throw new ArgumentNullException(nameof(field));
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public SalesReceiptLineViewModel? Line { get; }
    public string Field { get; }
    public string Code { get; }
    public string Message { get; }
    public string Source { get; }
}
