using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

// All operations are injected in-memory: no native handles, filesystem, processes or SQL.
public sealed class Web6AclCoordinatorMemoryTests
{
    private const string Secret = @"C:\private-canary\token-secret-descriptor";
    private static readonly SqlTestAclPhase[] Preparation = {
        SqlTestAclPhase.CaseVerify, SqlTestAclPhase.AttemptAdmission,
        SqlTestAclPhase.ParentAncestors, SqlTestAclPhase.ParentAclRead, SqlTestAclPhase.ParentAclPolicy,
        SqlTestAclPhase.ParentMembership, SqlTestAclPhase.ParentPinOpen,
        SqlTestAclPhase.SiblingPathAdmission, SqlTestAclPhase.SiblingAncestors, SqlTestAclPhase.SiblingDescriptor,
        SqlTestAclPhase.SiblingCreate, SqlTestAclPhase.SiblingAclNormalize, SqlTestAclPhase.SiblingRootOpen,
        SqlTestAclPhase.SiblingRootCapture, SqlTestAclPhase.RecordCreate, SqlTestAclPhase.RecordAclCapture,
        SqlTestAclPhase.RecordSerialize, SqlTestAclPhase.RecordWrite, SqlTestAclPhase.RecordFlush,
        SqlTestAclPhase.RecordReadback, SqlTestAclPhase.RecordSeal, SqlTestAclPhase.IndependentVerify,
        SqlTestAclPhase.AuthorityRegister, SqlTestAclPhase.AuthorityReverify, SqlTestAclPhase.ReadyForMutation };

    public static IEnumerable<object[]> PreparationFailures => Preparation.Select(phase => new object[] { (int)phase });

    [Theory]
    [MemberData(nameof(PreparationFailures))]
    public void Preparation_failure_identifies_exact_substage_and_never_enters_later_operations(int value)
    {
        var phase = (SqlTestAclPhase)value;
        var owner = new MemoryOwner { FailAt = phase };
        var markers = new List<string>();
        var coordinator = new SqlTestAclMutationCoordinator(owner, markers.Add);
        var error = Assert.Throws<SqlTestAclPhaseException>(() => coordinator.Prepare());
        Assert.Equal(phase, error.Phase);
        Assert.Same(owner.Failure, error.OriginalFailure);
        Assert.Equal(Preparation.Take(Array.IndexOf(Preparation, phase) + 1), owner.Calls);
        var before = owner.Calls.ToArray();
        Assert.Throws<SqlTestAclPhaseException>(() => coordinator.RunMutation(() => owner.Calls.Add(SqlTestAclPhase.Mutation), () => { }));
        Assert.Equal(before, owner.Calls);
        Assert.Contains("WEB6_ACL phase=" + phase + " result=Failed reason=OperationFailed", markers);
        AssertSafe(markers, error);
    }

    [Fact]
    public void Successful_preparation_earns_authority_before_fresh_verify_mutation_and_restoration()
    {
        var owner = new MemoryOwner(); var markers = new List<string>();
        var coordinator = new SqlTestAclMutationCoordinator(owner, markers.Add);
        coordinator.Prepare();
        coordinator.RunMutation(() => owner.Calls.Add(SqlTestAclPhase.Mutation), () => owner.Calls.Add(SqlTestAclPhase.Assertion));
        Assert.Equal(Preparation.Concat(new[] { SqlTestAclPhase.SealGate, SqlTestAclPhase.FreshVerify,
            SqlTestAclPhase.Mutation, SqlTestAclPhase.Assertion, SqlTestAclPhase.Restoration }), owner.Calls);
        AssertSafe(markers, null);
        var count = owner.Calls.Count;
        Assert.Throws<SqlTestAclPhaseException>(() => coordinator.RunMutation(() => throw new Exception(Secret), () => { }));
        Assert.Equal(count, owner.Calls.Count);
    }

    [Fact]
    public void Lost_seal_refuses_successful_verifier_mutation_assertion_and_restoration()
    {
        var owner = new MemoryOwner(); var coordinator = new SqlTestAclMutationCoordinator(owner, _ => { });
        coordinator.Prepare(); owner.Sealed = false; owner.Calls.Clear();
        var error = Assert.Throws<SqlTestAclPhaseException>(() => coordinator.RunMutation(
            () => owner.Calls.Add(SqlTestAclPhase.Mutation), () => owner.Calls.Add(SqlTestAclPhase.Assertion)));
        Assert.Equal(SqlTestAclReason.Unsealed, error.Reason);
        Assert.Equal(new[] { SqlTestAclPhase.SealGate }, owner.Calls);
    }

