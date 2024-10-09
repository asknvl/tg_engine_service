using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tg_engine.database.postgre.dtos;

namespace tg_engine.tg_hub.events
{
    public class gptStatusEvent : EventBase
    {
        public override string path => "events/gpt-status";
        public gptStatusEvent(Guid account_id, Guid chat_id, bool status) : base(account_id, chat_id)
        {
            data = new gptStatusData(status);
        }

    }

    class gptStatusData
    {
        public bool status { get; set; }
        public gptStatusData(bool status)
        {
            this.status = status;
        }
    }
}
