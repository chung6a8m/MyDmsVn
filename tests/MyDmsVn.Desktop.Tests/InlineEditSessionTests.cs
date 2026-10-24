using System;
using MyDmsVn.Desktop.WinForms;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed class InlineEditSessionTests
{
    [Fact]
    public void Validated_candidate_is_the_only_value_applied_and_is_consumed_before_reentry()
    {
        var line = new object();
        var session = new InlineEditSession<object>();
        var target = session.Begin(new InlineValidationAddress(1, line, "quantity"), line, 1m, "1");
        target.RawCandidate = "2.5";
        Assert.True(session.RecordValidatedCandidate(target, 2.5m));
        var applications = 0;
        Assert.True(session.TryApplyValidated(target, value => Equals(value, 2.5m), value =>
        {
            applications++;
            Assert.Equal(2.5m, value);
            Assert.False(session.TryApplyValidated(target, _ => true, _ => { applications++; return true; }));
            return true;
        }));
        Assert.Equal(1, applications);
        Assert.False(session.TryApplyValidated(target, _ => true, _ => { applications++; return true; }));
        Assert.Equal("2.5", target.RawCandidate);
    }

    [Fact]
    public void Native_value_mismatch_and_failed_revalidation_never_apply_the_old_candidate()
    {
        var line = new object();
        var session = new InlineEditSession<object>();
        var target = session.Begin(new InlineValidationAddress(1, line, "quantity"), line, 1m, "1");
        target.RawCandidate = "B";
        Assert.True(session.RecordValidatedCandidate(target, "A"));
        var applications = 0;
        Assert.False(session.TryApplyValidated(target, candidate => Equals(candidate, "B"), _ => { applications++; return true; }));
        Assert.Equal(0, applications);
        Assert.Equal("B", target.RawCandidate);
        Assert.True(session.RecordValidatedCandidate(target, "A"));
        session.InvalidateValidation(target); // A subsequent conversion failed.
        Assert.False(session.TryApplyValidated(target, _ => true, _ => { applications++; return true; }));
        Assert.Equal(0, applications);
        Assert.Equal("B", target.RawCandidate);
    }

    [Fact]
    public void Fresh_target_and_line_reference_fence_late_callbacks_even_for_matching_fields()
    {
        var first = new object();
        var second = new object();
        var session = new InlineEditSession<object>();
        var old = session.Begin(new InlineValidationAddress(4, first, "productId"), first, 7, "P7");
        Assert.True(session.RecordValidatedCandidate(old, 8));
        session.Invalidate();
        var current = session.Begin(new InlineValidationAddress(4, first, "productId"), first, 7, "P7");
        Assert.False(session.RecordValidatedCandidate(old, 9));
        Assert.False(session.TryApplyValidated(old, _ => true, _ => true));
        Assert.True(session.RecordValidatedCandidate(current, 8));
        Assert.True(session.TryApplyValidated(current, _ => true, _ => true));

        var other = session.Begin(new InlineValidationAddress(4, second, "productId"), second, 7, "P7");
        Assert.False(session.RecordValidatedCandidate(current, 9));
        Assert.True(session.RecordValidatedCandidate(other, 10));
        Assert.True(session.TryApplyValidated(other, value => Equals(value, 10), _ => true));
        Assert.Throws<ArgumentException>(() => session.Begin(new InlineValidationAddress(4, first, "quantity"), second, 1m, "1"));
    }

    [Fact]
    public void Native_start_snapshot_updates_original_raw_without_changing_the_model_snapshot()
    {
        var line = new object();
        var session = new InlineEditSession<object>();
        var target = session.Begin(new InlineValidationAddress(1, line, "quantity"), line, 1m, "");
        target.CaptureOriginalRaw("1.0000");
        target.RawCandidate = "bad";
        session.InvalidateValidation(target);
        Assert.Equal(1m, target.OriginalValue);
        Assert.Equal("1.0000", target.OriginalRaw);
        Assert.Equal("bad", target.RawCandidate);
    }
}
