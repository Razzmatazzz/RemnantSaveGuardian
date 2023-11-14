using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;

namespace RemnantSaveGuardian
{
    internal class EventTransfer
    {
        internal static event EventHandler<MessageArgs>? Event;
        internal class MessageArgs : EventArgs
        {
            internal MessageArgs(object message)
            {
                _message = message;
            }
            internal object _message { get; set; }

        }
        internal static void Transfer(object s)
        {
            Event?.Invoke(null, new MessageArgs(s));
        }
    }
}
