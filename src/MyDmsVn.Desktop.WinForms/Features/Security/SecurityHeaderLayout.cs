using System;
using System.Linq;
using System.Windows.Forms;

namespace MyDmsVn.Desktop.WinForms.Features.Security;

internal sealed class SecurityHeaderLayout
{
    readonly Panel _identity;
    readonly FlowLayoutPanel _actions;
    bool _updating;

    public SecurityHeaderLayout(Panel identity, FlowLayoutPanel actions)
    {
        _identity = identity;
        _actions = actions;
        _actions.Layout += (_, _) => Update();
        _identity.SizeChanged += (_, _) => Update();
        Update();
    }

    void Update()
    {
        if (_updating) return;
        _updating = true;
        try
        {
            var lastButton = _actions.Controls.Cast<Control>()
                .Where(control => control.Visible)
                .Select(control => control.Bottom)
                .DefaultIfEmpty(29)
                .Max();
            var actionHeight = lastButton + 3;
            var identityHeight = _identity.Padding.Vertical +
                _identity.Controls.Cast<Control>().Where(control => control != _actions && control.Visible).Sum(control => control.Height) +
                actionHeight;
            if (_actions.Height != actionHeight) _actions.Height = actionHeight;
            if (_identity.Height != identityHeight) _identity.Height = identityHeight;
        }
        finally
        {
            _updating = false;
        }
    }
}
