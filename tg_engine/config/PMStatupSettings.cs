using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tg_engine.database.postgre.models;

namespace tg_engine.config
{
    public class PMStatupSettings
    {
        public string source { get; set; }
        public account account { get; set; }   
    }
}
