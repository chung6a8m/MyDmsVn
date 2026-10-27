using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;

namespace MyDmsVn.Desktop.WinForms
{
    internal sealed class AccessibleLookupBox : BootstrapLookupBox
    {
        protected override AccessibleObject CreateAccessibilityInstance() => new LookupAccessibleObject(this);

        private sealed class LookupAccessibleObject : ControlAccessibleObject
        {
            private readonly AccessibleLookupBox _owner;
            public LookupAccessibleObject(AccessibleLookupBox owner) : base(owner) { _owner = owner; }
            public override string? Help => _owner.HasPendingText ? "Pending selection"
                : _owner.SelectedValue == null ? "No selection" : "Selected: " + _owner.CommittedDisplayText;
        }
    }
}
