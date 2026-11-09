using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

// Only injected managed callbacks/bytes. No native handles, files or SQL.
public sealed class Web6SqlSupervisionMemoryTests
{
    public static IEnumerable<object[]> Phases => Enum.GetValues(typeof(SqlTestSupervisionPhase)).Cast<SqlTestSupervisionPhase>().Select(x => new object[] { (int)x });
    private static async Task Run(SqlTestSupervisedCoordinator c, Func<SqlTestSupervisionPhase, Task> work, CancellationToken token = default)
    {
        foreach (var phase in Enum.GetValues(typeof(SqlTestSupervisionPhase)).Cast<SqlTestSupervisionPhase>())
        {
            if (phase == SqlTestSupervisionPhase.ProbeMutation)
                await c.ProbeAsync(() => work(phase), () => work(SqlTestSupervisionPhase.ProbeRefusal), () => work(SqlTestSupervisionPhase.Restoration), token);
            else if (phase != SqlTestSupervisionPhase.ProbeRefusal && phase != SqlTestSupervisionPhase.Restoration)
                await c.AdvanceAsync(phase, () => work(phase), token);
        }
    }
    [Fact]
    public async Task Exact_success_order_and_terminal_single_use()
    {
        var calls = new List<SqlTestSupervisionPhase>(); var c = new SqlTestSupervisedCoordinator(() => { });
        await Run(c, p => { calls.Add(p); return Task.CompletedTask; });
        Assert.Equal(Enum.GetValues(typeof(SqlTestSupervisionPhase)).Cast<SqlTestSupervisionPhase>(), calls);
        Assert.True(c.Completed); Assert.True(c.CreationMayHaveOccurred);
        await Assert.ThrowsAsync<SqlTestSupervisionException>(() => c.AdvanceAsync(SqlTestSupervisionPhase.MasterBinding, () => throw new Exception("forbidden"), default));
        Assert.True(c.Failed);
    }
    [Theory, MemberData(nameof(Phases))]
    public async Task Every_work_failure_is_terminal_with_only_entered_restoration_allowed(int value)
    {
        var fail = (SqlTestSupervisionPhase)value;
        var calls = new List<SqlTestSupervisionPhase>(); var c = new SqlTestSupervisedCoordinator(() => { });
        var error = await Assert.ThrowsAsync<SqlTestSupervisionException>(() => Run(c, p => { calls.Add(p); if (p == fail) throw new Exception("secret C:\\private token=raw"); return Task.CompletedTask; }));
        Assert.True(c.Failed); Assert.Null(error.InnerException); Assert.DoesNotContain("private", error.ToString()); Assert.DoesNotContain("token", error.ToString());
        var allowed = Enum.GetValues(typeof(SqlTestSupervisionPhase)).Cast<SqlTestSupervisionPhase>().TakeWhile(p => p != fail).Concat(new[] { fail }).ToList();
        if (fail == SqlTestSupervisionPhase.ProbeMutation || fail == SqlTestSupervisionPhase.ProbeRefusal) allowed.Add(SqlTestSupervisionPhase.Restoration);
        Assert.Equal(allowed, calls);
        Assert.Equal(fail >= SqlTestSupervisionPhase.Create,c.CreationMayHaveOccurred);
        var later = 0; await Assert.ThrowsAsync<SqlTestSupervisionException>(() => c.AdvanceAsync(SqlTestSupervisionPhase.Drop, () => { later++; return Task.CompletedTask; }, default)); Assert.Equal(0, later);
    }
    [Theory, MemberData(nameof(Phases))]
    public async Task Swallowed_reentrant_refusal_in_verifier_never_reaches_phase_work(int value)
    {
        var fail = (SqlTestSupervisionPhase)value;
        SqlTestSupervisedCoordinator? c = null; var current = SqlTestSupervisionPhase.MasterBinding; var injected = false; var calls = new List<SqlTestSupervisionPhase>();
        c = new SqlTestSupervisedCoordinator(() => {
            if (!injected && current == fail) { injected = true; try { c!.AdvanceAsync(SqlTestSupervisionPhase.MasterBinding, () => Task.CompletedTask, default).GetAwaiter().GetResult(); } catch (SqlTestSupervisionException) { } }
        }, p => current = p);
        await Assert.ThrowsAsync<SqlTestSupervisionException>(() => Run(c, p => { calls.Add(p); return Task.CompletedTask; }));
        Assert.True(c.Failed); Assert.DoesNotContain(fail, calls);
        Assert.DoesNotContain(SqlTestSupervisionPhase.Drop, calls.Where(p => p > fail));
    }
    [Fact]
    public async Task Cancellation_before_create_never_calls_create()
    {
        var c = new SqlTestSupervisedCoordinator(() => { }); var count = 0;
        using var stop = new CancellationTokenSource(); stop.Cancel();
        await Assert.ThrowsAsync<SqlTestSupervisionException>(() => c.AdvanceAsync(SqlTestSupervisionPhase.MasterBinding, () => { count++; return Task.CompletedTask; }, stop.Token));
        Assert.Equal(0, count); Assert.False(c.CreationMayHaveOccurred); Assert.True(c.Failed);
    }
    [Fact]
    public async Task Diagnostic_failure_precedes_work_and_is_sanitized()
    {
        var calls = 0; var c = new SqlTestSupervisedCoordinator(() => { }, _ => throw new Exception("secret"));
        await Assert.ThrowsAsync<SqlTestSupervisionException>(() => c.AdvanceAsync(SqlTestSupervisionPhase.MasterBinding, () => { calls++; return Task.CompletedTask; }, default));
        Assert.True(c.Failed); Assert.Equal(0, calls);
    }
    [Fact]
    public async Task Nested_probe_refusal_cannot_restore_before_original_mutation_drains()
    {
        var c=new SqlTestSupervisedCoordinator(()=>{}); var early=0; var restored=0; var inside=false;
        foreach(var p in Enum.GetValues(typeof(SqlTestSupervisionPhase)).Cast<SqlTestSupervisionPhase>().TakeWhile(p=>p!=SqlTestSupervisionPhase.ProbeMutation)) await c.AdvanceAsync(p,()=>Task.CompletedTask,default);
        await Assert.ThrowsAsync<SqlTestSupervisionException>(()=>c.ProbeAsync(async ()=>{
            inside=true;
            try { await c.ProbeAsync(()=>Task.CompletedTask,()=>Task.CompletedTask,()=>{ early++; return Task.CompletedTask; },default); } catch(SqlTestSupervisionException) { }
            inside=false;
        },()=>Task.CompletedTask,()=>{ if(inside) early++; restored++; return Task.CompletedTask; },default));
        Assert.Equal(0,early); Assert.Equal(1,restored); Assert.True(c.Failed);
    }
    [Theory, MemberData(nameof(Phases))]
    public async Task Cancellation_at_each_admission_prevents_work_except_already_entered_restoration(int value)
    {
        var fail=(SqlTestSupervisionPhase)value;var current=SqlTestSupervisionPhase.MasterBinding;var calls=new List<SqlTestSupervisionPhase>();
        using var stop=new CancellationTokenSource();
        var c=new SqlTestSupervisedCoordinator(()=>{if(current==fail)stop.Cancel();},p=>current=p);
        await Assert.ThrowsAsync<SqlTestSupervisionException>(()=>Run(c,p=>{calls.Add(p);return Task.CompletedTask;},stop.Token));
        Assert.True(c.Failed);
        if(fail==SqlTestSupervisionPhase.Restoration) { Assert.Contains(fail,calls);Assert.DoesNotContain(SqlTestSupervisionPhase.SubjectManifest,calls); }
        else Assert.DoesNotContain(fail,calls);
    }
    [Theory]
    [InlineData(true)][InlineData(false)]
    public async Task Duplicate_or_out_of_order_admission_latches_terminal_before_any_future_mutation(bool duplicate)
    {
        var c=new SqlTestSupervisedCoordinator(()=>{});await c.AdvanceAsync(SqlTestSupervisionPhase.MasterBinding,()=>Task.CompletedTask,default);var calls=0;
        await Assert.ThrowsAsync<SqlTestSupervisionException>(()=>c.AdvanceAsync(duplicate?SqlTestSupervisionPhase.MasterBinding:SqlTestSupervisionPhase.Create,()=>{calls++;return Task.CompletedTask;},default));
        await Assert.ThrowsAsync<SqlTestSupervisionException>(()=>c.AdvanceAsync(SqlTestSupervisionPhase.Intent,()=>{calls++;return Task.CompletedTask;},default));
        Assert.True(c.Failed);Assert.False(c.CreationMayHaveOccurred);Assert.Equal(0,calls);
    }
    [Fact]
    public async Task Direct_probe_phase_cannot_bypass_the_owner_of_restoration()
    {
        var c=new SqlTestSupervisedCoordinator(()=>{});var calls=0;
        foreach(var p in Enum.GetValues(typeof(SqlTestSupervisionPhase)).Cast<SqlTestSupervisionPhase>().TakeWhile(p=>p!=SqlTestSupervisionPhase.ProbeMutation))await c.AdvanceAsync(p,()=>Task.CompletedTask,default);
        await Assert.ThrowsAsync<SqlTestSupervisionException>(()=>c.AdvanceAsync(SqlTestSupervisionPhase.ProbeMutation,()=>{calls++;return Task.CompletedTask;},default));
        Assert.Equal(0,calls);Assert.True(c.Failed);
    }
    [Fact]
    public void Record_writer_admission_outside_active_work_is_terminal()
    {
        var c=new SqlTestSupervisedCoordinator(()=>{});
        Assert.Throws<SqlTestSupervisionException>(()=>c.RequireActiveWork(SqlTestSupervisionPhase.CompletionReceipt));Assert.True(c.Failed);
    }
    [Fact]
    public async Task Verifier_cannot_impersonate_an_admitted_record_writer()
    {
        SqlTestSupervisedCoordinator? c=null;var calls=0;
        c=new SqlTestSupervisedCoordinator(()=>{try{c!.RequireActiveWork(SqlTestSupervisionPhase.MasterBinding);}catch(SqlTestSupervisionException){}});
        await Assert.ThrowsAsync<SqlTestSupervisionException>(()=>c.AdvanceAsync(SqlTestSupervisionPhase.MasterBinding,()=>{calls++;return Task.CompletedTask;},default));
        Assert.Equal(0,calls);Assert.True(c.Failed);
    }
    [Fact]
    public async Task Only_current_admitted_work_can_pass_the_actual_record_writer_gate()
    {
        var c=new SqlTestSupervisedCoordinator(()=>{});
        await c.AdvanceAsync(SqlTestSupervisionPhase.MasterBinding,()=>{c.RequireActiveWork(SqlTestSupervisionPhase.MasterBinding);return Task.CompletedTask;},default);
        await Assert.ThrowsAsync<SqlTestSupervisionException>(()=>c.AdvanceAsync(SqlTestSupervisionPhase.Intent,()=>{c.RequireActiveWork(SqlTestSupervisionPhase.CompletionReceipt);return Task.CompletedTask;},default));
        Assert.True(c.Failed);
    }
    private static SqlTestSupervisionBinding Binding(string run = "a", string source = "b") => new SqlTestSupervisionBinding(new string(run[0], 64), new string(source[0], 64), SqlTestSupervisionBinding.SelectionHash, new string('c',64), new string('d',64));
    private static SqlTestSupervisionDelivery Delivery => new SqlTestSupervisionDelivery(new string('e',64),new string('f',64));
    [Fact]
    public void Actual_private_protocol_round_trips_and_completion_contains_no_private_identity()
    {
        var b = Binding(); var intent = SqlTestSupervisionProtocol.Intent(b, "MyDmsVn_Test_11111111111141118111111111111111", "private-server", "22222222222242228222222222222222",Delivery);
        var original = SqlTestSupervisionProtocol.Original(b, intent, 17, new DateTime(2026,11,9),Delivery);
        var done = SqlTestSupervisionProtocol.Completion(b, intent, original,Delivery);
        SqlTestSupervisionProtocol.Verify(new[] { intent, original, done }, b, true);
        var text = Encoding.UTF8.GetString(done); Assert.DoesNotContain("private-server", text); Assert.DoesNotContain("database=", text); Assert.DoesNotContain("ownershipToken=", text);
    }
    [Theory]
    [InlineData("missing")][InlineData("duplicate")][InlineData("order")][InlineData("run")][InlineData("source")][InlineData("rootIdentity")][InlineData("rootAcl")][InlineData("tamper")][InlineData("secret")]
    public void Actual_protocol_refuses_partial_duplicate_order_binding_and_tampered_records(string kind)
    {
        var b=Binding(); var one=SqlTestSupervisionProtocol.Intent(b,"MyDmsVn_Test_11111111111141118111111111111111","private-server","22222222222242228222222222222222",Delivery);
        var two=SqlTestSupervisionProtocol.Original(b,one,17,new DateTime(2026,11,9),Delivery); var three=SqlTestSupervisionProtocol.Completion(b,one,two,Delivery);
        var records=new[]{one,two,three};
        if(kind=="missing") records=new[]{one,two}; if(kind=="duplicate") records=new[]{one,one,three}; if(kind=="order") records=new[]{two,one,three};
        if(kind=="run") b=Binding("e"); if(kind=="source") b=Binding(source:"e");
        if(kind=="rootIdentity") b=new SqlTestSupervisionBinding(b.Run,b.Source,b.SelectionValue,new string('a',64),b.RootAcl);
        if(kind=="rootAcl") b=new SqlTestSupervisionBinding(b.Run,b.Source,b.SelectionValue,b.RootIdentity,new string('a',64));
        if(kind=="tamper") records[1]=(byte[])two.Clone(); if(kind=="tamper") records[1][20]^=1;
        if(kind=="secret") records[2]=SqlTestPrivateRecord.Seal(SqlTestPrivateRecord.Validate(three)+"secret=raw\n");
        Assert.Throws<SqlTestSupervisionException>(()=>SqlTestSupervisionProtocol.Verify(records,b,true));
    }
    [Theory]
    [InlineData("identity")][InlineData("acl")][InlineData("unprotected")][InlineData("reparse")]
    public void Original_native_delivery_observation_cannot_be_replaced(string kind) =>
        Assert.Throws<SqlTestSupervisionException>(()=>Delivery.Verify(kind=="identity"?new string('a',64):Delivery.Identity,kind=="acl"?new string('a',64):Delivery.Acl,kind!="unprotected",kind=="reparse"));
    [Fact]
    public void Configuration_is_inert_when_absent_and_refuses_partial_or_wrong_selection_before_IO()
    {
        Assert.Null(SqlTestSupervisionConfiguration.Read(_=>null));
        Assert.Throws<SqlTestSupervisionException>(()=>SqlTestSupervisionConfiguration.Read(key=>key.EndsWith("ROOT",StringComparison.Ordinal)?"C:\\outside":null));
        Assert.Throws<SqlTestSupervisionException>(()=>new SqlTestSupervisionBinding(new string('a',64),new string('b',64),new string('f',64),new string('c',64),new string('d',64)));
    }
    [Theory]
    [InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)]
    public void Actual_record_delivery_stops_at_each_injected_native_IO_failure(int fail)
    {
        var calls=new List<int>(); var bytes=Encoding.UTF8.GetBytes("private witness");
        void Step(int phase) { calls.Add(phase); if(phase==fail) throw new System.IO.IOException("secret"); }
        Assert.Throws<SqlTestSupervisionException>(()=>SqlTestSupervisionRecordDelivery.Deliver(
            ()=>{Step(0);return Delivery;},d=>{Step(1);return bytes;},_=>Step(2),()=>Step(3),()=>{Step(4);return bytes;},_=>Step(5)));
        Assert.Equal(Enumerable.Range(0,fail+1),calls);
    }
    [Fact]
    public void Actual_record_delivery_requires_exact_readback_before_seal_verification()
    {
        var verified=0;
        Assert.Throws<SqlTestSupervisionException>(()=>SqlTestSupervisionRecordDelivery.Deliver(()=>Delivery,_=>new byte[]{1},_=>{},()=>{},()=>new byte[]{2},_=>verified++));
        Assert.Equal(0,verified);
        Assert.Equal(new byte[]{1},SqlTestSupervisionRecordDelivery.Deliver(()=>Delivery,_=>new byte[]{1},_=>{},()=>{},()=>new byte[]{1},_=>verified++));
        Assert.Equal(1,verified);
    }
}
