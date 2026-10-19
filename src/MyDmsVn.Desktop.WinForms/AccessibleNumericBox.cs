using System;
using System.Globalization;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;

namespace MyDmsVn.Desktop.WinForms
{
    // The vendor's virtual SpinButton wrapper hides its nested edit from the net48 UIA bridge.
    // Expose the same numeric value and existing ValueChanged path through a standard editable text role.
    internal sealed class AccessibleNumericBox : BootstrapNumericBox
    {
        protected override AccessibleObject CreateAccessibilityInstance() => new NumericAccessibleObject(this);

        private sealed class NumericAccessibleObject : ControlAccessibleObject
        {
            private readonly AccessibleNumericBox _owner;
            public NumericAccessibleObject(AccessibleNumericBox owner) : base(owner) { _owner = owner; }
            public override AccessibleRole Role => AccessibleRole.Text;
            public override string? Value
            {
                get => _owner.Value.ToString("F" + _owner.DecimalPlaces, CultureInfo.CurrentCulture);
                set
                {
                    if (_owner.IsDisposed || !_owner.Enabled) throw new InvalidOperationException("Numeric input is unavailable.");
                    if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var number) ||
                        number < _owner.Minimum || number > _owner.Maximum ||
                        decimal.Round(number, _owner.DecimalPlaces) != number)
                        throw new ArgumentOutOfRangeException(nameof(value));
                    _owner.Value = number;
                }
            }
        }
    }
}
