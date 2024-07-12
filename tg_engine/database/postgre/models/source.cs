using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tg_engine.database.postgre.models
{
    public class source
    {
        [Key]
        public Guid id { get; set; }
        public Guid channel_id { get; set; }
        public string source_name { get; set; }
        public int status_id { get; set; }        
    }
}
