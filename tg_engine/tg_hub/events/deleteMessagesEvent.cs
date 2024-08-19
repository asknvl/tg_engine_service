using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using tg_engine.database.postgre.dtos;

namespace tg_engine.tg_hub.events
{
    public class deleteMessagesEvent : EventBase
    {
        [JsonIgnore]
        public override string path => "events/delete-messages";
        public deleteMessagesEvent(UserChat userChat, int[] ids) : base(userChat)
        {
            data = ids;
        }
    }    
}
