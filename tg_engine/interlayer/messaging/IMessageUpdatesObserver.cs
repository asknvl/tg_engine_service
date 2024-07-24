using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tg_engine.rest.updates;

namespace tg_engine.interlayer.messaging
{
    public interface IMessageUpdatesObserver
    {
        Guid account_id { get; }
        Task OnNewMessage(MessageBase message);
        Task OnNewUpdate(UpdateBase update);
    }
}
