using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using tg_engine.database.postgre.dtos;

namespace tg_engine.tg_hub.events
{
    public class readHistoryEvent : EventBase
    {
        [JsonIgnore]
        public override string path => "events/read-history";
        public readHistoryEvent(UserChat userChat, string direction, int max_id) : base(userChat)
        {
            data = new readHistoryData()
            {
                direction = direction,
                max_id = max_id
            };
        }
    }

    class readHistoryData {
        public string direction { get; set; }
        public int max_id { get; set; }
    }
}
