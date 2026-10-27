using System;
using System.Collections.Generic;
using System.Threading;
using MyDmsVn.Bootstrap5WinFormUI.Controls;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.WinForms;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed class WorkspaceBusyBinderTests
{
    [Fact]
    public void Old_completion_cannot_release_replacement_document_token()
    {
        using var scope = new WorkspaceFeedbackScope();
        var tokens = new List<Token>();
        using var binder = new WorkspaceBusyBinder(scope, presentation =>
        {
            var token = new Token(presentation);
            tokens.Add(token);
            return token;
        });
        var first = binder.CaptureOwner();
        binder.Project(first, true, "Loading");
        Assert.Single(tokens);
        scope.AdvanceDocument();
        Assert.True(tokens[0].Disposed);
        var second = binder.CaptureOwner();
        binder.Project(second, true, "Saving");
        binder.Project(first, false, "Loading");
        Assert.Equal(2, tokens.Count);
        Assert.False(tokens[1].Disposed);
        binder.Project(second, false, "Saving");
        Assert.True(tokens[1].Disposed);
    }

    [Fact]
    public void Repeated_state_is_idempotent_and_scope_replacement_releases_old_token()
    {
        using var firstScope = new WorkspaceFeedbackScope();
        using var secondScope = new WorkspaceFeedbackScope();
        var tokens = new List<Token>();
        using var binder = new WorkspaceBusyBinder(firstScope, presentation =>
        {
            var token = new Token(presentation);
            tokens.Add(token);
            return token;
        });
        var oldOwner = binder.CaptureOwner();
        binder.Project(oldOwner, true, "Loading");
        binder.Project(oldOwner, true, "Loading");
        Assert.Single(tokens);
        binder.ReplaceScope(secondScope);
        Assert.True(tokens[0].Disposed);
        binder.Project(oldOwner, true, "Loading");
        Assert.Single(tokens);
        binder.Project(binder.CaptureOwner(), true, "Loading");
        Assert.Equal(2, tokens.Count);
        binder.Dispose();
        Assert.True(tokens[1].Disposed);
    }

    [Fact]
    public void Changing_busy_message_acquires_replacement_before_releasing_old_token()
    {
        using var scope = new WorkspaceFeedbackScope();
        var events = new List<string>();
        using var binder = new WorkspaceBusyBinder(scope, presentation =>
        {
            events.Add("begin " + presentation.Message);
            return new Token(presentation, () => events.Add("dispose " + presentation.Message));
        });
        var owner = binder.CaptureOwner();
        binder.Project(owner, true, "Loading");
        binder.Project(owner, true, "Saving");
        Assert.Equal(new[] { "begin Loading", "begin Saving", "dispose Loading" }, events);
    }

    [Fact]
    public void Background_scope_invalidation_releases_token_on_the_ui_dispatcher()
    {
        using var scope = new WorkspaceFeedbackScope();
        var queued = new List<Action>();
        Token? token = null;
        using var binder = new WorkspaceBusyBinder(scope, presentation =>
        {
            token = new Token(presentation);
            return token;
        }, action => queued.Add(action));
        binder.Project(binder.CaptureOwner(), true, "Loading");
        var worker = new Thread(scope.AdvanceDocument);
        worker.Start();
        Assert.True(worker.Join(TimeSpan.FromSeconds(2)));
        Assert.False(token!.Disposed);
        Assert.Single(queued)();
        Assert.True(token.Disposed);
    }

    [Fact]
    public void Queued_old_invalidation_cannot_release_a_new_document_presentation()
    {
        using var scope = new WorkspaceFeedbackScope();
        var queued = new List<Action>();
        var tokens = new List<Token>();
        using var binder = new WorkspaceBusyBinder(scope, presentation =>
        {
            var token = new Token(presentation);
            tokens.Add(token);
            return token;
        }, action => queued.Add(action));
        binder.Project(binder.CaptureOwner(), true, "Old");
        var worker = new Thread(scope.AdvanceDocument);
        worker.Start();
        Assert.True(worker.Join(TimeSpan.FromSeconds(2)));
        binder.Project(binder.CaptureOwner(), true, "New");
        Assert.True(tokens[0].Disposed);
        Assert.False(tokens[1].Disposed);
        Assert.Single(queued)();
        Assert.False(tokens[1].Disposed);
    }

    private sealed class Token : IDisposable
    {
        public Token(BusyPresentation presentation, Action? onDispose = null)
        { Presentation = presentation; _onDispose = onDispose; }
        private readonly Action? _onDispose;
        public BusyPresentation Presentation { get; }
        public bool Disposed { get; private set; }
        public void Dispose() { if (Disposed) return; Disposed = true; _onDispose?.Invoke(); }
    }
}
