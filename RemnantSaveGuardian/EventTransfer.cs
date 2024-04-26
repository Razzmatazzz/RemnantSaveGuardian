using System;

namespace RemnantSaveGuardian
{
    internal class EventTransfer
    {
        internal static event EventHandler<MessageArgs>? Event;
        internal class MessageArgs : EventArgs
        {
            internal MessageArgs(object message)
            {
                Message = message;
            }
            internal object Message { get; set; }

        }
        internal static void Transfer(object s)
        {
            Event?.Invoke(null, new MessageArgs(s));
        }
    }
}
