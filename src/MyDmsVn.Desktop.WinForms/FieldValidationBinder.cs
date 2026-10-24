using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;

namespace MyDmsVn.Desktop.WinForms
{
    /// <summary>Projects one error source into editor state and an accessible summary.</summary>
    public sealed class FieldValidationBinder : IDisposable
    {
        public sealed class Entry
        {
            internal Entry(string fieldKey, string message, Control? focusTarget)
            {
                FieldKey = fieldKey;
                Message = message;
                FocusTarget = focusTarget;
            }

            public string FieldKey { get; }
            public string Message { get; }
            public Control? FocusTarget { get; }
        }

        private sealed class Binding : IDisposable
        {
            private readonly FieldValidationBinder _owner;
            internal Binding(FieldValidationBinder owner, string fieldKey, Control focusTarget,
                Action<BootstrapValidationState> setState, Action<string> setMessage)
            {
                _owner = owner;
                FieldKey = fieldKey;
                FocusTarget = focusTarget;
                SetState = setState;
                SetMessage = setMessage;
            }

            internal string FieldKey { get; }
            internal Control FocusTarget { get; }
            internal Action<BootstrapValidationState> SetState { get; }
            internal Action<string> SetMessage { get; }
            public void Dispose() => _owner.Remove(this);
        }

        private readonly INotifyDataErrorInfo _source;
        private readonly Action<Action> _dispatch;
        private readonly Dictionary<string, Binding> _bindings = new Dictionary<string, Binding>(StringComparer.Ordinal);
        private readonly HashSet<string> _observedFields = new HashSet<string>(StringComparer.Ordinal);
        private IReadOnlyList<Entry> _entries = Array.Empty<Entry>();
        private bool _disposed;

        public FieldValidationBinder(INotifyDataErrorInfo source, Action<Action>? dispatch = null)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _dispatch = dispatch ?? (action => action());
            _source.ErrorsChanged += OnErrorsChanged;
        }

        public event EventHandler? Changed;
        public IReadOnlyList<Entry> Entries => _entries;

        public IDisposable Bind(string fieldKey, Control focusTarget,
            Action<BootstrapValidationState> setState, Action<string> setMessage)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FieldValidationBinder));
            if (string.IsNullOrWhiteSpace(fieldKey)) throw new ArgumentException("Field key is required.", nameof(fieldKey));
            if (_bindings.ContainsKey(fieldKey)) throw new InvalidOperationException("Field is already bound.");
            var binding = new Binding(this, fieldKey, focusTarget ?? throw new ArgumentNullException(nameof(focusTarget)),
                setState ?? throw new ArgumentNullException(nameof(setState)), setMessage ?? throw new ArgumentNullException(nameof(setMessage)));
            _bindings.Add(fieldKey, binding);
            _observedFields.Add(fieldKey);
            Refresh();
            return binding;
        }

        public void Refresh()
        {
            if (_disposed) return;
            var entries = new List<Entry>();
            foreach (var field in _observedFields.OrderBy(key => key, StringComparer.Ordinal))
            {
                var messages = (_source.GetErrors(field) ?? Array.Empty<string>()).Cast<object>()
                    .Select(value => Convert.ToString(value) ?? string.Empty)
                    .Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
                _bindings.TryGetValue(field, out var binding);
                var joined = string.Join(Environment.NewLine, messages);
                if (binding != null)
                {
                    binding.SetState(messages.Length == 0 ? BootstrapValidationState.None : BootstrapValidationState.Invalid);
                    binding.SetMessage(joined);
                }
                foreach (var message in messages) entries.Add(new Entry(field, message, binding?.FocusTarget));
            }
            _entries = entries;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private void OnErrorsChanged(object? sender, DataErrorsChangedEventArgs args)
        {
            _dispatch(() =>
            {
                if (_disposed) return;
                if (!string.IsNullOrWhiteSpace(args.PropertyName)) _observedFields.Add(args.PropertyName!);
                Refresh();
            });
        }

        private void Remove(Binding binding)
        {
            if (_disposed || !_bindings.TryGetValue(binding.FieldKey, out var current) || !ReferenceEquals(binding, current)) return;
            _bindings.Remove(binding.FieldKey);
            binding.SetState(BootstrapValidationState.None);
            binding.SetMessage(string.Empty);
            Refresh();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _source.ErrorsChanged -= OnErrorsChanged;
            foreach (var binding in _bindings.Values)
            {
                binding.SetState(BootstrapValidationState.None);
                binding.SetMessage(string.Empty);
            }
            _bindings.Clear();
            _entries = Array.Empty<Entry>();
        }
    }
}
