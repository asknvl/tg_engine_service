using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tg_engine.database.postgre.models;

namespace tg_engine.dm
{
    public class DMStartupSettings
    {
        public Guid source_id { get; set; }
        public string source_name {  get; set; }         
        public account account { get; set; }
    }
}
