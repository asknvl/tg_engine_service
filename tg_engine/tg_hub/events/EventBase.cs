using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tg_engine.database.postgre.dtos;

namespace tg_engine.tg_hub.events
{
    public abstract class EventBase
    {
        [JsonIgnore]
        public abstract string path { get; }
        public Guid account_id { get; protected set; }
        public Guid chat_id { get; protected set; }        
        public Guid telegram_user_id { get; protected set; }
        public Object data { get; protected set; }
        public string GetSerialized() => JsonConvert.SerializeObject(this);

        public EventBase(UserChat userChat)
        {
            account_id = userChat.chat.account_id;
            chat_id = userChat.chat.id;
            telegram_user_id = userChat.chat.telegram_user_id;
        }
    }   
}
