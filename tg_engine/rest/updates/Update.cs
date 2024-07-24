using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tg_engine.rest.updates
{
    public class UpdateBase
    {
        public Guid account_id { get; set; }
        public long user_telegram_id { get; set; }        
        public UpdateType type { get; set; }
    }

    public enum UpdateType : int
    {
        readHistory = 100
    }
}
