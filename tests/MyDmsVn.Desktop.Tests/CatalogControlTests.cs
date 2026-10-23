using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;
using MyDmsVn.Bootstrap5WinFormUI.Theme;
using CommunityToolkit.Mvvm.Messaging;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.WinForms;
using Xunit;
using Xunit.Abstractions;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class CatalogControlTests
    {
        private readonly ITestOutputHelper _output;

        public CatalogControlTests(ITestOutputHelper output) => _output = output;

        [Fact]
        public void Catalog_control_renders_active_state_and_keeps_compact_keyboard_layout()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    var client = new ProductClient
                    {
                        Items = new[]
                        {
                            new ProductDto(1, "P1", "Active", "pcs", true),
                            new ProductDto(2, "P2", "Inactive", "box", false),
                        },
                    };
                    using (var viewModel = CreateViewModel(client))
                    using (var control = new CatalogControl(viewModel))
                    {
                        control.Size = new System.Drawing.Size(1000, 700);
                        control.CreateControl();
                        control.PerformLayout();
                        control.ActivateAsync(cancellationToken).GetAwaiter().GetResult();

                        Assert.Equal(3, control.Grid.RowsCount);
                        Assert.Equal("Active", control.Grid[1, 3].Value);
                        Assert.Equal("Inactive", control.Grid[2, 3].Value);
                        Assert.True(control.EditorPanelWidth >= 280);
                        Assert.True(control.SearchBox.TabIndex < control.Grid.TabIndex);
                        Assert.True(control.Grid.TabIndex < control.EditorControls["code"].TabIndex);
                        Assert.True(control.EditorControls["code"].TabIndex < control.SaveButton.TabIndex);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Catalog_control_pages_beyond_the_first_twenty_five_rows()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    var client = new ProductClient
                    {
                        Items = Enumerable.Range(1, 30)
                            .Select(id => new ProductDto(id, "P" + id, "Product " + id, "pcs", true))
                            .ToArray(),
                    };
                    using (var viewModel = CreateViewModel(client))
                    using (var control = new CatalogControl(viewModel))
                    {
                        control.ActivateAsync(cancellationToken).GetAwaiter().GetResult();
                        Assert.Equal(26, control.Grid.RowsCount);
                        Assert.True(control.NextPageButton.Enabled);

                        control.NextPageButton.PerformClick();
                        control.LastOperation.GetAwaiter().GetResult();

                        Assert.Equal(2, viewModel.PageNumber);
                        Assert.Equal(6, control.Grid.RowsCount);
                        Assert.Equal("Page 2 of 2", control.PageLabel.Text);
                        Assert.False(control.NextPageButton.Enabled);
                        Assert.True(control.PreviousPageButton.Enabled);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Canceled_activation_does_not_start_a_catalog_query()
        {
            StaTest.Run(
                _ =>
                {
                    var client = new ProductClient();
                    using (var viewModel = CreateViewModel(client))
                    using (var control = new CatalogControl(viewModel))
                    using (var cancellation = new CancellationTokenSource())
                    {
                        cancellation.Cancel();

                        control.ActivateAsync(cancellation.Token).GetAwaiter().GetResult();

                        Assert.Equal(0, client.ListCount);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Save_button_forwards_editor_values_and_shows_field_errors_without_modal_dialogs()
        {
            StaTest.Run(
                _ =>
                {
                    var client = new ProductClient
                    {
                        CreateResponse = ApiResponse<ProductDto>.Failure(
                            new ApiError(
                                ApiStatusCode.BadRequest,
                                "ValidationError",
                                "Correct the fields.",
                                new[]
                                {
                                    new ApiErrorDetail(
                                        "Validation.Required",
                                        "Code is required.",
                                        "code"),
                                })),
                    };
                    using (var guard = new WinFormsTestGuard())
                    using (var viewModel = CreateViewModel(client))
                    using (var control = new CatalogControl(viewModel))
                    {
                        control.NewButton.PerformClick();
                        control.EditorControls["name"].Text = "Product";
                        control.EditorControls["unit"].Text = "pcs";
                        control.SaveButton.PerformClick();
                        control.LastOperation.GetAwaiter().GetResult();

                        Assert.Equal(1, client.CreateCount);
                        Assert.Equal("Code is required.", control.GetFieldError("code"));
                        Assert.Empty(guard.Exceptions);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Duplicate_code_retains_typed_text_and_offers_accessible_navigation()
        {
            StaTest.Run(_ =>
            {
                var client = new ProductClient
                {
                    CreateResponse = ApiResponse<ProductDto>.Failure(new ApiError(
                        ApiStatusCode.Conflict, "Catalog.DuplicateCode", "Code already exists.",
                        new[] { new ApiErrorDetail("Catalog.DuplicateCode", "Code already exists.", "code") })),
                };
                using (var viewModel = CreateViewModel(client))
                using (var control = new CatalogControl(viewModel))
                using (var host = new Form())
                {
                    host.Controls.Add(control);
                    host.Show();
                    control.NewButton.PerformClick();
                    control.EditorControls["code"].Text = "DUPLICATE";
                    control.EditorControls["name"].Text = "Product";
                    control.EditorControls["unit"].Text = "pcs";
                    control.SaveButton.PerformClick();
                    PumpMessagesUntil(() => control.LastOperation.IsCompleted && control.GetFieldError("code").Length != 0, TimeSpan.FromSeconds(2));

                    Assert.Equal("DUPLICATE", control.EditorControls["code"].Text);
                    Assert.Equal(BootstrapValidationState.Invalid, control.EditorControls["code"].ValidationState);
                    Assert.Contains("Code already exists.", control.ValidationSummaryText);
                    Assert.False(control.EditorControls["code"].ContainsFocus);
                    control.FocusFirstValidationError();
                    Assert.True(control.EditorControls["code"].ContainsFocus);
                }
            }, TimeSpan.FromSeconds(10));
        }

        [Theory]
        [InlineData(BootstrapThemeMode.Light)]
        [InlineData(BootstrapThemeMode.Dark)]
        public void Compact_product_validation_summary_keeps_bound_and_unknown_errors_visible_with_feedback_host(BootstrapThemeMode mode)
        {
            StaTest.Run(token =>
            {
                var oldTheme = BootstrapThemeManager.CurrentTheme;
                try
                {
                    BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(mode);
                    var client = new ProductClient
                    {
                        CreateResponse = ApiResponse<ProductDto>.Failure(new ApiError(
                            ApiStatusCode.Conflict, "Catalog.DuplicateCode", "Correct the fields.",
                            new[]
                            {
                                new ApiErrorDetail("Catalog.DuplicateCode", "Code already exists.", "code"),
                                new ApiErrorDetail("Validation.Unknown", "Additional check required.", "legacy.path"),
                            })),
                    };
                    using (var viewModel = CreateViewModel(client))
                    using (var control = new CatalogControl(viewModel))
                    using (var scope = new WorkspaceFeedbackScope())
                    using (var workspace = new WorkspaceFeedbackHost(control, scope))
                    using (var form = new Form { ClientSize = new System.Drawing.Size(940, 700) })
                    {
                        control.AttachFeedback(scope, new WorkspaceFeedbackNotificationService(scope));
                        form.Controls.Add(workspace);
                        form.Show();
                        control.NewButton.PerformClick();
                        control.EditorControls["code"].Text = "DUPLICATE";
                        control.EditorControls["name"].Text = "Product";
                        control.EditorControls["unit"].Text = "pcs";
                        control.SaveButton.PerformClick();
                        PumpMessagesUntil(() => control.LastOperation.IsCompleted && control.ValidationSummaryText.Contains("legacy.path"), TimeSpan.FromSeconds(2));
                        scope.Capture(scope.BeginOperation("command", WorkspaceOperationKind.Write)).Publish(
                            new DesktopNotification(DesktopNotificationKind.Error, "Correction needed."));
                        System.Windows.Forms.Application.DoEvents();

                        var summary = Assert.IsType<Label>(control.Controls.Find("CatalogValidationSummary", true).Single());
                        var navigate = Assert.IsType<BootstrapButton>(control.Controls.Find("CatalogValidationNavigation", true).Single());
                        Assert.True(summary.Visible);
                        Assert.True(workspace.Alert.Visible);
                        Assert.False(control.Controls.Find("CatalogError", true).Single().Visible);
                        Assert.Contains("code: Code already exists.", summary.Text);
                        Assert.Contains("legacy.path: Additional check required.", summary.Text);
                        Assert.Equal(BootstrapValidationState.Invalid, control.EditorControls["code"].ValidationState);
                        Assert.False(control.EditorControls["code"].ContainsFocus);
                        Assert.True(navigate.CanSelect);
                        navigate.Select();
                        navigate.PerformClick();
                        Assert.True(control.EditorControls["code"].ContainsFocus);
                        Assert.Equal("DUPLICATE", control.EditorControls["code"].Text);
                        _output.WriteLine($"Product validation theme={mode}; DeviceDpi={form.DeviceDpi}; client={form.ClientSize}; workspace={workspace.ClientSize}; inspector={control.EditorPanelWidth}");
                        _output.WriteLine(UiNextNativeScaleObservation.Observe(form, token));
                    }
                }
                finally { BootstrapThemeManager.CurrentTheme = oldTheme; }
            }, TimeSpan.FromSeconds(12));
        }

        [Fact]
        public void Editor_fields_are_disabled_while_a_save_is_in_flight()
        {
            StaTest.Run(
                _ =>
                {
                    var client = new ProductClient { ControlCreateResponse = true };
                    using (var viewModel = CreateViewModel(client))
                    using (var control = new CatalogControl(viewModel))
                    {
                        control.NewButton.PerformClick();
                        control.EditorControls["code"].Text = "P1";
                        control.EditorControls["name"].Text = "Product";
                        control.EditorControls["unit"].Text = "pcs";

                        control.SaveButton.PerformClick();

                        var busyHost = Assert.Single(control.Controls.OfType<BootstrapBusyHost>());
                        Assert.NotNull(busyHost.Content);
                        Assert.Contains(control.Grid, ((SplitContainer)busyHost.Content).Panel1.Controls.Cast<Control>());
                        Assert.All(control.EditorControls.Values, editor => Assert.False(editor.Enabled));

                        client.CompleteCreate(new ProductDto(1, "P1", "Product", "pcs", true));
                        PumpMessagesUntil(
                            () => control.LastOperation.IsCompleted,
                            TimeSpan.FromSeconds(2));
                        control.LastOperation.GetAwaiter().GetResult();

                        Assert.All(control.EditorControls.Values, editor => Assert.True(editor.Enabled));
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Theory]
        [InlineData(BootstrapThemeMode.Light)]
        [InlineData(BootstrapThemeMode.Dark)]
        public void Native_busy_overlay_stays_local_and_releases_after_save(BootstrapThemeMode mode)
        {
            StaTest.Run(token =>
            {
                var original = BootstrapThemeManager.CurrentTheme;
                try
                {
                    BootstrapThemeManager.CurrentTheme = BootstrapTheme.CreateDefault(mode);
                    using var guard = new WinFormsTestGuard();
                    var client = new ProductClient { ControlCreateResponse = true };
                    using var viewModel = CreateViewModel(client);
                    using var control = new CatalogControl(viewModel);
                    using var form = new Form { ClientSize = new System.Drawing.Size(940, 700), TopMost = true };
                    form.Controls.Add(control);
                    form.Show();
                    control.NewButton.PerformClick();
                    control.EditorControls["code"].Text = "P1";
                    control.EditorControls["name"].Text = "Product";
                    control.EditorControls["unit"].Text = "pcs";
                    control.SaveButton.PerformClick();
                    var busyHost = Assert.Single(control.Controls.OfType<BootstrapBusyHost>());
                    var overlay = Assert.Single(busyHost.Controls.OfType<Panel>());
                    _output.WriteLine(UiNextNativeScaleObservation.Observe(form, token));
                    PumpMessagesUntil(() => overlay.Visible, TimeSpan.FromSeconds(2));
                    Assert.True(overlay.Visible);
                    Assert.False(Assert.Single(overlay.Controls.OfType<Button>()).Visible);
                    Assert.False(control.SaveButton.Enabled);
                    Assert.False(busyHost.Contains(control.RefreshButton));
                    client.CompleteCreate(new ProductDto(1, "P1", "Product", "pcs", true));
                    PumpMessagesUntil(() => control.LastOperation.IsCompleted && !overlay.Visible, TimeSpan.FromSeconds(2));
                    control.LastOperation.GetAwaiter().GetResult();
                    Assert.False(overlay.Visible);
                    Assert.Empty(guard.Exceptions);
                }
                finally { BootstrapThemeManager.CurrentTheme = original; }
            }, TimeSpan.FromSeconds(20));
        }

        [Fact]
        public void Editor_fields_are_disabled_while_a_row_selection_is_in_flight()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    var product = new ProductDto(1, "P1", "Product", "pcs", true);
                    var client = new ProductClient
                    {
                        Items = new[] { product },
                        ControlGetResponses = true,
                    };
                    using (var viewModel = CreateViewModel(client))
                    using (var control = new CatalogControl(viewModel))
                    {
                        control.CreateControl();
                        control.ActivateAsync(cancellationToken).GetAwaiter().GetResult();

                        var selection = viewModel.SelectAsync(product.Id, cancellationToken);

                        Assert.All(control.EditorControls.Values, editor => Assert.False(editor.Enabled));
                        Assert.False(control.SaveButton.Enabled);

                        client.CompleteGet(product);
                        PumpMessagesUntil(
                            () => selection.IsCompleted &&
                                control.EditorControls.Values.All(editor => editor.Enabled),
                            TimeSpan.FromSeconds(2));
                        selection.GetAwaiter().GetResult();

                        Assert.All(control.EditorControls.Values, editor => Assert.True(editor.Enabled));
                        Assert.True(control.SaveButton.Enabled);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Enter_selects_the_active_catalog_row()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    var product = new ProductDto(1, "P1", "Product", "pcs", true);
                    var client = new ProductClient { Items = new[] { product } };
                    using (var viewModel = CreateViewModel(client))
                    using (var control = new CatalogControl(viewModel))
                    using (var host = new Form())
                    {
                        host.Controls.Add(control);
                        host.Show();
                        System.Windows.Forms.Application.DoEvents();
                        control.ActivateAsync(cancellationToken).GetAwaiter().GetResult();
                        Assert.True(control.Grid.Selection.Focus(new SourceGrid.Position(1, 0), true));

                        RaiseKeyDown(control.Grid, Keys.Enter);
                        control.LastOperation.GetAwaiter().GetResult();

                        Assert.Equal(product.Id, viewModel.SelectedId);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void All_catalog_list_cells_are_read_only()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    var product = new ProductDto(1, "P1", "Product", "pcs", true);
                    var client = new ProductClient { Items = new[] { product } };
                    using (var viewModel = CreateViewModel(client))
                    using (var control = new CatalogControl(viewModel))
                    using (var host = new Form())
                    {
                        host.Controls.Add(control);
                        host.Show();
                        System.Windows.Forms.Application.DoEvents();
                        control.ActivateAsync(cancellationToken).GetAwaiter().GetResult();

                        foreach (var column in Enumerable.Range(0, 4))
                        {
                            var position = new SourceGrid.Position(1, column);
                            Assert.True(control.Grid.Selection.Focus(position, true));
                            var context = new SourceGrid.CellContext(control.Grid, position);

                            context.StartEdit();

                            Assert.False(context.IsEditing());
                        }
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Enter_uses_the_immutable_row_id_instead_of_the_visible_cell_value()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    var first = new ProductDto(1, "P1", "First", "pcs", true);
                    var other = new ProductDto(999, "P999", "Other", "pcs", true);
                    var client = new ProductClient { Items = new[] { first, other } };
                    using (var viewModel = CreateViewModel(client))
                    using (var control = new CatalogControl(viewModel))
                    using (var host = new Form())
                    {
                        host.Controls.Add(control);
                        host.Show();
                        System.Windows.Forms.Application.DoEvents();
                        control.ActivateAsync(cancellationToken).GetAwaiter().GetResult();
                        control.Grid[1, 0].Value = other.Id;
                        Assert.True(control.Grid.Selection.Focus(new SourceGrid.Position(1, 0), true));

                        RaiseKeyDown(control.Grid, Keys.Enter);
                        control.LastOperation.GetAwaiter().GetResult();

                        Assert.Equal(first.Id, viewModel.SelectedId);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Enter_does_not_select_a_row_while_the_active_cell_is_editing()
        {
            StaTest.Run(
                cancellationToken =>
                {
                    var product = new ProductDto(1, "P1", "Product", "pcs", true);
                    var client = new ProductClient { Items = new[] { product } };
                    using (var viewModel = CreateViewModel(client))
                    using (var control = new CatalogControl(viewModel))
                    using (var host = new Form())
                    {
                        host.Controls.Add(control);
                        host.Show();
                        System.Windows.Forms.Application.DoEvents();
                        control.ActivateAsync(cancellationToken).GetAwaiter().GetResult();
                        var position = new SourceGrid.Position(1, 1);
                        control.Grid[position].Editor =
                            new SourceGrid.Cells.Editors.TextBox(typeof(string));
                        Assert.True(control.Grid.Selection.Focus(position, true));
                        var context = new SourceGrid.CellContext(control.Grid, position);
                        context.StartEdit();
                        Assert.True(context.IsEditing());

                        RaiseKeyDown(control.Grid, Keys.Enter);

                        Assert.Null(viewModel.SelectedId);
                        if (context.IsEditing())
                        {
                            context.EndEdit(true);
                        }
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Background_view_model_updates_are_marshaled_to_the_control_thread()
        {
            StaTest.Run(
                _ =>
                {
                    var uiThreadId = Thread.CurrentThread.ManagedThreadId;
                    using (var viewModel = CreateViewModel(new ProductClient()))
                    using (var control = new CatalogControl(viewModel))
                    {
                        Assert.NotEqual(IntPtr.Zero, control.Handle);
                        var updateThreadId = 0;
                        control.EditorControls["code"].TextChanged += (_, __) =>
                            updateThreadId = Thread.CurrentThread.ManagedThreadId;

                        Task.Run(() => viewModel.Code = "BACKGROUND").GetAwaiter().GetResult();
                        PumpMessagesUntil(
                            () => control.EditorControls["code"].Text == "BACKGROUND",
                            TimeSpan.FromSeconds(2));

                        Assert.Equal(uiThreadId, updateThreadId);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Disposing_control_disposes_view_model_and_prevents_delayed_work()
        {
            StaTest.Run(
                _ =>
                {
                    var viewModel = CreateViewModel(new ProductClient());
                    var control = new CatalogControl(viewModel);

                    control.Dispose();

                    var exception = Record.Exception(
                        (Action)(() => viewModel.SetSearch("late")));
                    Assert.IsType<ObjectDisposedException>(exception);
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Shell_reuses_catalog_tab_and_disposes_catalog_controls_on_sign_out()
        {
            StaTest.Run(
                _ =>
                {
                    var session = new TestDesktopSession();
                    session.SetCurrentUser(new CurrentUserDto(1, "operator", "Operator"));
                    var factory = new RecordingCatalogControlFactory();
                    using (var shell = new FoundationShellForm(
                        new FoundationViewModel(new ReadyApiClient()),
                        session,
                        new DesktopNotificationCenter(),
                        factory))
                    {
                        var first = shell.OpenCatalog(CatalogKind.Product);
                        var second = shell.OpenCatalog(CatalogKind.Product);

                        Assert.Same(first, second);
                        Assert.Single(shell.Workspace.TabPages);
                        Assert.Single(factory.Created);
                        Assert.False(factory.Created[0].IsDisposed);

                        session.SignOut();

                        Assert.True(factory.Created[0].IsDisposed);
                        Assert.Empty(shell.Workspace.TabPages);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void Reopening_existing_catalog_tab_refreshes_authoritative_data_without_new_control()
        {
            StaTest.Run(
                _ =>
                {
                    var session = new TestDesktopSession();
                    session.SetCurrentUser(new CurrentUserDto(1, "operator", "Operator"));
                    var client = new ProductClient();
                    var factory = new ProductCatalogControlFactory(client);
                    using (var shell = new FoundationShellForm(
                        new FoundationViewModel(new ReadyApiClient()),
                        session,
                        new DesktopNotificationCenter(),
                        factory))
                    {
                        shell.OpenCatalog(CatalogKind.Product);
                        factory.Control!.LastOperation.GetAwaiter().GetResult();
                        shell.OpenCatalog(CatalogKind.Product);
                        factory.Control.LastOperation.GetAwaiter().GetResult();

                        Assert.Equal(1, factory.CreateCount);
                        Assert.Equal(2, client.ListCount);
                    }
                },
                TimeSpan.FromSeconds(10));
        }

        private static ProductCatalogViewModel CreateViewModel(IProductApiClient client)
        {
            return new ProductCatalogViewModel(
                client,
                new WeakReferenceMessenger(),
                new ImmediateUiDispatcher(),
                new DesktopNotificationCenter(),
                new SystemAsyncDelay(),
                TimeSpan.Zero);
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

        private static void RaiseKeyDown(Control control, Keys key)
        {
            var onKeyDown = control.GetType().GetMethod(
                "OnKeyDown",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(onKeyDown);
            onKeyDown!.Invoke(control, new object[] { new KeyEventArgs(key) });
        }

        private sealed class ProductClient : IProductApiClient
        {
            private TaskCompletionSource<ApiResponse<ProductDto>>? _controlledCreate;
            private TaskCompletionSource<ApiResponse<ProductDto>>? _controlledGet;

            public ProductDto[] Items { get; set; } = Array.Empty<ProductDto>();
            public int CreateCount { get; private set; }
            public int ListCount { get; private set; }
            public bool ControlCreateResponse { get; set; }
            public bool ControlGetResponses { get; set; }
            public ApiResponse<ProductDto> CreateResponse { get; set; } =
                ApiResponse<ProductDto>.Success(new ProductDto(1, "P1", "Product", "pcs", true));

            public Task<ApiResponse<PagedResult<ProductDto>>> ListAsync(CatalogListRequest request, CancellationToken token)
            {
                ListCount++;
                return Task.FromResult(
                    ApiResponse<PagedResult<ProductDto>>.Success(
                        new PagedResult<ProductDto>(
                            Items.Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize).ToArray(),
                            request.PageNumber,
                            request.PageSize,
                            Items.Length)));
            }
            public Task<ApiResponse<ProductDto>> GetAsync(int id, CancellationToken token)
            {
                if (!ControlGetResponses)
                {
                    return Task.FromResult(
                        ApiResponse<ProductDto>.Success(Items.Single(item => item.Id == id)));
                }

                _controlledGet = new TaskCompletionSource<ApiResponse<ProductDto>>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                return _controlledGet.Task;
            }

            public void CompleteGet(ProductDto product) =>
                _controlledGet!.TrySetResult(ApiResponse<ProductDto>.Success(product));
            public Task<ApiResponse<ProductDto>> CreateAsync(SaveProductRequest request, CancellationToken token)
            {
                CreateCount++;
                if (!ControlCreateResponse)
                {
                    return Task.FromResult(CreateResponse);
                }

                _controlledCreate = new TaskCompletionSource<ApiResponse<ProductDto>>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                return _controlledCreate.Task;
            }
            public void CompleteCreate(ProductDto product) =>
                _controlledCreate!.TrySetResult(ApiResponse<ProductDto>.Success(product));
            public Task<ApiResponse<ProductDto>> UpdateAsync(int id, SaveProductRequest request, CancellationToken token) => Task.FromResult(CreateResponse);
            public Task<ApiResponse<UnitResponse>> SetActiveAsync(int id, bool active, CancellationToken token) => Task.FromResult(ApiResponse<UnitResponse>.Success(UnitResponse.Value));
            public Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(CatalogLookupRequest request, CancellationToken token) => throw new NotSupportedException();
        }

        private sealed class ProductCatalogControlFactory : ICatalogControlFactory
        {
            private readonly ProductClient _client;
            public ProductCatalogControlFactory(ProductClient client) => _client = client;
            public int CreateCount { get; private set; }
            public CatalogControl? Control { get; private set; }
            public Control Create(CatalogKind kind)
            {
                CreateCount++;
                Control = new CatalogControl(CreateViewModel(_client));
                return Control;
            }
        }

        private sealed class RecordingCatalogControlFactory : ICatalogControlFactory
        {
            public List<Control> Created { get; } = new List<Control>();

            public Control Create(CatalogKind kind)
            {
                var control = new Panel { Name = kind.ToString() };
                Created.Add(control);
                return control;
            }
        }

        private sealed class TestDesktopSession : IDesktopSession
        {
            public event EventHandler? SessionChanged;
            public bool IsAuthenticated => CurrentUser != null;

            public DesktopSessionState State => IsAuthenticated
                ? DesktopSessionState.Authenticated
                : DesktopSessionState.Unauthenticated;
            public CurrentUserDto? CurrentUser { get; private set; }
            public long Version { get; private set; }
            public void SignOut() => SetCurrentUser(null);
            public void SetCurrentUser(CurrentUserDto? user)
            {
                CurrentUser = user;
                Version++;
                SessionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private sealed class ReadyApiClient : IFoundationApiClient
        {
            public Task<ApiResponse<FoundationStatus>> GetStatusAsync(FoundationStatusRequest request, CancellationToken token) =>
                Task.FromResult(ApiResponse<FoundationStatus>.Success(new FoundationStatus(true, "Local")));
        }
    }
}
