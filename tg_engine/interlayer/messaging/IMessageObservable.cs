using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tg_engine.interlayer.messaging
{
    public interface IMessageObservable
    {
        public void Add(IMessageObserver observer);
        public void Remove(IMessageObserver observer);
    }
}
