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
        public deleteMessagesEvent(Guid account_id, Guid chat_id, int[] ids) : base(account_id, chat_id)
        {
            data = new idData(ids);
        }
    }    

    class idData
    {
        public int[] ids { get; set; }
        public idData(int[] ids)
        {
            this.ids = ids;
        }
    }
}
