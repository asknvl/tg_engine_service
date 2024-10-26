using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tg_engine.rest.updates
{
    public class sendReaction : UpdateBase
    {
        public int telegram_message_id { get; set; }
        public string reaction { get; set; }
    }
}
