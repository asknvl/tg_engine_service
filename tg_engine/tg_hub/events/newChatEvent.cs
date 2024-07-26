using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tg_engine.database.postgre.models;

namespace tg_engine.tg_hub.events
{
    public class newChatEvent : EventBase
    {
        public override EventType type => EventType.newChat;        
        public Guid account_id { get; set; }
        public Guid chat_id {  get; set; }  
        public long telegram_id { get; set; }   
        public string? firstname { get; set; }
        public string? lastname { get; set; }   
        public string? username { get; set; }   
        public int uread_count { get; set; }    

        public override string GetSerialized()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
