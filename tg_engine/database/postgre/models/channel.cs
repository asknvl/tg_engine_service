using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tg_engine.database.postgre.models
{
    public class channel
    {
        [Key]
        public Guid id { get; set; }
        public long telegram_id { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public int status_id { get; set; }        
    }

}
