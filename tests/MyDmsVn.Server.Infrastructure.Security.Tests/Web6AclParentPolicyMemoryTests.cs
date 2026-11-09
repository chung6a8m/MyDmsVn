using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

// Calls the same policy and lifecycle used by the native dedicated-parent owner. No filesystem or SQL.
public sealed class Web6AclParentPolicyMemoryTests
{
    private const string Shared = @"C:\evidence";
    private const string Parent = @"C:\evidence\owned";
    private const string Target = Parent + @"\target";
    private const string Control = Parent + @"\control";
    private const string Secret = @"C:\private-canary\secret-token";

    [Fact]
    public void Owned_layout_is_separate_from_shared_temp_and_control_is_a_sibling()
    {
        SqlTestAclParentPolicy.ValidateLayout(Shared, Parent, Target, Control);
        var snapshot = new Dictionary<string, string[]> { [Shared] = new[] { Parent, Secret }, [Parent] = new[] { Target } };
        string? enumerated = null; var enumerations = 0;
        SqlTestAclParentPolicy.RequireMembership(SqlTestAclMembership.TargetOnly, Parent, Target, null, path => {
            enumerations++; enumerated = path; return snapshot[path];
        });
        Assert.Equal(1, enumerations);
        Assert.Equal(Parent, enumerated); // Shared TEMP's unrelated sibling is outside the enumerated scope.
    }

    [Theory]
    [InlineData("shared")]
    [InlineData("parent-case")]
    [InlineData("nested-control")]
    [InlineData("other-parent")]
    [InlineData("traversal")]
    [InlineData("same-control")]
    public void Shared_or_wrongly_nested_layout_is_refused(string kind)
    {
        var parent = kind == "shared" ? Shared : Parent;
        var target = kind == "parent-case" ? Parent : kind == "other-parent" ? Shared + @"\foreign" : Target;
        var control = kind == "nested-control" ? Target + @"\control" : kind == "traversal" ? Parent + @"\child\..\control" : kind == "same-control" ? Target : Control;
        var error = Assert.Throws<SqlTestAclParentPolicyException>(() => SqlTestAclParentPolicy.ValidateLayout(Shared, parent, target, control));
        Assert.Equal("InvalidParentBinding", error.Reason.ToString());
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("acl")]
    [InlineData("unprotected")]
    [InlineData("reparse")]
    [InlineData("unowned")]
    public void Original_parent_identity_descriptor_protection_and_ancestry_are_required(string kind)
    {
        Action check = () => SqlTestAclParentPolicy.VerifyIdentity(
            kind == "unowned" ? "" : "native-original", new byte[] { 1, 2 },
            kind == "identity" ? "replacement" : "native-original", kind == "acl" ? new byte[] { 9 } : new byte[] { 1, 2 },
            kind != "unprotected", kind == "reparse");
        var error = Assert.Throws<SqlTestAclParentPolicyException>(check);
        Assert.Equal("ParentIdentityMismatch", error.Reason.ToString());
        var calls = new List<SqlTestAclPhase>();
        var coordinator = new SqlTestAclMutationCoordinator(new PolicyOwner(calls, () => new[] { Target }, check), _ => { });
        var failure = Assert.Throws<SqlTestAclPhaseException>(() => coordinator.Prepare());
        Assert.Equal(SqlTestAclPhase.ParentAncestors, failure.Phase);
        Assert.Equal("ParentIdentityMismatch", failure.Reason.ToString());
        Assert.Equal(new[] { SqlTestAclPhase.CaseVerify, SqlTestAclPhase.AttemptAdmission, SqlTestAclPhase.ParentAncestors }, calls);
        Assert.Throws<SqlTestAclPhaseException>(() => coordinator.RunMutation(() => calls.Add(SqlTestAclPhase.Mutation), () => { }));
        Assert.Equal(3, calls.Count);
    }

