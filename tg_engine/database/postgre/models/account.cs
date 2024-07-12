using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tg_engine.database.postgre.models
{
    public class account
    {
        [Key]
        public Guid id { get; set; }
        public long telegram_id { get; set; }
        public string phone_number { get; set; }
        public string two_fa { get; set; }
        public string api_id { get; set; }
        public string api_hash { get; set; }
        public int? status_id { get; set; } 

    }
}
