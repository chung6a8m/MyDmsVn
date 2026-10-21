using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Icons;

namespace MyDmsVn.Desktop.WinForms;

public sealed class BusinessCompactNavigationItem : INotifyPropertyChanged
{
    private string _text = "";
    private bool _enabled = true, _expanded;
    private IconDescriptor? _icon;
    private object? _tag;
    public string Text { get => _text; set { _text = value ?? ""; Changed(); } }
    public bool Enabled { get => _enabled; set { _enabled = value; Changed(); } }
    public bool Expanded { get => _expanded; set { _expanded = value; Changed(); } }
    public bool Selected { get; internal set; }
    public object? Tag { get => _tag; set { _tag = value; Changed(); } }
    public IconDescriptor? Icon { get => _icon; set { _icon = value; Changed(); } }
    public BindingList<BusinessCompactNavigationItem> Items { get; } = new BindingList<BusinessCompactNavigationItem>();
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
}

// Native HWND-backed buttons preserve keyboard/UIA behavior with an independently owned palette.
public sealed class BusinessCompactNavigation : UserControl
{
    private readonly FlowLayoutPanel _itemsHost = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
    private readonly Button _toggle = new Button { Dock = DockStyle.Top, Text = "☰", FlatStyle = FlatStyle.Flat, TabIndex = 0 };
    private readonly HashSet<BusinessCompactNavigationItem> _observed = new HashSet<BusinessCompactNavigationItem>();
    private BusinessCompactNavigationItem? _selected;
    private bool _expanded = true, _rendering;
    public BusinessCompactNavigation()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        AccessibleName = "Main navigation";
        Controls.Add(_itemsHost); Controls.Add(_toggle);
        Items.ListChanged += OnItemsChanged;
        _toggle.Click += (_, __) => Expanded = !Expanded;
        Render();
    }
    public BindingList<BusinessCompactNavigationItem> Items { get; } = new BindingList<BusinessCompactNavigationItem>();
    public int ExpandedWidth { get; set; } = 224;
    public int CollapsedWidth { get; set; } = 64;
    public bool Expanded { get => _expanded; set { if (_expanded == value) return; _expanded = value; Render(); } }
    public BusinessCompactNavigationItem? SelectedItem
    {
        get => _selected;
        set
        {
            if (value != null && (!All(Items).Contains(value) || !Admitted(value, Items, true))) return;
            if (ReferenceEquals(_selected, value)) return;
            if (_selected != null) _selected.Selected = false;
            _selected = value;
            if (value != null) value.Selected = true;
            Render(); SelectedItemChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public event EventHandler? SelectedItemChanged;
    public void Expand() => Expanded = true;
    public void Collapse() => Expanded = false;
    public void Toggle() => Expanded = !Expanded;
    private static IEnumerable<BusinessCompactNavigationItem> All(IEnumerable<BusinessCompactNavigationItem> items) => items.SelectMany(item => new[] { item }.Concat(All(item.Items)));
    private static bool Admitted(BusinessCompactNavigationItem target, IEnumerable<BusinessCompactNavigationItem> items, bool parentEnabled)
    {
        foreach (var item in items)
            if (ReferenceEquals(item, target)) return parentEnabled && item.Enabled;
            else if (Admitted(target, item.Items, parentEnabled && item.Enabled)) return true;
        return false;
    }
    private void OnItemsChanged(object? sender, ListChangedEventArgs e) => Render();
    private void OnItemChanged(object? sender, PropertyChangedEventArgs e) => Render();
    private void Observe()
    {
        foreach (var item in _observed) { item.PropertyChanged -= OnItemChanged; item.Items.ListChanged -= OnItemsChanged; }
        _observed.Clear();
        foreach (var item in All(Items))
            if (_observed.Add(item)) { item.PropertyChanged += OnItemChanged; item.Items.ListChanged += OnItemsChanged; }
    }
    private void Render()
    {
        if (_rendering || IsDisposed) return;
        _rendering = true;
        var removedSelection = false;
        try
        {
            Observe();
            if (_selected != null && !_observed.Contains(_selected))
            { _selected.Selected = false; _selected = null; removedSelection = true; }
            var focused = _itemsHost.Controls.Cast<Control>().FirstOrDefault(button => button.ContainsFocus)?.Tag;
            _itemsHost.SuspendLayout();
            foreach (Control control in _itemsHost.Controls.Cast<Control>().ToArray()) control.Dispose();
            Width = BusinessCompactAppearance.Scale(this, Expanded ? ExpandedWidth : CollapsedWidth);
            _toggle.Height = BusinessCompactAppearance.Scale(this, 48);
            _toggle.AccessibleName = Expanded ? "Collapse navigation" : "Expand navigation";
            AddRows(Items, 0, true);
            _itemsHost.ResumeLayout();
            ApplyPalette();
            if (focused != null) _itemsHost.Controls.Cast<Control>().FirstOrDefault(button => ReferenceEquals(button.Tag, focused))?.Focus();
        }
        finally { _rendering = false; }
        if (removedSelection) SelectedItemChanged?.Invoke(this, EventArgs.Empty);
    }
    private void AddRows(IEnumerable<BusinessCompactNavigationItem> items, int depth, bool enabled)
    {
        foreach (var item in items)
        {
            var button = new NavigationButton(item)
            {
                Text = Expanded ? item.Text : item.Icon != null ? "" : item.Text.Substring(0, Math.Min(1, item.Text.Length)),
                AccessibleName = item.Text, Tag = item, Enabled = enabled && item.Enabled,
                AccessibleDescription = (item.Selected ? "Selected. " : "") + (item.Items.Count == 0 ? "" : item.Expanded ? "Section expanded" : "Section collapsed"),
                Width = Width - BusinessCompactAppearance.Scale(this, 8) - SystemInformation.VerticalScrollBarWidth, Height = BusinessCompactAppearance.Scale(this, 36),
                Margin = new Padding(0, 0, 0, BusinessCompactAppearance.Scale(this, 4)),
                Padding = new Padding(BusinessCompactAppearance.Scale(this, Expanded ? 12 + depth * 16 + (item.Icon != null ? 20 : 0) : 4), 0, 0, 0),
                FlatStyle = FlatStyle.Flat, TextAlign = Expanded ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleCenter,
            };
            button.Click += (_, __) =>
            {
                if (item.Items.Count > 0) { if (!Expanded) Expanded = true; item.Expanded = !item.Expanded; }
                SelectedItem = item;
            };
            button.KeyDown += (_, e) =>
            {
                var buttons = _itemsHost.Controls.OfType<Button>().Where(current => current.Enabled).ToArray();
                var index = Array.IndexOf(buttons, button);
                if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up || e.KeyCode == Keys.Home || e.KeyCode == Keys.End)
                {
                    var next = e.KeyCode == Keys.Home ? 0 : e.KeyCode == Keys.End ? buttons.Length - 1 : Math.Max(0, Math.Min(buttons.Length - 1, index + (e.KeyCode == Keys.Down ? 1 : -1)));
                    buttons[next].Focus(); e.Handled = e.SuppressKeyPress = true;
                }
                else if (item.Items.Count > 0 && (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right))
                { item.Expanded = e.KeyCode == Keys.Right; e.Handled = e.SuppressKeyPress = true; }
            };
            _itemsHost.Controls.Add(button);
            if (Expanded && item.Expanded) AddRows(item.Items, depth + 1, enabled && item.Enabled);
        }
    }
    internal void ApplyPalette()
    {
        BackColor = _itemsHost.BackColor = _toggle.BackColor = BusinessCompactAppearance.Navy;
        _toggle.ForeColor = Color.White;
        foreach (Button button in _itemsHost.Controls)
        {
            button.BackColor = ((BusinessCompactNavigationItem)button.Tag).Selected ? Color.FromArgb(48, 76, 114) : BusinessCompactAppearance.Navy;
            button.ForeColor = Color.White; button.FlatAppearance.BorderSize = 0;
        }
    }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); Render(); }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Items.ListChanged -= OnItemsChanged;
            foreach (var item in _observed) { item.PropertyChanged -= OnItemChanged; item.Items.ListChanged -= OnItemsChanged; }
            _observed.Clear();
        }
        base.Dispose(disposing);
    }
    private sealed class NavigationButton : Button
    {
        private readonly BusinessCompactNavigationItem _item;
        private readonly IIconRenderer _renderer = BootstrapIconRenderer.CreateDefault();
        internal NavigationButton(BusinessCompactNavigationItem item) { _item = item; }
        protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) == Keys.Left || (keyData & Keys.KeyCode) == Keys.Right ||
            (keyData & Keys.KeyCode) == Keys.Up || (keyData & Keys.KeyCode) == Keys.Down ||
            (keyData & Keys.KeyCode) == Keys.Home || (keyData & Keys.KeyCode) == Keys.End || base.IsInputKey(keyData);
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var extent = BusinessCompactAppearance.Scale(this, 16);
            if (_item.Icon != null) _renderer.TryRender(e.Graphics, _item.Icon,
                new Rectangle(BusinessCompactAppearance.Scale(this, 4), (Height - extent) / 2, extent, extent), Enabled ? ForeColor : SystemColors.GrayText);
        }
    }
}
