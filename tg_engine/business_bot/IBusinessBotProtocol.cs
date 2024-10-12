using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tg_engine.rest.updates;

namespace tg_engine.business_bot
{
    public interface IBusinessBotProtocol
    {
        string GetCommand(long tg_id, UpdateBase update);
    }
}
