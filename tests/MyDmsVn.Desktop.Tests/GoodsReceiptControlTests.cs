using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.Messaging;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.WinForms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;
using MyDmsVn.Bootstrap5WinFormUI.Theme;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed partial class GoodsReceiptControlTests
    {
        internal static string MeasureGoodsEditingPerformance()
        {
            using var fixture = new Fixture();
            using var control = new GoodsReceiptControl(fixture.List, fixture.Editor);
            using var form = new Form { Width = 1000, Height = 650 };
            form.Controls.Add(control); form.Show(); System.Windows.Forms.Application.DoEvents();
            fixture.Api.CurrentReceipt = ReceiptApiClient.Receipt(status: "Draft");
            control.ActivateAsync(default).GetAwaiter().GetResult();
            control.SelectReceiptAsync(41, default).GetAwaiter().GetResult();
            while (fixture.Editor.Lines.Count < 25) fixture.Editor.AddLine();
            var line = fixture.Editor.Lines[12];
            var grid = (GoodsReceiptLinesGrid)control.LineGrid;
            var adapter = (GoodsReceiptLinesGridAdapter)typeof(GoodsReceiptControl)
                .GetField("_inline", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(control)!;
            var refresh = adapter.GetType().GetMethod("RefreshProjection", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var events = 0; var invalidations = 0;
            fixture.Editor.PropertyChanged += (_, __) => events++;
            grid.Invalidated += (_, __) => invalidations++;
            var edits = new List<double>(); var editBytes = new List<double>();
            var projections = new List<double>(); var projectionBytes = new List<double>();
            for (var repetition = 0; repetition < 14; ++repetition)
            {
                var before = GC.GetAllocatedBytesForCurrentThread();
                var watch = System.Diagnostics.Stopwatch.StartNew();
                adapter.SetAccessibleValue(adapter.AccessibleGeneration, line, 2, repetition % 2 == 0 ? "2.5" : "3.5");
                watch.Stop();
                if (repetition >= 2) { edits.Add(watch.Elapsed.TotalMilliseconds); editBytes.Add(GC.GetAllocatedBytesForCurrentThread() - before); }
                before = GC.GetAllocatedBytesForCurrentThread(); watch.Restart();
                refresh.Invoke(adapter, null);
                watch.Stop();
                if (repetition >= 2) { projections.Add(watch.Elapsed.TotalMilliseconds); projectionBytes.Add(GC.GetAllocatedBytesForCurrentThread() - before); }
            }
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            string Distribution(List<double> values)
            {
                var sorted = values.OrderBy(value => value).ToArray();
                return "p50=" + sorted[(int)Math.Ceiling(sorted.Length * .5) - 1].ToString("F2") +
                    ",p95=" + sorted[(int)Math.Ceiling(sorted.Length * .95) - 1].ToString("F2");
            }
            int CountControls(Control parent) => 1 + parent.Controls.Cast<Control>().Sum(CountControls);
            return "Goods lines=25; edit/commit=" + Distribution(edits) + "; bytes=" + Distribution(editBytes) +
                "; projection=" + Distribution(projections) + "; projectionBytes=" + Distribution(projectionBytes) +
                "; editorEvents=" + events + "; gridInvalidations=" + invalidations + "; retainedControls=" + CountControls(form);
        }
        [Theory]
        [InlineData(BootstrapThemeMode.Light)]
        [InlineData(BootstrapThemeMode.Dark)]
        public void Numeric_paste_command_applies_goods_quantity_and_cost_without_write_api_call(BootstrapThemeMode mode)
        {
            StaTest.Run(token =>
            {
                var original = BootstrapThemeManager.CurrentTheme;
                try
                {
                    BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(mode);
                    using var guard = new WinFormsTestGuard(); using var fixture = new Fixture();
                    using var control = new GoodsReceiptControl(fixture.List, fixture.Editor);
                    using var form = new Form { Width = 1000, Height = 650 };
                    form.Controls.Add(control); form.Show(); System.Windows.Forms.Application.DoEvents();
                    fixture.Api.CurrentReceipt = ReceiptApiClient.Receipt(status: "Draft");
                    control.ActivateAsync(token).GetAwaiter().GetResult();
                    control.SelectReceiptAsync(41, token).GetAwaiter().GetResult();
                    var grid = (GoodsReceiptLinesGrid)control.LineGrid;
                    var adapter = (GoodsReceiptLinesGridAdapter)typeof(GoodsReceiptControl)
                        .GetField("_inline", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(control)!;
                    grid.Selection.ResetSelection(false);
                    grid.Selection.SelectRange(new SourceGrid.Range(new SourceGrid.Position(1, 2), new SourceGrid.Position(1, 3)), true);
                    Assert.True(adapter.TryPasteNumeric("2\t7"), adapter.EditError);
                    Assert.Equal(2m, fixture.Editor.Lines[0].Quantity);
                    Assert.Equal(7m, fixture.Editor.Lines[0].UnitCost);
                    Assert.Equal(0, fixture.Api.CreateCalls);
                    Assert.Null(fixture.Api.LastUpdate);
                    Assert.Null(fixture.Api.LastPost);
                    var revision = fixture.Editor.EditRevision;
                    grid.Selection.ResetSelection(false);
                    grid.Selection.SelectCell(new SourceGrid.Position(1, 4), true); // Read-only total.
                    Assert.False(adapter.TryPasteNumeric("9"));
                    Assert.Equal(revision, fixture.Editor.EditRevision);
                    Assert.Equal(7m, fixture.Editor.Lines[0].UnitCost);
                    Console.WriteLine(UiNextNativeScaleObservation.Observe(form, token));
                    Assert.Empty(guard.Exceptions);
                }
                finally { BootstrapThemeManager.CurrentTheme = original; }
            }, TimeSpan.FromSeconds(15));
        }
        [Fact]
        public void Factory_goods_query_failure_is_visible_in_its_own_workspace()
        {
            StaTest.Run(token =>
            {
                using var guard = new WinFormsTestGuard(); using var fixture = new Fixture();
                var globalNotices = 0;
                fixture.Notifications.NotificationPublished += (_, __) => globalNotices++;
                var factory = new GoodsReceiptControlFactory(fixture.Api,
                    new WarehouseCatalogClient(fixture.WarehouseSource),
                    new EmployeeCatalogClient(fixture.EmployeeSource),
                    new ProductCatalogClient(fixture.ProductSource), fixture.Messenger,
                    new ImmediateUiDispatcher(), fixture.Notifications,
                    new InventoryControlTests.SilentReporter(), new ImmediateDelay());
                var control = Assert.IsType<GoodsReceiptControl>(factory.Create());
                using var host = new WorkspaceFeedbackHost(control, control.FeedbackScope);
                using var form = new Form { Width = 900, Height = 600 };
                form.Controls.Add(host); form.Show(); System.Windows.Forms.Application.DoEvents();
                fixture.Api.ListError = new ApiError(ApiStatusCode.Forbidden, "Auth.Forbidden", "Goods denied.");

                control.ActivateAsync(token).GetAwaiter().GetResult();
                System.Windows.Forms.Application.DoEvents();

                Assert.True(host.Alert.Visible);
                Assert.Contains("Support ID:", host.Alert.Text);
                Assert.Equal(BootstrapTextRole.Danger, Assert.IsType<BootstrapLabel>(control.OperationStatusLabel).TextRole);
                Assert.Equal(0, globalNotices);
                Assert.Empty(guard.Exceptions);
            }, TimeSpan.FromSeconds(20));
        }

        [Fact]
        public void Close_state_detects_dirty_draft_and_clean_posted_receipt()
        {
            StaTest.Run(token =>
            {
                using var guard = new WinFormsTestGuard(); using var fixture = new Fixture();
                using var control = new GoodsReceiptControl(fixture.List, fixture.Editor);
                Assert.Equal(BootstrapSurfaceRole.Surface,
                    Assert.IsType<BootstrapPanel>(Assert.Single(control.Controls.Find("GoodsDetailSurface", true))).SurfaceRole);
                Assert.Equal(BootstrapTextRole.Muted, Assert.IsType<BootstrapLabel>(control.PageLabel).TextRole);
                fixture.Api.CurrentReceipt = ReceiptApiClient.Receipt(status: "Draft");
                control.ActivateAsync(token).GetAwaiter().GetResult();
                control.SelectReceiptAsync(41, token).GetAwaiter().GetResult();
                Assert.False(control.CaptureCloseState().IsDirty);
                fixture.Editor.Note = "unsaved";
                Assert.True(control.CaptureCloseState().IsDirty);
                Assert.True(control.CaptureCloseState().CanSave);
                fixture.Editor.Load(ReceiptApiClient.Receipt(status: "Posted"));
                Assert.False(control.CaptureCloseState().IsDirty);
                Assert.False(control.CaptureCloseState().CanSave);
                Assert.Empty(guard.Exceptions);
            }, TimeSpan.FromSeconds(15));
        }

        [Fact]
        public void Save_for_close_returns_goods_write_and_current_revision()
        {
            StaTest.Run(token =>
            {
                using var guard = new WinFormsTestGuard(); using var fixture = new Fixture();
                using var control = new GoodsReceiptControl(fixture.List, fixture.Editor);
                fixture.Api.CurrentReceipt = ReceiptApiClient.Receipt(status: "Draft");
                control.ActivateAsync(token).GetAwaiter().GetResult();
                control.SelectReceiptAsync(41, token).GetAwaiter().GetResult();
                fixture.Editor.Note = "unsaved";
                var outcome = control.SaveForCloseAsync(token).GetAwaiter().GetResult();
                Assert.Equal(WorkspaceSaveStatus.Saved, outcome.Status);
                Assert.Equal(control.CaptureCloseState().Revision, outcome.SubmittedRevision);
                Assert.False(control.CaptureCloseState().IsDirty);
                Assert.Empty(guard.Exceptions);
            }, TimeSpan.FromSeconds(15));
        }

        [Fact]
        public void Newer_goods_edit_during_close_save_keeps_receipt_dirty_and_revision_mismatched()
        {
            StaTest.Run(token =>
            {
                using var guard = new WinFormsTestGuard(); using var fixture = new Fixture();
                using var control = new GoodsReceiptControl(fixture.List, fixture.Editor);
                control.ActivateAsync(token).GetAwaiter().GetResult();
                control.NewButton.PerformClick();
                fixture.Editor.SetWarehouse(3, "W3 - Main", true);
                fixture.Editor.SetEmployee(5, "E5 - Operator", true);
                var line = fixture.Editor.AddLine();
                line.SetProduct(7, "P7 - Product 7", true);
                line.Quantity = 1m; line.UnitCost = 2m;
                fixture.Editor.Note = "submitted";
                fixture.Api.ControlCreateResponse = true;

                var pending = control.SaveForCloseAsync(token);
                Assert.Equal(1, fixture.Api.CreateCalls);
                fixture.Editor.Note = "newer edit";
                fixture.Api.CompleteCreate();
                PumpMessagesUntil(() => pending.IsCompleted, TimeSpan.FromSeconds(2));
                var outcome = pending.GetAwaiter().GetResult();

                Assert.Equal(WorkspaceSaveStatus.Saved, outcome.Status);
                Assert.True(outcome.RefreshFailed);
                Assert.NotEqual(control.CaptureCloseState().Revision, outcome.SubmittedRevision);
                Assert.True(control.CaptureCloseState().IsDirty);
                Assert.Equal("newer edit", fixture.Editor.Note);
                Assert.Empty(guard.Exceptions);
            }, TimeSpan.FromSeconds(15));
        }

        [Fact]
        public void Activation_and_selection_render_master_detail_lookups_total_and_read_only_state()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    using (var guard = new WinFormsTestGuard())
                    using (var fixture = new Fixture())
                    using (var control = new GoodsReceiptControl(fixture.List, fixture.Editor))
                    {
                        control.ActivateAsync(cancellationToken).GetAwaiter().GetResult();
                        control.SelectReceiptAsync(41, cancellationToken).GetAwaiter().GetResult();

                        Assert.Equal(2, control.ReceiptGrid.RowsCount);
                        Assert.Equal("GR0000000041", control.ReceiptGrid[1, 1].Value);
                        Assert.Equal(2, control.LineGrid.RowsCount);
                        Assert.Equal(7, control.LineGrid[1, 1].Value);
                        Assert.Equal("P7 - Product 7", fixture.Editor.Lines[0].ProductDisplayName);
                        Assert.Equal("10.00", control.TotalLabel.Text);
                        Assert.Equal("Posted", control.StatusLabel.Text);
                        Assert.Equal("AQIDBAUGBwg=", control.VersionLabel.Text);
                        Assert.Single(
                            Assert.IsAssignableFrom<IEnumerable<CatalogLookupOption>>(
                                control.WarehouseLookup.DataSource));
                        Assert.Single(
                            Assert.IsAssignableFrom<IEnumerable<CatalogLookupOption>>(
                                control.ProductLookup.DataSource));
                        Assert.False(control.SaveButton.Enabled);
                        Assert.False(control.AddLineButton.Enabled);
                        Assert.Empty(guard.Exceptions);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Draft_post_button_posts_current_version_and_locks_the_editor()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    using (var guard = new WinFormsTestGuard())
                    using (var fixture = new Fixture())
                    using (var control = new GoodsReceiptControl(fixture.List, fixture.Editor))
                    {
                        fixture.Api.CurrentReceipt = ReceiptApiClient.Receipt(status: "Draft");
                        control.ActivateAsync(cancellationToken).GetAwaiter().GetResult();
                        control.SelectReceiptAsync(41, cancellationToken).GetAwaiter().GetResult();

                        Assert.True(control.PostButton.Enabled);
                        control.PostButton.PerformClick();
                        control.LastOperation.GetAwaiter().GetResult();

                        Assert.NotNull(fixture.Api.LastPost);
                        Assert.Equal(41, fixture.Api.LastPost!.ReceiptId);
                        Assert.Equal("AQIDBAUGBwg=", fixture.Api.LastPost.ExpectedVersion);
                        Assert.Equal("Posted", fixture.Editor.Status);
                        Assert.False(control.PostButton.Enabled);
                        Assert.False(control.SaveButton.Enabled);
                        Assert.NotNull(fixture.Notifications.LastNotification);
                        Assert.Equal(
                            "Goods receipt posted.",
                            fixture.Notifications.LastNotification!.Message);
                        Assert.Empty(guard.Exceptions);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Draft_commands_update_lines_and_validation_errors_stay_inline()
        {
            StaTest.Run(
                _ =>
                {
                    using (var guard = new WinFormsTestGuard())
                    using (var fixture = new Fixture())
                    using (var control = new GoodsReceiptControl(fixture.List, fixture.Editor))
                    {
                        control.NewButton.PerformClick();
                        control.AddLineButton.PerformClick();
                        Assert.Single(fixture.Editor.Lines);
                        Assert.Equal(2, control.LineGrid.RowsCount);
                        control.RemoveLineButton.PerformClick();
                        Assert.Empty(fixture.Editor.Lines);
                        control.AddLineButton.PerformClick();

                        control.SaveButton.PerformClick();
                        control.LastOperation.GetAwaiter().GetResult();

                        Assert.Equal("Warehouse is required.", control.GetFieldError("warehouseId"));
                        Assert.Equal("Employee is required.", control.GetFieldError("employeeId"));
                        Assert.Equal("Product is required.", control.GetFieldError("lines[0].productId"));
                        Assert.Empty(guard.Exceptions);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Draft_decimal_values_round_trip_at_the_contract_precision_and_range()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    using (var fixture = new Fixture())
                    using (var control = new GoodsReceiptControl(fixture.List, fixture.Editor))
                    {
                        fixture.Api.CurrentReceipt = ReceiptApiClient.Receipt(
                            status: "Draft",
                            quantity: 1.2345m,
                            unitCost: 0.1234m);

                        control.ActivateAsync(cancellationToken).GetAwaiter().GetResult();
                        control.SelectReceiptAsync(41, cancellationToken).GetAwaiter().GetResult();

                        Assert.Equal(1.2345m, InlineReceiptTestDriver.Value(control, 2));
                        Assert.Equal(0.1234m, InlineReceiptTestDriver.Value(control, 3));

                        control.SaveButton.PerformClick();
                        control.LastOperation.GetAwaiter().GetResult();

                        Assert.NotNull(fixture.Api.LastUpdate);
                        Assert.Equal(1.2345m, fixture.Api.LastUpdate!.Lines[0].Quantity);
                        Assert.Equal(0.1234m, fixture.Api.LastUpdate.Lines[0].UnitCost);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Theory]
        [InlineData(BootstrapThemeMode.Light)]
        [InlineData(BootstrapThemeMode.Dark)]
        public void In_flight_activation_disables_commands_until_the_list_finishes(BootstrapThemeMode mode)
        {
            StaTest.Run(
                cancellationToken =>
                {
                    var original = BootstrapThemeManager.CurrentTheme;
                    try
                    {
                        BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(mode);
                        using (var fixture = new Fixture())
                        using (var control = new GoodsReceiptControl(fixture.List, fixture.Editor))
                        using (var form = new Form { ClientSize = new System.Drawing.Size(1000, 700) })
                        {
                            form.Controls.Add(control);
                            form.Show();
                            fixture.Api.ControlListResponse = true;

                            var activation = control.ActivateAsync(cancellationToken);

                            var split = Assert.Single(control.Controls.OfType<SplitContainer>());
                            var busyHost = Assert.Single(split.Panel1.Controls.OfType<BootstrapBusyHost>());
                            Assert.Contains(control.ReceiptGrid, busyHost.Content.Controls.Cast<Control>());
                            var overlay = Assert.Single(busyHost.Controls.OfType<Panel>(),
                                item => !ReferenceEquals(item, busyHost.Content));
                            Console.WriteLine(UiNextNativeScaleObservation.Observe(form, cancellationToken));
                            PumpMessagesUntil(() => overlay.Visible, TimeSpan.FromSeconds(2));
                            Assert.True(overlay.Visible);
                            Assert.False(busyHost.Contains(control.RefreshButton));
                            Assert.False(control.RefreshButton.Enabled);
                            Assert.False(control.ReceiptGrid.Enabled);
                            Assert.True(control.BusyIndicator.Visible);
                            fixture.Api.CompleteList();
                            PumpMessagesUntil(() => activation.IsCompleted, TimeSpan.FromSeconds(2));
                            activation.GetAwaiter().GetResult();
                            Assert.True(control.RefreshButton.Enabled);
                            Assert.False(overlay.Visible);
                            Assert.False(control.BusyIndicator.Visible);
                        }
                    }
                    finally { BootstrapThemeManager.CurrentTheme = original; }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Operation_errors_render_in_the_non_modal_status_area()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    using (var fixture = new Fixture())
                    using (var control = new GoodsReceiptControl(fixture.List, fixture.Editor))
                    {
                        fixture.Api.ListError = new ApiError(
                            ApiStatusCode.Forbidden,
                            "Auth.Forbidden",
                            "Denied.");

                        control.ActivateAsync(cancellationToken).GetAwaiter().GetResult();

                        Assert.Equal("Denied.", control.OperationStatusLabel.Text);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Paging_and_explicit_filters_refresh_immediately()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    using (var fixture = new Fixture())
                    using (var control = new GoodsReceiptControl(fixture.List, fixture.Editor))
                    {
                        control.ActivateAsync(cancellationToken).GetAwaiter().GetResult();
                        Assert.Equal("Page 1 of 2", control.PageLabel.Text);
                        Assert.True(control.NextPageButton.Enabled);

                        control.NextPageButton.PerformClick();
                        control.LastOperation.GetAwaiter().GetResult();
                        Assert.Equal(2, fixture.Api.ListRequests.Last().PageNumber);
                        Assert.Equal("Page 2 of 2", control.PageLabel.Text);

                        control.WarehouseFilterLookup.SelectValue(3);
                        control.LastOperation.GetAwaiter().GetResult();
                        Assert.Equal(3, fixture.Api.ListRequests.Last().WarehouseId);
                        Assert.Equal(1, fixture.Api.ListRequests.Last().PageNumber);

                        control.StatusFilter.SelectedItem = "Posted";
                        control.LastOperation.GetAwaiter().GetResult();
                        Assert.Equal("Posted", fixture.Api.ListRequests.Last().Status);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Lookup_text_searches_the_server_side_catalog_sources()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    using (var fixture = new Fixture())
                    using (var control = new GoodsReceiptControl(fixture.List, fixture.Editor))
                    {
                        fixture.Api.CurrentReceipt = ReceiptApiClient.Receipt(status: "Draft");
                        using var form = new Form { Size = new System.Drawing.Size(1500, 850) };
                        form.Controls.Add(control);
                        form.Show();
                        control.ActivateAsync(cancellationToken).GetAwaiter().GetResult();
                        control.SelectReceiptAsync(41, cancellationToken).GetAwaiter().GetResult();

                        control.WarehouseLookup.Text = "remote warehouse";
                        control.LastOperation.GetAwaiter().GetResult();
                        Assert.Equal("remote warehouse", fixture.WarehouseSource.Requests.Last().Search);
                        Assert.Equal("remote warehouse", control.WarehouseLookup.Text);
                        Assert.True(control.WarehouseLookup.HasPendingText);

                        control.EmployeeLookup.Text = "remote employee";
                        control.LastOperation.GetAwaiter().GetResult();
                        Assert.Equal("remote employee", fixture.EmployeeSource.Requests.Last().Search);
                        Assert.Equal("remote employee", control.EmployeeLookup.Text);
                        Assert.True(control.EmployeeLookup.HasPendingText);

                        BeginInline(control, 1);
                        control.ProductLookup.Text = "remote product";
                        control.LastOperation.GetAwaiter().GetResult();
                        Assert.Equal("remote product", fixture.ProductSource.Requests.Last().Search);
                        Assert.Equal("remote product", control.ProductLookup.Text);
                        Assert.True(control.ProductLookup.HasPendingText);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Explicit_new_discards_uncommitted_lookup_text_in_an_unsaved_empty_draft()
        {
            StaTest.Run(token =>
            {
                using var fixture = new Fixture();
                using var control = new GoodsReceiptControl(fixture.List, fixture.Editor);
                control.ActivateAsync(token).GetAwaiter().GetResult();
                control.WarehouseLookup.Text = "remote warehouse";
                control.LastOperation.GetAwaiter().GetResult();
                Assert.True(control.WarehouseLookup.HasPendingText);
                Assert.Null(fixture.Editor.WarehouseId);
                control.NewButton.PerformClick();
                Assert.Empty(control.WarehouseLookup.Text);
                Assert.False(control.WarehouseLookup.HasPendingText);
                Assert.Null(control.WarehouseLookup.SelectedValue);
            }, TimeSpan.FromSeconds(10));
        }
        [Fact]
        public void Receipt_line_keeps_a_historical_product_selected_outside_the_active_page()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    using (var fixture = new Fixture())
                    using (var control = new GoodsReceiptControl(fixture.List, fixture.Editor))
                    {
                        fixture.Api.CurrentReceipt = ReceiptApiClient.Receipt(
                            status: "Draft",
                            productId: 99,
                            productDisplayName: "P99 - Historical product",
                            productIsActive: false);

                        control.ActivateAsync(cancellationToken).GetAwaiter().GetResult();
                        control.SelectReceiptAsync(41, cancellationToken).GetAwaiter().GetResult();

                        Assert.Equal(99, control.LineGrid[1, 1].Value);
                        Assert.Equal("P99 - Historical product", fixture.Editor.Lines[0].ProductDisplayName);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Failed_selection_does_not_reload_the_previous_receipt_into_a_new_editor()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    using (var fixture = new Fixture())
                    using (var control = new GoodsReceiptControl(fixture.List, fixture.Editor))
                    {
                        control.ActivateAsync(cancellationToken).GetAwaiter().GetResult();
                        control.SelectReceiptAsync(41, cancellationToken).GetAwaiter().GetResult();
                        fixture.Editor.Load(null);
                        fixture.Api.GetError = new ApiError(
                            ApiStatusCode.NotFound,
                            "GoodsReceipt.NotFound",
                            "Missing.");

                        control.SelectReceiptAsync(99, cancellationToken).GetAwaiter().GetResult();

                        Assert.Null(fixture.Editor.Id);
                        Assert.Empty(fixture.Editor.Lines);
                        Assert.Equal("Missing.", control.OperationStatusLabel.Text);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Dirty_editor_blocks_selection_new_and_refresh_without_losing_input()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    using (var fixture = new Fixture())
                    using (var control = new GoodsReceiptControl(fixture.List, fixture.Editor))
                    {
                        control.ActivateAsync(cancellationToken).GetAwaiter().GetResult();
                        control.NewButton.PerformClick();
                        control.NoteEditor.Text = "unsaved draft";
                        var listCalls = fixture.Api.ListCalls;
                        var getCalls = fixture.Api.GetCalls;

                        control.SelectReceiptAsync(41, cancellationToken).GetAwaiter().GetResult();
                        control.NewButton.PerformClick();
                        control.RefreshButton.PerformClick();
                        control.LastOperation.GetAwaiter().GetResult();

                        Assert.Equal("unsaved draft", fixture.Editor.Note);
                        Assert.True(fixture.Editor.IsDirty);
                        Assert.Equal(listCalls, fixture.Api.ListCalls);
                        Assert.Equal(getCalls, fixture.Api.GetCalls);
                        Assert.Equal(
                            "Save the current draft before leaving it.",
                            control.OperationStatusLabel.Text);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Forbidden_refresh_clears_visible_receipt_data()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    using (var fixture = new Fixture())
                    using (var control = new GoodsReceiptControl(fixture.List, fixture.Editor))
                    {
                        control.ActivateAsync(cancellationToken).GetAwaiter().GetResult();
                        control.SelectReceiptAsync(41, cancellationToken).GetAwaiter().GetResult();
                        Assert.Equal(41, fixture.Editor.Id);

                        fixture.Api.ListError = new ApiError(
                            ApiStatusCode.Forbidden,
                            "Auth.Forbidden",
                            "Denied.");
                        control.RefreshButton.PerformClick();
                        control.LastOperation.GetAwaiter().GetResult();

                        Assert.Empty(fixture.List.Items);
                        Assert.Null(fixture.Editor.Id);
                        Assert.Empty(fixture.Editor.Lines);
                        Assert.Equal(1, control.ReceiptGrid.RowsCount);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Editing_a_line_after_save_updates_the_reloaded_line()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    using (var fixture = new Fixture())
                    using (var control = new GoodsReceiptControl(fixture.List, fixture.Editor))
                    {
                        fixture.Api.CurrentReceipt = ReceiptApiClient.Receipt(status: "Draft");
                        control.ActivateAsync(cancellationToken).GetAwaiter().GetResult();
                        control.SelectReceiptAsync(41, cancellationToken).GetAwaiter().GetResult();

                        control.SaveButton.PerformClick();
                        control.LastOperation.GetAwaiter().GetResult();
                        InlineReceiptTestDriver.SetValue(control, 2, 9.125m);
                        control.SaveButton.PerformClick();
                        control.LastOperation.GetAwaiter().GetResult();

                        Assert.Equal(9.125m, fixture.Api.LastUpdate!.Lines[0].Quantity);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Canceled_post_save_refresh_still_refreshes_list_and_reports_saved()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    using (var fixture = new Fixture())
                    using (var control = new GoodsReceiptControl(fixture.List, fixture.Editor))
                    {
                        control.ActivateAsync(cancellationToken).GetAwaiter().GetResult();
                        control.NewButton.PerformClick();
                        fixture.Editor.SetWarehouse(3, "W3 - Main", true);
                        fixture.Editor.SetEmployee(5, "E5 - Operator", true);
                        var line = fixture.Editor.AddLine();
                        line.SetProduct(7, "P7 - Product 7", true);
                        line.Quantity = 1m;
                        line.UnitCost = 2m;
                        fixture.Api.ControlCreateResponse = true;
                        var listCallsBeforeSave = fixture.Api.ListCalls;

                        control.SaveButton.PerformClick();
                        Assert.Equal(1, fixture.Api.CreateCalls);
                        fixture.Editor.CancelCurrentOperation();
                        fixture.Api.CompleteCreate();
                        PumpMessagesUntil(
                            () => control.LastOperation.IsCompleted,
                            TimeSpan.FromSeconds(2));
                        control.LastOperation.GetAwaiter().GetResult();

                        Assert.Equal(listCallsBeforeSave + 1, fixture.Api.ListCalls);
                        Assert.NotNull(fixture.Notifications.LastNotification);
                        Assert.Equal(
                            DesktopNotificationKind.Warning,
                            fixture.Notifications.LastNotification!.Kind);
                        Assert.Equal(
                            "Goods receipt was saved, but its latest details were not refreshed because the operation was canceled.",
                            fixture.Notifications.LastNotification.Message);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Editing_a_line_after_refresh_updates_the_reloaded_line()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    using (var fixture = new Fixture())
                    using (var control = new GoodsReceiptControl(fixture.List, fixture.Editor))
                    {
                        fixture.Api.CurrentReceipt = ReceiptApiClient.Receipt(status: "Draft");
                        control.ActivateAsync(cancellationToken).GetAwaiter().GetResult();
                        control.SelectReceiptAsync(41, cancellationToken).GetAwaiter().GetResult();

                        control.RefreshButton.PerformClick();
                        control.LastOperation.GetAwaiter().GetResult();
                        InlineReceiptTestDriver.SetValue(control, 3, 7.4321m);
                        control.SaveButton.PerformClick();
                        control.LastOperation.GetAwaiter().GetResult();

                        Assert.Equal(7.4321m, fixture.Api.LastUpdate!.Lines[0].UnitCost);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Detail_inputs_stay_disabled_during_the_entire_refresh()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    using (var fixture = new Fixture())
                    using (var control = new GoodsReceiptControl(fixture.List, fixture.Editor))
                    {
                        fixture.Api.CurrentReceipt = ReceiptApiClient.Receipt(status: "Draft");
                        control.ActivateAsync(cancellationToken).GetAwaiter().GetResult();
                        control.SelectReceiptAsync(41, cancellationToken).GetAwaiter().GetResult();
                        fixture.Api.ControlGetResponse = true;

                        control.RefreshButton.PerformClick();

                        Assert.False(control.NoteEditor.Enabled);
                        Assert.False(control.QuantityEditor.Enabled);
                        Assert.True(control.BusyIndicator.Visible);

                        fixture.Api.CompleteGet();
                        PumpMessagesUntil(
                            () => control.LastOperation.IsCompleted,
                            TimeSpan.FromSeconds(2));
                        control.LastOperation.GetAwaiter().GetResult();
                        Assert.True(control.NoteEditor.Enabled);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Reactivation_after_access_revocation_clears_visible_receipt_data()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    using (var fixture = new Fixture())
                    using (var control = new GoodsReceiptControl(fixture.List, fixture.Editor))
                    {
                        control.ActivateAsync(cancellationToken).GetAwaiter().GetResult();
                        control.SelectReceiptAsync(41, cancellationToken).GetAwaiter().GetResult();
                        Assert.Equal(41, fixture.Editor.Id);
                        fixture.Api.ListError = new ApiError(
                            ApiStatusCode.Forbidden,
                            "Auth.Forbidden",
                            "Denied.");

                        control.ActivateAsync(cancellationToken).GetAwaiter().GetResult();

                        Assert.Empty(fixture.List.Items);
                        Assert.Null(fixture.Editor.Id);
                        Assert.Empty(fixture.Editor.Lines);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Write_only_create_clears_sensitive_detail_and_locks_save_after_commit()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    using (var fixture = new Fixture())
                    using (var control = new GoodsReceiptControl(fixture.List, fixture.Editor))
                    {
                        var denied = new ApiError(
                            ApiStatusCode.Forbidden,
                            "Auth.Forbidden",
                            "Denied.");
                        fixture.Api.ListError = denied;
                        fixture.Api.GetError = denied;
                        fixture.Api.CurrentReceipt = ReceiptApiClient.Receipt(status: "Draft");
                        control.ActivateAsync(cancellationToken).GetAwaiter().GetResult();
                        control.NewButton.PerformClick();
                        fixture.Editor.SetWarehouse(3, "W3 - Main", true);
                        fixture.Editor.SetEmployee(5, "E5 - Operator", true);
                        var line = fixture.Editor.AddLine();
                        line.SetProduct(7, "P7 - Product 7", true);
                        line.Quantity = 1m;
                        line.UnitCost = 2m;

                        Assert.True(control.SaveButton.Enabled);
                        control.SaveButton.PerformClick();
                        control.LastOperation.GetAwaiter().GetResult();

                        Assert.Equal(1, fixture.Api.CreateCalls);
                        Assert.Null(fixture.Editor.Id);
                        Assert.Empty(fixture.Editor.Lines);
                        Assert.False(control.SaveButton.Enabled);
                        Assert.Equal(
                            "Goods receipt was saved. Read access is required to view or edit it.",
                            control.OperationStatusLabel.Text);

                        control.SaveButton.PerformClick();
                        Assert.Equal(1, fixture.Api.CreateCalls);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Repeated_activation_refreshes_and_disposal_owns_both_view_models()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    var fixture = new Fixture();
                    var control = new GoodsReceiptControl(fixture.List, fixture.Editor);
                    control.ActivateAsync(cancellationToken).GetAwaiter().GetResult();
                    control.ActivateAsync(cancellationToken).GetAwaiter().GetResult();
                    Assert.Equal(2, fixture.Api.ListCalls);

                    control.Dispose();
                    Assert.True(control.VersionLabel.IsDisposed);

                    Assert.Throws<ObjectDisposedException>(
                        (Action)(() => _ = fixture.List.RefreshAsync(CancellationToken.None)));
                    Assert.Throws<ObjectDisposedException>(() => fixture.Editor.AddLine());
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Open_catalog_controls_refresh_receipt_lookups_without_losing_selected_references()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    using (var guard = new WinFormsTestGuard())
                    using (var fixture = new Fixture())
                    using (var control = new GoodsReceiptControl(fixture.List, fixture.Editor))
                    {
                        var dispatcher = new ImmediateUiDispatcher();
                        var delay = new ImmediateDelay();
                        var warehouseViewModel = new WarehouseCatalogViewModel(
                            new WarehouseCatalogClient(fixture.WarehouseSource),
                            fixture.Messenger,
                            dispatcher,
                            fixture.Notifications,
                            delay,
                            TimeSpan.Zero);
                        var employeeViewModel = new EmployeeCatalogViewModel(
                            new EmployeeCatalogClient(fixture.EmployeeSource),
                            fixture.Messenger,
                            dispatcher,
                            fixture.Notifications,
                            delay,
                            TimeSpan.Zero);
                        var productViewModel = new ProductCatalogViewModel(
                            new ProductCatalogClient(fixture.ProductSource),
                            fixture.Messenger,
                            dispatcher,
                            fixture.Notifications,
                            delay,
                            TimeSpan.Zero);
                        using var warehouseControl = new CatalogControl(warehouseViewModel);
                        using var employeeControl = new CatalogControl(employeeViewModel);
                        using var productControl = new CatalogControl(productViewModel);
                        fixture.Api.CurrentReceipt = ReceiptApiClient.Receipt(status: "Draft");
                        control.ActivateAsync(cancellationToken).GetAwaiter().GetResult();
                        control.SelectReceiptAsync(41, cancellationToken).GetAwaiter().GetResult();
                        warehouseControl.ActivateAsync(cancellationToken).GetAwaiter().GetResult();
                        employeeControl.ActivateAsync(cancellationToken).GetAwaiter().GetResult();
                        productControl.ActivateAsync(cancellationToken).GetAwaiter().GetResult();
                        warehouseViewModel.SelectAsync(3, cancellationToken).GetAwaiter().GetResult();
                        employeeViewModel.SelectAsync(5, cancellationToken).GetAwaiter().GetResult();
                        productViewModel.SelectAsync(7, cancellationToken).GetAwaiter().GetResult();

                        warehouseControl.DeactivateButton.PerformClick();
                        warehouseControl.LastOperation.GetAwaiter().GetResult();
                        employeeControl.DeactivateButton.PerformClick();
                        employeeControl.LastOperation.GetAwaiter().GetResult();
                        productControl.DeactivateButton.PerformClick();
                        productControl.LastOperation.GetAwaiter().GetResult();

                        PumpMessagesUntil(
                            () => !fixture.Editor.WarehouseLookup.Items.Single(item => item.Id == 3).IsAvailableForNewSelection &&
                                !fixture.Editor.EmployeeLookup.Items.Single(item => item.Id == 5).IsAvailableForNewSelection &&
                                !fixture.Editor.Lines.Single().ProductIsAvailable,
                            TimeSpan.FromSeconds(5));

                        Assert.Equal(3, fixture.Editor.WarehouseId);
                        Assert.Equal(5, fixture.Editor.EmployeeId);
                        Assert.Equal(7, fixture.Editor.Lines.Single().ProductId);
                        Assert.Equal(3, control.WarehouseLookup.SelectedValue);
                        Assert.Equal(5, control.EmployeeLookup.SelectedValue);
                        Assert.Equal(7, control.LineGrid[1, 1].Value);

                        warehouseControl.ActivateButton.PerformClick();
                        warehouseControl.LastOperation.GetAwaiter().GetResult();
                        employeeControl.ActivateButton.PerformClick();
                        employeeControl.LastOperation.GetAwaiter().GetResult();
                        productControl.ActivateButton.PerformClick();
                        productControl.LastOperation.GetAwaiter().GetResult();

                        PumpMessagesUntil(
                            () => fixture.Editor.WarehouseLookup.Items.Single(item => item.Id == 3).IsAvailableForNewSelection &&
                                fixture.Editor.EmployeeLookup.Items.Single(item => item.Id == 5).IsAvailableForNewSelection &&
                                fixture.Editor.Lines.Single().ProductIsAvailable,
                            TimeSpan.FromSeconds(5));

                        Assert.Equal(3, fixture.Editor.WarehouseId);
                        Assert.Equal(5, fixture.Editor.EmployeeId);
                        Assert.Equal(7, fixture.Editor.Lines.Single().ProductId);
                        Assert.Equal(3, control.WarehouseLookup.SelectedValue);
                        Assert.Equal(5, control.EmployeeLookup.SelectedValue);
                        Assert.Equal(7, control.LineGrid[1, 1].Value);
                        Assert.Empty(guard.Exceptions);
                    }
                },
                TimeSpan.FromSeconds(15));
        }

        private sealed class Fixture : IDisposable
        {
            public Fixture()
            {
                Api = new ReceiptApiClient();
                Messenger = new WeakReferenceMessenger();
                Notifications = new DesktopNotificationCenter();
                var dispatcher = new ImmediateUiDispatcher();
                var delay = new ImmediateDelay();
                WarehouseSource = new LookupSource(CatalogKind.Warehouse);
                EmployeeSource = new LookupSource(CatalogKind.Employee);
                ProductSource = new LookupSource(CatalogKind.Product);
                List = new GoodsReceiptListViewModel(Api, dispatcher, Notifications, delay);
                Editor = new GoodsReceiptEditorViewModel(
                    Api,
                    new CatalogLookupViewModel(WarehouseSource, Messenger, dispatcher, Notifications, delay),
                    new CatalogLookupViewModel(EmployeeSource, Messenger, dispatcher, Notifications, delay),
                    new GoodsReceiptProductOptionsViewModel(ProductSource, Messenger, dispatcher, Notifications, delay),
                    dispatcher,
                    Notifications);
            }

            public ReceiptApiClient Api { get; }
            public WeakReferenceMessenger Messenger { get; }
            public DesktopNotificationCenter Notifications { get; }
            public LookupSource WarehouseSource { get; }
            public LookupSource EmployeeSource { get; }
            public LookupSource ProductSource { get; }
            public GoodsReceiptListViewModel List { get; }
            public GoodsReceiptEditorViewModel Editor { get; }

            public void Dispose()
            {
                List.Dispose();
                Editor.Dispose();
            }
        }

        private sealed class ImmediateDelay : IAsyncDelay
        {
            public Task DelayAsync(TimeSpan interval, CancellationToken cancellationToken) =>
                Task.CompletedTask;
        }

        private sealed class LookupSource : ICatalogLookupSource
        {
            public LookupSource(CatalogKind kind) => Kind = kind;
            public CatalogKind Kind { get; }
            public bool SelectedIsActive { get; set; } = true;
            public bool HoldQueries { get; set; }
            public List<TaskCompletionSource<ApiResponse<IReadOnlyList<CatalogLookupDto>>>> Pending { get; } = new List<TaskCompletionSource<ApiResponse<IReadOnlyList<CatalogLookupDto>>>>();
            public IList<CatalogLookupRequest> Requests { get; } = new List<CatalogLookupRequest>();

            public Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(
                CatalogLookupRequest request,
                CancellationToken cancellationToken)
            {
                Requests.Add(request);
                if (HoldQueries)
                {
                    var pending = new TaskCompletionSource<ApiResponse<IReadOnlyList<CatalogLookupDto>>>();
                    Pending.Add(pending);
                    return pending.Task; // Deliberately ignores cancellation to exercise generation fences.
                }
                return Task.FromResult(
                    ApiResponse<IReadOnlyList<CatalogLookupDto>>.Success(
                        new[] { new CatalogLookupDto(Kind == CatalogKind.Product ? 7 : 3, Kind.ToString(), "Choice", SelectedIsActive) }));
            }

            public Task<ApiResponse<CatalogLookupDto>> GetAsync(int id, CancellationToken cancellationToken) =>
                Task.FromResult(ApiResponse<CatalogLookupDto>.Success(new CatalogLookupDto(id, "C" + id, "Choice", SelectedIsActive)));
        }

        private sealed class WarehouseCatalogClient : IWarehouseApiClient
        {
            private readonly LookupSource _source;

            public WarehouseCatalogClient(LookupSource source) => _source = source;

            public Task<ApiResponse<PagedResult<WarehouseDto>>> ListAsync(CatalogListRequest request, CancellationToken token) =>
                Task.FromResult(ApiResponse<PagedResult<WarehouseDto>>.Success(
                    new PagedResult<WarehouseDto>(
                        new[] { new WarehouseDto(3, "W3", "Main", null, _source.SelectedIsActive) },
                        request.PageNumber,
                        request.PageSize,
                        1)));
            public Task<ApiResponse<WarehouseDto>> GetAsync(int id, CancellationToken token) =>
                Task.FromResult(ApiResponse<WarehouseDto>.Success(
                    new WarehouseDto(id, "W" + id, "Main", null, _source.SelectedIsActive)));
            public Task<ApiResponse<WarehouseDto>> CreateAsync(SaveWarehouseRequest request, CancellationToken token) => throw new NotSupportedException();
            public Task<ApiResponse<WarehouseDto>> UpdateAsync(int id, SaveWarehouseRequest request, CancellationToken token) => throw new NotSupportedException();
            public Task<ApiResponse<UnitResponse>> SetActiveAsync(int id, bool active, CancellationToken token)
            {
                _source.SelectedIsActive = active;
                return Task.FromResult(ApiResponse<UnitResponse>.Success(UnitResponse.Value));
            }
            public Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(CatalogLookupRequest request, CancellationToken token) => throw new NotSupportedException();
        }

        private sealed class EmployeeCatalogClient : IEmployeeApiClient
        {
            private readonly LookupSource _source;

            public EmployeeCatalogClient(LookupSource source) => _source = source;

            public Task<ApiResponse<PagedResult<EmployeeDto>>> ListAsync(CatalogListRequest request, CancellationToken token) =>
                Task.FromResult(ApiResponse<PagedResult<EmployeeDto>>.Success(
                    new PagedResult<EmployeeDto>(
                        new[] { new EmployeeDto(5, "E5", "Operator", null, null, _source.SelectedIsActive) },
                        request.PageNumber,
                        request.PageSize,
                        1)));
            public Task<ApiResponse<EmployeeDto>> GetAsync(int id, CancellationToken token) =>
                Task.FromResult(ApiResponse<EmployeeDto>.Success(
                    new EmployeeDto(id, "E" + id, "Operator", null, null, _source.SelectedIsActive)));
            public Task<ApiResponse<EmployeeDto>> CreateAsync(SaveEmployeeRequest request, CancellationToken token) => throw new NotSupportedException();
            public Task<ApiResponse<EmployeeDto>> UpdateAsync(int id, SaveEmployeeRequest request, CancellationToken token) => throw new NotSupportedException();
            public Task<ApiResponse<UnitResponse>> SetActiveAsync(int id, bool active, CancellationToken token)
            {
                _source.SelectedIsActive = active;
                return Task.FromResult(ApiResponse<UnitResponse>.Success(UnitResponse.Value));
            }
            public Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(CatalogLookupRequest request, CancellationToken token) => throw new NotSupportedException();
        }

        private sealed class ProductCatalogClient : IProductApiClient
        {
            private readonly LookupSource _source;

            public ProductCatalogClient(LookupSource source) => _source = source;

            public Task<ApiResponse<PagedResult<ProductDto>>> ListAsync(CatalogListRequest request, CancellationToken token) =>
                Task.FromResult(ApiResponse<PagedResult<ProductDto>>.Success(
                    new PagedResult<ProductDto>(
                        new[] { new ProductDto(7, "P7", "Product 7", "EA", _source.SelectedIsActive) },
                        request.PageNumber,
                        request.PageSize,
                        1)));
            public Task<ApiResponse<ProductDto>> GetAsync(int id, CancellationToken token) =>
                Task.FromResult(ApiResponse<ProductDto>.Success(
                    new ProductDto(id, "P" + id, "Product 7", "EA", _source.SelectedIsActive)));
            public Task<ApiResponse<ProductDto>> CreateAsync(SaveProductRequest request, CancellationToken token) => throw new NotSupportedException();
            public Task<ApiResponse<ProductDto>> UpdateAsync(int id, SaveProductRequest request, CancellationToken token) => throw new NotSupportedException();
            public Task<ApiResponse<UnitResponse>> SetActiveAsync(int id, bool active, CancellationToken token)
            {
                _source.SelectedIsActive = active;
                return Task.FromResult(ApiResponse<UnitResponse>.Success(UnitResponse.Value));
            }
            public Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(CatalogLookupRequest request, CancellationToken token) => throw new NotSupportedException();
        }

        private sealed class ReceiptApiClient : IGoodsReceiptApiClient
        {
            private TaskCompletionSource<ApiResponse<PagedResult<GoodsReceiptDto>>>? _listCompletion;
            private TaskCompletionSource<ApiResponse<GoodsReceiptDto>>? _getCompletion;
            private TaskCompletionSource<ApiResponse<GoodsReceiptDto>>? _createCompletion;

            public int ListCalls { get; private set; }
            public bool ControlListResponse { get; set; }
            public bool ControlGetResponse { get; set; }
            public ApiError? ListError { get; set; }
            public ApiError? GetError { get; set; }
            public int GetCalls { get; private set; }
            public GoodsReceiptDto CurrentReceipt { get; set; } = Receipt();
            public IReadOnlyList<GoodsReceiptDto>? ListRows { get; set; }
            public UpdateGoodsReceiptDraftRequest? LastUpdate { get; private set; }
            public PostGoodsReceiptRequest? LastPost { get; private set; }
            public int CreateCalls { get; private set; }
            public bool ControlCreateResponse { get; set; }
            public IList<GoodsReceiptListRequest> ListRequests { get; } =
                new List<GoodsReceiptListRequest>();

            public Task<ApiResponse<PagedResult<GoodsReceiptDto>>> ListAsync(
                GoodsReceiptListRequest request,
                CancellationToken cancellationToken)
            {
                ListCalls++;
                ListRequests.Add(request);
                if (ListError != null)
                {
                    return Task.FromResult(
                        ApiResponse<PagedResult<GoodsReceiptDto>>.Failure(ListError));
                }
                var response = ApiResponse<PagedResult<GoodsReceiptDto>>.Success(
                    new PagedResult<GoodsReceiptDto>(ListRows ?? new[] { CurrentReceipt }, request.PageNumber, 25, 30));
                if (!ControlListResponse)
                {
                    return Task.FromResult(response);
                }

                _listCompletion = new TaskCompletionSource<ApiResponse<PagedResult<GoodsReceiptDto>>>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                return _listCompletion.Task;
            }

            public void CompleteList()
            {
                _listCompletion!.TrySetResult(
                    ApiResponse<PagedResult<GoodsReceiptDto>>.Success(
                        new PagedResult<GoodsReceiptDto>(new[] { CurrentReceipt }, 1, 25, 30)));
            }

            public Task<ApiResponse<GoodsReceiptDto>> GetAsync(long id, CancellationToken cancellationToken)
            {
                GetCalls++;
                var response = GetError == null
                    ? ApiResponse<GoodsReceiptDto>.Success(CurrentReceipt)
                    : ApiResponse<GoodsReceiptDto>.Failure(GetError);
                if (!ControlGetResponse)
                {
                    return Task.FromResult(response);
                }

                _getCompletion = new TaskCompletionSource<ApiResponse<GoodsReceiptDto>>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                return _getCompletion.Task;
            }

            public void CompleteGet() =>
                _getCompletion!.TrySetResult(ApiResponse<GoodsReceiptDto>.Success(CurrentReceipt));

            public Task<ApiResponse<GoodsReceiptDto>> CreateDraftAsync(
                SaveGoodsReceiptRequest request,
                CancellationToken cancellationToken)
            {
                CreateCalls++;
                var response = ApiResponse<GoodsReceiptDto>.Success(CurrentReceipt);
                if (!ControlCreateResponse)
                {
                    return Task.FromResult(response);
                }

                _createCompletion = new TaskCompletionSource<ApiResponse<GoodsReceiptDto>>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                return _createCompletion.Task;
            }

            public void CompleteCreate() =>
                _createCompletion!.TrySetResult(ApiResponse<GoodsReceiptDto>.Success(CurrentReceipt));

            public Task<ApiResponse<GoodsReceiptDto>> UpdateDraftAsync(
                UpdateGoodsReceiptDraftRequest request,
                CancellationToken cancellationToken)
            {
                LastUpdate = request;
                return Task.FromResult(ApiResponse<GoodsReceiptDto>.Success(CurrentReceipt));
            }

            public Task<ApiResponse<PostGoodsReceiptResponse>> PostAsync(
                PostGoodsReceiptRequest request,
                CancellationToken cancellationToken)
            {
                LastPost = request;
                CurrentReceipt = Receipt(status: "Posted");
                return Task.FromResult(
                    ApiResponse<PostGoodsReceiptResponse>.Success(
                        new PostGoodsReceiptResponse(
                            request.ReceiptId,
                            "Posted",
                            new DateTime(2026, 10, 10, 1, 2, 3, DateTimeKind.Utc))));
            }

            public static GoodsReceiptDto Receipt(
                string status = "Posted",
                decimal quantity = 2.5m,
                decimal unitCost = 4m,
                int productId = 7,
                string productDisplayName = "P7 - Product 7",
                bool productIsActive = true) =>
                new GoodsReceiptDto(
                    41,
                    "GR0000000041",
                    new DateTime(2026, 10, 10),
                    3,
                    5,
                    status,
                    "received",
                    new DateTime(2026, 10, 10, 1, 2, 3, DateTimeKind.Utc),
                    "AQIDBAUGBwg=",
                    new[] { new GoodsReceiptLineDto(91, 1, productId, quantity, unitCost, productDisplayName, productIsActive) },
                    "W3 - Main",
                    true,
                    "E5 - Operator",
                    true);
        }

        private static void PumpMessagesUntil(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (!condition() && DateTime.UtcNow < deadline)
            {
                System.Windows.Forms.Application.DoEvents();
                Thread.Sleep(10);
            }

            Assert.True(condition(), "Expected UI update did not arrive before the timeout.");
        }
    }
}
