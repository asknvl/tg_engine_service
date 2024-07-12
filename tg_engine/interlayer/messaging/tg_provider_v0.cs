using logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tg_engine.database.mongo;
using tg_engine.database.postgre;

namespace tg_engine.interlayer.messaging
{
    public class tg_provider_v0 : TGProviderBase
    {
        public tg_provider_v0(Guid account_id, IPostgreProvider postgreProvider, IMongoProvider mongoProvider, ILogger logger) : base(account_id, postgreProvider, mongoProvider, logger)
        {
        }
    }
}
