using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tg_engine.tg_hub.events
{
    public abstract class EventBase
    {        
        public abstract EventType type { get; }
        public abstract string GetSerialized();        
    }

    public enum EventType : int
    {
        newChat = 0,
    }
}
