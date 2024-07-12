using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tg_engine.database.postgre.models
{
    public class channel_account
    {
        [Key]
        public Guid id { get; set; }
        public Guid channel_id { get; set; }
        public Guid account_id { get; set; }        
    }
}
