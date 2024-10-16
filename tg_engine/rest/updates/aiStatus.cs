using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tg_engine.rest.updates
{
    //public class aiStatus : UpdateBase
    //{
    //    public bool is_active { get; set; }
    //}

    public class aiStatus : UpdateBase
    {
        public int status { get; set; }
    }

    public enum AIStatuses
    {
        off = 0,
        on = 1,
        done = 2
    }
}
