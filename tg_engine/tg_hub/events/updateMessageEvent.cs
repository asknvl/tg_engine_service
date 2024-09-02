using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using tg_engine.database.postgre.dtos;
using tg_engine.interlayer.messaging;

namespace tg_engine.tg_hub.events
{
    public class updateMessageEvent : newMessageEvent
    {
        [JsonIgnore]
        public override string path => "events/update-message";
        public updateMessageEvent(UserChat userChat, MessageBase message) : base(userChat, message)
        {
        }
    }
}
