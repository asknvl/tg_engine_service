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
        public Guid chat_id { get; set; }
        public Guid telegram_user_id {  get; set; }         
    }    
}