    [Theory]
    [InlineData((int)SqlTestAclPhase.FreshVerify)]
    [InlineData((int)SqlTestAclPhase.Mutation)]
    [InlineData((int)SqlTestAclPhase.Assertion)]
    [InlineData((int)SqlTestAclPhase.Restoration)]
    public void Admitted_failure_has_exact_phase_and_restores_only_after_mutation_boundary(int value)
    {
        var phase = (SqlTestAclPhase)value; var owner = new MemoryOwner(); var markers = new List<string>();
        var coordinator = new SqlTestAclMutationCoordinator(owner, markers.Add); coordinator.Prepare();
        owner.Calls.Clear(); owner.FailAt = phase;
        var error = Assert.Throws<SqlTestAclPhaseException>(() => coordinator.RunMutation(
            () => owner.Execute(SqlTestAclPhase.Mutation), () => owner.Execute(SqlTestAclPhase.Assertion)));
        Assert.Equal(phase, error.Phase); Assert.Same(owner.Failure, error.OriginalFailure);
        var expected = new List<SqlTestAclPhase> { SqlTestAclPhase.SealGate, SqlTestAclPhase.FreshVerify };
        if (phase != SqlTestAclPhase.FreshVerify) {
            expected.Add(SqlTestAclPhase.Mutation);
            if (phase != SqlTestAclPhase.Mutation) expected.Add(SqlTestAclPhase.Assertion);
            expected.Add(SqlTestAclPhase.Restoration);
        }
        Assert.Equal(expected, owner.Calls); AssertSafe(markers, error);
    }

