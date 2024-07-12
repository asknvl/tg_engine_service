using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tg_engine.rest
{
    public interface IRestService
    {
        public List<IRequestProcessor> RequestProcessors { get; }
        void Listen();
    }
}
