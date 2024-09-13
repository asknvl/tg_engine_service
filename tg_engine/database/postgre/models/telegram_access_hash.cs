using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tg_engine.database.postgre.models
{
    public class telegram_access_hash
    {
        public Guid id { get; set; }
        public Guid account_id { get; set; }    
        public long telegram_id { get; set; }
        public long access_hash {  get; set; }  
    }
}
