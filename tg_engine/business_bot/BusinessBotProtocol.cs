using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tg_engine.rest.updates;
using static MediaInfo.NativeMethods;

namespace tg_engine.business_bot
{
    public class BusinessBotProtocol : IBusinessBotProtocol
    {
        //public string GetCommand(long tg_id, UpdateBase update)
        //{
        //    string command = "";

        //    switch (update)
        //    {
        //        case aiStatus gs:
        //            var status = gs.is_active ? "ON" : "OFF";
        //            command = $"AI:STATUS:{tg_id}:{status}:MANUAL";
        //            break;
        //    }
        //    return command;
        //}

        public string GetCommand(long tg_id, UpdateBase update)
        {
            string command = "";

            switch (update)
            {
                case aiStatus st:

                    if (Enum.IsDefined(typeof(AIStatuses), st.status))
                    {
                        var statusEnum = (AIStatuses)st.status;
                        switch (statusEnum)
                        {
                            case AIStatuses.off:
                                command = $"AI:STATUS:{tg_id}:OFF:MANUAL";
                                break;

                            case AIStatuses.on:
                                command = $"AI:STATUS:{tg_id}:ON:MANUAL";
                                break;

                            default:
                                break;
                        }
                                                
                    }
                    break;
            }
            return command;
        }
    }
}