    [Fact]
    public void Repeat_preparation_is_refused_without_invoking_owner()
    {
        var owner = new MemoryOwner(); var coordinator = new SqlTestAclMutationCoordinator(owner, _ => { });
        coordinator.Prepare(); var count = owner.Calls.Count;
        Assert.Throws<SqlTestAclPhaseException>(() => coordinator.Prepare()); Assert.Equal(count, owner.Calls.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Rejected_repeat_prepare_is_terminal_before_diagnostics_and_all_later_admissions(bool diagnosticFails)
    {
        var owner = new MemoryOwner(); var markers = new List<string>();
        SqlTestAclMutationCoordinator? coordinator = null;
        Exception? duringDiagnostic = null;
        var armed = false; var observing = false;
        coordinator = new SqlTestAclMutationCoordinator(owner, marker => {
            markers.Add(marker);
            if (armed && !observing && marker.Contains("reason=AlreadyAttempted")) {
                observing = true;
                duringDiagnostic = Record.Exception(() => coordinator!.RunMutation(
                    () => owner.Execute(SqlTestAclPhase.Mutation), () => owner.Execute(SqlTestAclPhase.Assertion)));
                if (diagnosticFails) throw new IOException(Secret);
            }
        });
        coordinator.Prepare(); owner.Calls.Clear(); markers.Clear(); armed = true;
        var first = Assert.Throws<SqlTestAclPhaseException>(() => coordinator.Prepare());
        Assert.Equal(SqlTestAclReason.AlreadyAttempted, first.Reason);
        Assert.Empty(owner.Calls); // Includes seal getter, fresh verifier, mutation, assertion and restoration.
        var reentrant = Assert.IsType<SqlTestAclPhaseException>(duringDiagnostic);
        Assert.Equal("TerminalFailure", reentrant.Reason.ToString());
        var laterPrepare = Assert.Throws<SqlTestAclPhaseException>(() => coordinator.Prepare());
        var laterMutation = Assert.Throws<SqlTestAclPhaseException>(() => coordinator.RunMutation(
            () => owner.Execute(SqlTestAclPhase.Mutation), () => owner.Execute(SqlTestAclPhase.Assertion)));
        Assert.Empty(owner.Calls);
        Assert.Equal("TerminalFailure", laterPrepare.Reason.ToString());
        Assert.Equal(laterPrepare.Message, laterMutation.Message);
        Assert.Equal(reentrant.Message, laterMutation.Message);
        Assert.Same(first, laterPrepare.OriginalFailure);
        Assert.Same(first, laterMutation.OriginalFailure);
        Assert.Equal(diagnosticFails, first.DiagnosticFailure != null);
        AssertSafe(markers, first); AssertSafe(markers, laterMutation);
    }

    [Fact]
    public void Mutation_before_preparation_permanently_refuses_later_preparation()
    {
        var owner = new MemoryOwner(); var coordinator = new SqlTestAclMutationCoordinator(owner, _ => { });
        var first = Assert.Throws<SqlTestAclPhaseException>(() => coordinator.RunMutation(
            () => owner.Execute(SqlTestAclPhase.Mutation), () => owner.Execute(SqlTestAclPhase.Assertion)));
        Assert.Equal(SqlTestAclReason.NotReady, first.Reason);
        var later = Assert.Throws<SqlTestAclPhaseException>(() => coordinator.Prepare());
        Assert.Equal("TerminalFailure", later.Reason.ToString());
        Assert.Same(first, later.OriginalFailure); Assert.Empty(owner.Calls);
    }

    [Fact]
    public void Failed_diagnostic_sink_never_advances_preparation()
    {
        var owner = new MemoryOwner();
        var coordinator = new SqlTestAclMutationCoordinator(owner, _ => throw new IOException(Secret));
        var error = Assert.Throws<SqlTestAclPhaseException>(() => coordinator.Prepare());
        Assert.Empty(owner.Calls); Assert.Equal(SqlTestAclReason.DiagnosticUnavailable, error.Reason);
        var laterPrepare = Assert.Throws<SqlTestAclPhaseException>(() => coordinator.Prepare());
        var laterMutation = Assert.Throws<SqlTestAclPhaseException>(() => coordinator.RunMutation(
            () => owner.Execute(SqlTestAclPhase.Mutation), () => owner.Execute(SqlTestAclPhase.Assertion)));
        Assert.Empty(owner.Calls);
        Assert.Equal("TerminalFailure", laterPrepare.Reason.ToString());
        Assert.Equal(laterPrepare.Message, laterMutation.Message);
        Assert.Same(error, laterPrepare.OriginalFailure);
        Assert.Same(error, laterMutation.OriginalFailure);
        AssertSafe(Array.Empty<string>(), error); AssertSafe(Array.Empty<string>(), laterMutation);
    }

    [Theory]
    [InlineData("phase=Mutation result=Started", false)]
    [InlineData("phase=Mutation result=Completed", true)]
    [InlineData("phase=Restoration result=Started", true)]
    public void Diagnostic_failure_cannot_bypass_admission_or_prevent_required_restoration(string failMarker, bool entered)
    {
        var owner = new MemoryOwner();
        var coordinator = new SqlTestAclMutationCoordinator(owner, marker => {
            if (marker.Contains(failMarker)) throw new IOException(Secret);
        });
        coordinator.Prepare(); owner.Calls.Clear();
        var error = Assert.Throws<SqlTestAclPhaseException>(() => coordinator.RunMutation(
            () => owner.Execute(SqlTestAclPhase.Mutation), () => owner.Execute(SqlTestAclPhase.Assertion)));
        Assert.Equal(entered, owner.Calls.Contains(SqlTestAclPhase.Mutation));
        Assert.Equal(entered, owner.Calls.Contains(SqlTestAclPhase.Restoration));
        Assert.Equal(SqlTestAclReason.DiagnosticUnavailable, error.Reason);
        AssertSafe(Array.Empty<string>(), error);
    }

    [Fact]
    public void Restoration_failure_preserves_earlier_mutation_failure_privately()
    {
        var owner = new MemoryOwner(); var coordinator = new SqlTestAclMutationCoordinator(owner, _ => { });
        coordinator.Prepare(); owner.FailAt = SqlTestAclPhase.Restoration;
        var mutationFailure = new IOException(Secret);
        var error = Assert.Throws<SqlTestAclPhaseException>(() => coordinator.RunMutation(() => throw mutationFailure, () => { }));
        Assert.Equal(SqlTestAclPhase.Restoration, error.Phase);
        Assert.Same(owner.Failure, error.OriginalFailure);
        Assert.Same(mutationFailure, Assert.IsType<SqlTestAclPhaseException>(error.PriorFailure).OriginalFailure);
        AssertSafe(Array.Empty<string>(), error);
    }

    private static void AssertSafe(IEnumerable<string> markers, Exception? error)
    {
        foreach (var marker in markers) {
            Assert.DoesNotContain(Secret, marker);
            Assert.Matches(@"^WEB6_ACL phase=[A-Za-z]+ result=(Started|Completed|Failed) reason=(None|OperationFailed|Unsealed|NotReady|AlreadyAttempted|DiagnosticUnavailable|TerminalFailure|EnumerationFailed|UnexpectedMembership|ParentIdentityMismatch|InvalidParentBinding)$", marker);
        }
        if (error != null) { Assert.DoesNotContain(Secret, error.ToString()); Assert.Null(error.InnerException); }
    }

    private sealed class MemoryOwner : ISqlTestAclPreparation
    {
        internal readonly List<SqlTestAclPhase> Calls = new List<SqlTestAclPhase>();
        internal readonly Exception Failure = new IOException(Secret);
        internal SqlTestAclPhase? FailAt;
        internal bool Sealed;
        private bool _readback, _registered;
        public bool IsSealed { get { Calls.Add(SqlTestAclPhase.SealGate); return Sealed && _registered; } }
        public void Execute(SqlTestAclPhase phase)
        {
            Calls.Add(phase);
            if (phase == FailAt) throw Failure;
            if (phase == SqlTestAclPhase.RecordReadback) _readback = true;
            if (phase == SqlTestAclPhase.RecordSeal) { Assert.True(_readback); Sealed = true; }
            if (phase == SqlTestAclPhase.AuthorityRegister) { Assert.True(Sealed); _registered = true; }
            if (phase == SqlTestAclPhase.ReadyForMutation) Assert.True(Sealed && _registered);
        }
    }
}
