using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;
using BootstrapGrid = MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapSourceGrid;

namespace MyDmsVn.Desktop.WinForms
{
    public sealed class MasterDetailRow
    {
        public MasterDetailRow(string key, string code, string name)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public string Key { get; }

        public string Code { get; }

        public string Name { get; }
    }

    public sealed class LookupOption
    {
        public LookupOption(int id, string displayName)
        {
            Id = id;
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        }

        public int Id { get; }

        public string DisplayName { get; }
    }

    public sealed class MasterDetailPrototypeControl : UserControl
    {
        private readonly ErrorProvider _errors;
        private readonly Dictionary<string, Control> _fields;

        public MasterDetailPrototypeControl()
        {
            Dock = DockStyle.Fill;

            Grid = new BootstrapGrid
            {
                Dock = DockStyle.Fill,
                TabStop = true,
            };
            CodeEditor = new BootstrapTextBox
            {
                Dock = DockStyle.Top,
                AccessibleName = "Code",
            };
            NameEditor = new BootstrapTextBox
            {
                Dock = DockStyle.Top,
                AccessibleName = "Name",
            };
            Lookup = new BootstrapLookupBox
            {
                Dock = DockStyle.Top,
                AccessibleName = "Lookup",
                DisplayMember = nameof(LookupOption.DisplayName),
                ValueMember = nameof(LookupOption.Id),
            };
            Lookup.SearchMembers.Add(nameof(LookupOption.DisplayName));

            _fields = new Dictionary<string, Control>(StringComparer.Ordinal)
            {
                ["code"] = CodeEditor,
                ["name"] = NameEditor,
                ["lookupId"] = Lookup,
            };
            _errors = new ErrorProvider
            {
                BlinkStyle = ErrorBlinkStyle.NeverBlink,
            };

            var editor = CreateEditorPanel();
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel2,
            };
            split.SizeChanged += (_, _) => UpdateEditorPanelWidth(split);
            split.Panel1.Controls.Add(Grid);
            split.Panel2.Controls.Add(editor);
            Controls.Add(split);
            UpdateEditorPanelWidth(split);
            SetRows(Array.Empty<MasterDetailRow>());
        }

        public event EventHandler? RefreshRequested;

        public event EventHandler? NewRequested;

        public event EventHandler? SaveRequested;

        public BootstrapGrid Grid { get; }

        public BootstrapTextBox CodeEditor { get; }

        public BootstrapTextBox NameEditor { get; }

        public BootstrapLookupBox Lookup { get; }

        public void SetRows(IEnumerable<MasterDetailRow> rows)
        {
            if (rows == null)
            {
                throw new ArgumentNullException(nameof(rows));
            }

            var items = rows.ToArray();
            Grid.Redim(items.Length + 1, 3);
            Grid.FixedRows = 1;
            Grid[0, 0] = new SourceGrid.Cells.ColumnHeader("Key");
            Grid[0, 1] = new SourceGrid.Cells.ColumnHeader("Code");
            Grid[0, 2] = new SourceGrid.Cells.ColumnHeader("Name");
            for (var index = 0; index < items.Length; index++)
            {
                var row = index + 1;
                Grid[row, 0] = new SourceGrid.Cells.Cell(items[index].Key, typeof(string));
                Grid[row, 1] = new SourceGrid.Cells.Cell(items[index].Code, typeof(string));
                Grid[row, 2] = new SourceGrid.Cells.Cell(items[index].Name, typeof(string));
            }

            Grid.AutoSizeCells();
        }

        public void SetLookupOptions(IEnumerable<LookupOption> options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            Lookup.DataSource = options.ToArray();
        }

        public void ApplyFieldErrors(
            IReadOnlyDictionary<string, IReadOnlyList<string>> fieldErrors)
        {
            if (fieldErrors == null)
            {
                throw new ArgumentNullException(nameof(fieldErrors));
            }

            foreach (var field in _fields)
            {
                _errors.SetError(field.Value, string.Empty);
            }

            foreach (var error in fieldErrors)
            {
                if (_fields.TryGetValue(error.Key, out var control))
                {
                    _errors.SetError(control, string.Join(Environment.NewLine, error.Value));
                }
            }
        }

        public string GetFieldError(string field)
        {
            return _fields.TryGetValue(field, out var control)
                ? _errors.GetError(control)
                : string.Empty;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _errors.Dispose();
            }

            base.Dispose(disposing);
        }

        private Control CreateEditorPanel()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                ColumnCount = 1,
                RowCount = 7,
                Padding = new Padding(12),
            };
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.Controls.Add(new Label { Text = "Code", AutoSize = true });
            panel.Controls.Add(CodeEditor);
            panel.Controls.Add(new Label { Text = "Name", AutoSize = true });
            panel.Controls.Add(NameEditor);
            panel.Controls.Add(new Label { Text = "Lookup", AutoSize = true });
            panel.Controls.Add(Lookup);
            panel.Controls.Add(CreateCommandBar());
            return panel;
        }

        private static void UpdateEditorPanelWidth(SplitContainer split)
        {
            var availableWidth = split.ClientSize.Width - split.SplitterWidth;
            if (availableWidth < 560)
            {
                return;
            }

            var editorWidth = Math.Min(400, Math.Max(280, availableWidth / 3));
            split.SplitterDistance = availableWidth - editorWidth;
        }

        private Control CreateCommandBar()
        {
            var commandBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
            };
            var refresh = new BootstrapButton { Text = "Refresh", AutoSize = true };
            var create = new BootstrapButton { Text = "New", AutoSize = true };
            var save = new BootstrapButton { Text = "Save", AutoSize = true };
            refresh.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);
            create.Click += (_, _) => NewRequested?.Invoke(this, EventArgs.Empty);
            save.Click += (_, _) => SaveRequested?.Invoke(this, EventArgs.Empty);
            commandBar.Controls.Add(refresh);
            commandBar.Controls.Add(create);
            commandBar.Controls.Add(save);
            return commandBar;
        }
    }
}
