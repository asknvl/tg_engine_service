using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tg_engine.rest.updates;

namespace tg_engine.business_bot
{
    public class BusinessBotProtocol : IBusinessBotProtocol
    {       
        public string GetCommand(long tg_id, UpdateBase update)
        {
            string command = "";

            switch (update)
            {
                case aiStatus gs:
                    var status = gs.is_active ? "ON" : "OFF";
                    command = $"AI:STATUS:{tg_id}:{status}:MANUAL";
                    break;
            }
            return command;
        }
    }
}
