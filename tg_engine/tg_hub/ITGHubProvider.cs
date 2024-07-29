using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tg_engine.tg_hub.events;

namespace tg_engine.tg_hub
{
    public interface ITGHubProvider
    {
        Task SendEvent(EventBase hevent);
    }
}
