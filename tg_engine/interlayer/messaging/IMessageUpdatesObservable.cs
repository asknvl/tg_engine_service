using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tg_engine.interlayer.messaging
{
    public interface IMessageUpdatesObservable
    {
        public void Add(IMessageUpdatesObserver observer);
        public void Remove(IMessageUpdatesObserver observer);
    }
}
