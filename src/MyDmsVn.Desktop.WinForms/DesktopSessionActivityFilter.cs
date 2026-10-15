using System.Windows.Forms;
using MyDmsVn.Desktop.Application;

namespace MyDmsVn.Desktop.WinForms
{
    internal sealed class DesktopSessionActivityFilter : IMessageFilter
    {
        private readonly IDesktopSessionActivity _activity;

        public DesktopSessionActivityFilter(IDesktopSessionActivity activity)
        {
            _activity = activity;
        }

        public bool PreFilterMessage(ref Message message)
        {
            const int WmKeyFirst = 0x0100;
            const int WmKeyLast = 0x0109;
            const int WmMouseFirst = 0x0200;
            const int WmMouseLast = 0x020E;
            if ((message.Msg >= WmKeyFirst && message.Msg <= WmKeyLast) ||
                (message.Msg >= WmMouseFirst && message.Msg <= WmMouseLast))
            {
                _activity.RecordUserActivity();
            }

            return false;
        }
    }
}
