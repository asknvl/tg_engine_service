using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using tg_engine.database.postgre.dtos;
using tg_engine.messaging_scheduled;

namespace tg_engine.tg_hub.events
{
    public class sheduledMessageEvent : EventBase
    {
        [JsonIgnore]
        public override string path => "events/sheduled-messages";

        public sheduledMessageEvent(ScheduledMessage scheduled, bool is_delivered) : base(scheduled.account_id, scheduled.chat_id)
        {
            data = new scheduledMessageData()
            {
                id = scheduled.id,
                is_delivered = is_delivered
            };
        }        
    }

    public class scheduledMessageData
    {
        public string id { get; set; }
        public bool is_delivered { get; set; }  
    }
}
