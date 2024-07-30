using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tg_engine.tg_hub.events
{
    public abstract class EventBase
    {
        public abstract string path { get; }
        public Guid account_id { get; protected set; }
        public Guid chat_id { get; protected set; }
        public long telegram_id { get; protected set; }
        public Object data { get; protected set; }
        public string GetSerialized() => JsonConvert.SerializeObject(this);
    }   
}
