using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tg_engine.rest.updates
{
    public class deleteMessage : UpdateBase
    {
        public List<int> ids { get; set; } = new();
    }
}