    [Fact]
    public void Exact_phase_memberships_and_original_identity_pass()
    {
        SqlTestAclParentPolicy.VerifyIdentity("original", new byte[] { 1 }, "original", new byte[] { 1 }, true, false);
        SqlTestAclParentPolicy.RequireMembership(SqlTestAclMembership.TargetOnly, Parent, Target, null, _ => new[] { Target.ToUpperInvariant() });
        SqlTestAclParentPolicy.RequireMembership(SqlTestAclMembership.Prepared, Parent, Target, Control, _ => new[] { Control, Target });
        SqlTestAclParentPolicy.RequireMembership(SqlTestAclMembership.TargetRemoved, Parent, Target, Control, _ => new[] { Control });
        SqlTestAclParentPolicy.RequireMembership(SqlTestAclMembership.Empty, Parent, Target, Control, _ => Array.Empty<string>());
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("foreign")]
    [InlineData("extra")]
    [InlineData("duplicate")]
    [InlineData("prepared-missing")]
    [InlineData("prepared-extra")]
    [InlineData("removed-still-target")]
    [InlineData("empty-still-control")]
    public void Unexpected_membership_is_not_filtered_or_accepted_in_another_phase(string kind)
    {
        var phase = kind.StartsWith("prepared") ? SqlTestAclMembership.Prepared : kind.StartsWith("removed") ? SqlTestAclMembership.TargetRemoved : kind.StartsWith("empty-still") ? SqlTestAclMembership.Empty : SqlTestAclMembership.TargetOnly;
        var entries = kind == "empty" ? Array.Empty<string>() : kind == "foreign" ? new[] { Secret } :
            kind == "extra" ? new[] { Target, Secret } : kind == "duplicate" ? new[] { Target, Target } :
            kind == "prepared-missing" ? new[] { Target } : kind == "prepared-extra" ? new[] { Target, Control, Secret } :
            kind == "removed-still-target" ? new[] { Target, Control } : new[] { Control };
        var error = Assert.Throws<SqlTestAclParentPolicyException>(() => SqlTestAclParentPolicy.RequireMembership(phase, Parent, Target,
            phase == SqlTestAclMembership.TargetOnly ? null : Control, _ => entries));
        Assert.Equal("UnexpectedMembership", error.Reason.ToString()); Assert.DoesNotContain(Secret, error.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Real_membership_policy_distinguishes_enumeration_and_mismatch_and_makes_coordinator_terminal(bool enumerationFails)
    {
        var original = new IOException(Secret); var calls = new List<SqlTestAclPhase>(); var markers = new List<string>();
        var owner = new PolicyOwner(calls, () => { if (enumerationFails) throw original; return new[] { Target, Secret }; });
        var coordinator = new SqlTestAclMutationCoordinator(owner, markers.Add);
        var error = Assert.Throws<SqlTestAclPhaseException>(() => coordinator.Prepare());
        Assert.Equal(SqlTestAclPhase.ParentMembership, error.Phase);
        Assert.Equal(enumerationFails ? "EnumerationFailed" : "UnexpectedMembership", error.Reason.ToString());
        var policyError = Assert.IsType<SqlTestAclParentPolicyException>(error.OriginalFailure);
        Assert.Equal(enumerationFails ? original : null, policyError.OriginalFailure);
        Assert.Equal(SqlTestAclPhase.ParentMembership, calls[calls.Count - 1]);
        var count = calls.Count;
        Assert.Throws<SqlTestAclPhaseException>(() => coordinator.RunMutation(() => calls.Add(SqlTestAclPhase.Mutation), () => { }));
        Assert.Throws<SqlTestAclPhaseException>(() => coordinator.Prepare()); Assert.Equal(count, calls.Count);
        Assert.DoesNotContain(Secret, error.ToString());
        Assert.All(markers, marker => Assert.DoesNotContain(Secret, marker));
        Assert.Contains("WEB6_ACL phase=ParentMembership result=Started reason=None", markers);
        Assert.Contains("WEB6_ACL phase=ParentMembership result=Failed reason=" + error.Reason, markers);
    }

    [Fact]
    public void Scope_lifecycle_requires_sealed_control_then_verified_children_removal_before_parent_removal()
    {
        var state = new SqlTestAclParentLifecycle(); var calls = new List<string>();
        state.CreateTarget(() => calls.Add("empty"), () => calls.Add("target"));
        state.SealControl(() => calls.Add("sealed-control"));
        state.ChildrenRemoved(() => calls.Add("children-absent"));
        state.Remove(() => calls.Add("identity-empty"), () => calls.Add("remove-exact-parent"));
        Assert.Equal(new[] { "empty", "target", "sealed-control", "children-absent", "identity-empty", "remove-exact-parent" }, calls);
        Assert.True(state.Removed);
        Assert.Throws<SqlTestAclParentPolicyException>(() => state.Remove(() => { }, () => calls.Add("repeat")));
        Assert.Equal(6, calls.Count);
    }

    [Theory]
    [InlineData("create-failure")]
    [InlineData("before-seal")]
    [InlineData("seal-failure")]
    [InlineData("empty-failure")]
    [InlineData("disposed")]
    public void Partial_or_uncertain_scope_never_invokes_parent_deletion(string kind)
    {
        var state = new SqlTestAclParentLifecycle(); var deletions = 0;
        if (kind == "create-failure") {
            Assert.Throws<IOException>(() => state.CreateTarget(() => { }, () => throw new IOException(Secret)));
            Assert.Throws<SqlTestAclParentPolicyException>(() => state.Remove(() => { }, () => deletions++));
            Assert.Equal(0, deletions); Assert.False(state.Removed); return;
        }
        state.CreateTarget(() => { }, () => { });
        if (kind == "seal-failure") Assert.Throws<IOException>(() => state.SealControl(() => throw new IOException(Secret)));
        else if (kind != "before-seal") state.SealControl(() => { });
        if (kind == "empty-failure") Assert.Throws<IOException>(() => state.ChildrenRemoved(() => throw new IOException(Secret)));
        if (kind == "disposed") state.Abandon();
        Assert.Throws<SqlTestAclParentPolicyException>(() => state.Remove(() => { }, () => deletions++));
        Assert.Equal(0, deletions); Assert.False(state.Removed);
    }

    [Fact]
    public void Scope_failure_cannot_be_swallowed_into_a_later_successful_transition()
    {
        var state = new SqlTestAclParentLifecycle(); var deletions = 0;
        Assert.Throws<SqlTestAclParentPolicyException>(() => state.CreateTarget(() => { }, () => {
            Assert.Throws<SqlTestAclParentPolicyException>(() => state.Remove(() => { }, () => deletions++));
        }));
        Assert.Throws<SqlTestAclParentPolicyException>(() => state.SealControl(() => { }));
        Assert.Equal(0, deletions);
    }

    [Fact]
    public void Failed_transition_caught_by_create_verifier_never_enters_create_callback()
    {
        var state = new SqlTestAclParentLifecycle(); var creates = 0; var deletes = 0;
        var error = Assert.Throws<SqlTestAclParentPolicyException>(() => state.CreateTarget(() => {
            var rejected = Assert.Throws<SqlTestAclParentPolicyException>(() => state.Remove(() => { }, () => deletes++));
            Assert.Equal(SqlTestAclReason.TerminalFailure, rejected.Reason);
        }, () => creates++));
        Assert.Equal(SqlTestAclReason.TerminalFailure, error.Reason);
        Assert.Equal(0, creates); Assert.Equal(0, deletes);
        Assert.Throws<SqlTestAclParentPolicyException>(() => state.CreateTarget(() => { }, () => creates++));
        Assert.Equal(0, creates);
    }

    [Fact]
    public void Failed_transition_caught_by_remove_verifier_never_enters_delete_callback()
    {
        var state = new SqlTestAclParentLifecycle(); var creates = 0; var deletes = 0;
        state.CreateTarget(() => { }, () => { });
        state.SealControl(() => { }); state.ChildrenRemoved(() => { });
        var error = Assert.Throws<SqlTestAclParentPolicyException>(() => state.Remove(() => {
            var rejected = Assert.Throws<SqlTestAclParentPolicyException>(() => state.CreateTarget(() => { }, () => creates++));
            Assert.Equal(SqlTestAclReason.TerminalFailure, rejected.Reason);
        }, () => deletes++));
        Assert.Equal(SqlTestAclReason.TerminalFailure, error.Reason);
        Assert.Equal(0, creates); Assert.Equal(0, deletes); Assert.False(state.Removed);
        Assert.Throws<SqlTestAclParentPolicyException>(() => state.Remove(() => { }, () => deletes++));
        Assert.Equal(0, deletes);
    }

    private sealed class PolicyOwner : ISqlTestAclPreparation
    {
        private readonly List<SqlTestAclPhase> _calls; private readonly Func<string[]> _enumerate;
        private readonly Action? _validateParent;
        internal PolicyOwner(List<SqlTestAclPhase> calls, Func<string[]> enumerate, Action? validateParent = null)
        { _calls = calls; _enumerate = enumerate; _validateParent = validateParent; }
        public bool IsSealed { get { _calls.Add(SqlTestAclPhase.SealGate); return false; } }
        public void Execute(SqlTestAclPhase phase)
        {
            _calls.Add(phase);
            if (phase == SqlTestAclPhase.ParentAncestors) _validateParent?.Invoke();
            if (phase == SqlTestAclPhase.ParentMembership)
                SqlTestAclParentPolicy.RequireMembership(SqlTestAclMembership.TargetOnly, Parent, Target, null, _ => _enumerate());
        }
    }
}
