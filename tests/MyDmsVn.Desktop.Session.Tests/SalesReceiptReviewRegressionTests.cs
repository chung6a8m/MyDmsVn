using System;
using System.Linq;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed class SalesReceiptReviewRegressionTests
{
    [Fact]
    public async Task R2_Post_follow_on_attempt_at_replacement_boundary_never_admits_new_session_Get()
    {
        var session = new SalesSession(); var api = new SalesApi { Owner = session };
        using var vm = Editor(api, session, new SalesBuffers()); await vm.LoadAsync(41);
        api.BeforeGetAdmission = () => { api.BeforeGetAdmission = null; session.Replace(); };
        await vm.PostAsync();
        Assert.Equal(2, api.GetAttempts); Assert.Single(api.Gets); Assert.Equal(0, vm.SaleId); Assert.Empty(vm.Lines);
    }

    [Fact]
    public async Task R2_Empty_later_page_recovery_attempt_never_admits_new_session_List()
    {
        var session = new SalesSession(); var api = new SalesApi { Owner = session };
        using var vm = new SalesReceiptListViewModel(api, session, new ImmediateUiDispatcher()); await vm.RefreshAsync();
        api.List = request => Task.FromResult(SalesApi.Ok(SalesApi.Page(0)));
        api.BeforeListAdmission = () => { if (api.ListAttempts == 3) { api.BeforeListAdmission = null; session.Replace(); } };
        await vm.NextAsync();
        Assert.Equal(3, api.ListAttempts); Assert.Equal(2, api.Lists.Count); Assert.Empty(vm.Items); Assert.Null(vm.AppliedCriteria);
    }

    [Fact]
    public async Task R1_Read_and_Post_without_Write_posts_saved_clean_draft_but_cannot_edit_or_save()
    {
        var api = new SalesApi(); var session = new SalesSession(); var buffers = new SalesBuffers();
        using var vm = Editor(api, session, buffers);
        await vm.LoadAsync(41); vm.SetAccess(true, false, true);
        var row = vm.Lines.Single(); row.Quantity = 99; row.UnitPrice = 99; row.IsGift = true; row.ProductId = 99;
        vm.Note = "denied"; vm.CustomerId = 99; vm.WarehouseId = 99; vm.EmployeeId = 99; vm.SaleDate = DateTime.MinValue;
        Assert.False(vm.CanEdit); Assert.Equal(2m, row.Quantity); Assert.Equal(4m, row.UnitPrice); Assert.False(row.IsGift); Assert.Equal(7, row.ProductId);
        Assert.Equal("note", vm.Note); Assert.Equal(1, vm.CustomerId); Assert.Equal(2, vm.WarehouseId); Assert.Equal(3, vm.EmployeeId);
        Assert.False(vm.RemoveLine(row)); Assert.False(vm.MoveLine(row, 0)); Assert.Null(vm.AddLine());
        await vm.SaveAsync(); Assert.Empty(api.Updates); Assert.Empty(api.Creates);
        var admitted = 0; buffers.Commit = intent => { Assert.Equal(SalesCommandIntent.Post, intent); admitted++; return Task.FromResult(true); };
        api.Get = id => Task.FromResult(SalesApi.Ok(SalesTestData.Receipt(id, true)));
        await vm.PostAsync();
        Assert.Single(api.Posts); Assert.Equal(1, admitted); Assert.Equal(new long[] { 41, 41 }, api.Gets);
        Assert.Equal("Posted", vm.Status); Assert.Equal(5m, vm.TotalCost); Assert.False(vm.CanEdit);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task R3_Forbidden_outcome_read_scrubs_presentation_then_restored_Read_recovers_exact_original_id(bool knownPost)
    {
        var api = new SalesApi(); var session = new SalesSession(); using var vm = Editor(api, session, new SalesBuffers());
        await vm.LoadAsync(41);
        if (!knownPost) api.Post = _ => throw new InvalidOperationException("ambiguous post");
        api.Get = _ => Task.FromResult(SalesApi.Error<SalesReceiptDto>(ApiStatusCode.Forbidden, "Auth.Forbidden"));
        await vm.PostAsync();
        if (!knownPost) await vm.ReloadOutcomeAsync();
        Assert.True(vm.RequiresPostOutcomeReload); Assert.True(vm.IsAccessDenied);
        Assert.Equal(0, vm.SaleId); Assert.Equal("", vm.Version); Assert.Empty(vm.Lines); Assert.Null(vm.Note);
        Assert.Null(vm.TotalCost); Assert.Null(vm.GrossProfit); Assert.False(vm.NewDraft());
        var before = api.Gets.Count; await vm.LoadAsync(99); Assert.Equal(before, api.Gets.Count);
        vm.SetAccess(true, false, false);
        await vm.LoadAsync(99); await vm.ResolveCreateOutcomeAsync(99); Assert.Equal(before, api.Gets.Count); Assert.False(vm.NewDraft());
        if (knownPost)
        {
            api.Get = id => Task.FromResult(SalesApi.Ok(SalesTestData.Receipt(id)));
            await vm.ReloadOutcomeAsync(); Assert.True(vm.RequiresPostOutcomeReload); Assert.Equal(0, vm.SaleId); Assert.Null(vm.TotalCost);
            before = api.Gets.Count;
        }
        api.Get = id => Task.FromResult(SalesApi.Ok(SalesTestData.Receipt(id, true)));
        await vm.ReloadOutcomeAsync();
        Assert.Equal(before + 1, api.Gets.Count); Assert.Equal(41, api.Gets.Last());
        Assert.False(vm.RequiresPostOutcomeReload); Assert.Equal(41, vm.SaleId); Assert.Equal("Posted", vm.Status);
        Assert.Equal(5m, vm.TotalCost); Assert.Single(api.Posts);
    }

    [Fact]
    public async Task R3_Denied_recovery_identity_never_follows_session_replacement()
    {
        var api = new SalesApi(); var session = new SalesSession(); using var vm = Editor(api, session, new SalesBuffers());
        await vm.LoadAsync(41); api.Post = _ => throw new InvalidOperationException("ambiguous"); await vm.PostAsync();
        api.Get = _ => Task.FromResult(SalesApi.Error<SalesReceiptDto>(ApiStatusCode.Forbidden)); await vm.ReloadOutcomeAsync();
        var before = api.Gets.Count; session.Replace(); vm.SetAccess(true, true, true); await vm.ReloadOutcomeAsync(); await vm.LoadAsync(41);
        Assert.Equal(before, api.Gets.Count); Assert.Equal(0, vm.SaleId); Assert.Empty(vm.Lines); Assert.False(vm.CanEdit);
    }

    internal static SalesReceiptEditorViewModel Editor(SalesApi api, SalesSession session, SalesBuffers buffers, IUiDispatcher? ui = null)
    {
        var vm = new SalesReceiptEditorViewModel(api, session, ui ?? new ImmediateUiDispatcher(), SalesApi.Calculator(), buffers);
        vm.SetAccess(true, true, true); return vm;
    }
}
