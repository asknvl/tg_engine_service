using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using tg_engine.database.postgre.dtos;

namespace tg_engine.tg_hub.events
{
    public class updateChatEvent : newChatEvent
    {
        [JsonIgnore]
        public override string path => "events/update-chat";
        public updateChatEvent(UserChat userChat, Guid source_id, string source_name) : base(userChat, source_id, source_name)
        {
        }
    }
}
