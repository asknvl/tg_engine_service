using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tg_engine.interlayer.messaging
{
    public interface IMessageObserver
    {
        Guid account_id { get; }
        Task OnMessageTX(MessageBase message);
    }
}
