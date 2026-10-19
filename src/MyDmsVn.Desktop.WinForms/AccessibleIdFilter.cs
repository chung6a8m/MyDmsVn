using System;
using System.Globalization;
using System.Windows.Forms;

namespace MyDmsVn.Desktop.WinForms
{
    internal sealed class AccessibleIdFilter : NumericUpDown
    {
        protected override AccessibleObject CreateAccessibilityInstance() => new IdAccessibleObject(this);

        private sealed class IdAccessibleObject : ControlAccessibleObject
        {
            private readonly AccessibleIdFilter _owner;
            public IdAccessibleObject(AccessibleIdFilter owner) : base(owner) { _owner = owner; }
            public override AccessibleRole Role => AccessibleRole.Text;
            public override string? Value
            {
                get => _owner.Value.ToString("F0", CultureInfo.CurrentCulture);
                set
                {
                    if (_owner.IsDisposed || !_owner.Enabled) throw new InvalidOperationException("ID filter is unavailable.");
                    if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var number) ||
                        number < _owner.Minimum || number > _owner.Maximum || decimal.Truncate(number) != number)
                        throw new ArgumentOutOfRangeException(nameof(value));
                    _owner.Value = number;
                }
            }
        }
    }
}
