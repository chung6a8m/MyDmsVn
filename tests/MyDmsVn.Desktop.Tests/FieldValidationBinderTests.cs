using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;
using MyDmsVn.Desktop.WinForms;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class FieldValidationBinderTests
    {
        [Fact]
        public void Bound_and_unknown_errors_project_without_focus_and_clear_independently()
        {
            StaTest.Run(_ =>
            {
                var source = new ErrorSource();
                using (var editor = new TextBox())
                using (var binder = new FieldValidationBinder(source))
                {
                    var state = BootstrapValidationState.None;
                    var help = string.Empty;
                    using (binder.Bind("code", editor, value => state = value, value => help = value))
                    {
                        source.Set("code", "Code already exists.");
                        source.Set("serverField", "Check the server field.");

                        Assert.Equal(BootstrapValidationState.Invalid, state);
                        Assert.Equal("Code already exists.", help);
                        Assert.False(editor.Focused);
                        Assert.Contains(binder.Entries, entry => entry.FieldKey == "serverField" && entry.FocusTarget == null);

                        source.Set("code");
                        Assert.Equal(BootstrapValidationState.None, state);
                        Assert.Empty(help);
                        Assert.Contains(binder.Entries, entry => entry.FieldKey == "serverField");
                    }
                }
            }, TimeSpan.FromSeconds(10));
        }

        private sealed class ErrorSource : INotifyDataErrorInfo
        {
            private readonly Dictionary<string, string[]> _errors = new Dictionary<string, string[]>();
            public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;
            public bool HasErrors => _errors.Count != 0;
            public IEnumerable GetErrors(string? propertyName) => propertyName != null && _errors.TryGetValue(propertyName, out var messages)
                ? messages : Array.Empty<string>();
            public void Set(string field, params string[] messages)
            {
                if (messages.Length == 0) _errors.Remove(field);
                else _errors[field] = messages;
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(field));
            }
        }
    }
}
