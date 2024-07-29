using logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tg_engine.database.mongo;
using tg_engine.database.postgre;
using tg_engine.interlayer.messaging;
using tg_engine.tg_hub;

namespace tg_engine.userapi
{
    public class userapi_handler_v0 : UserApiHandlerBase
    {
        public userapi_handler_v0(Guid account_id,
                                  string phone_number,
                                  string _2fa_password,
                                  string api_id,
                                  string api_hash,
                                  IPostgreProvider postgreProvider,
                                  IMongoProvider mongoProvider,
                                  ITGHubProvider tgHubProvider,
                                  ILogger logger) : 
                                                         base(account_id,
                                                         phone_number,
                                                         _2fa_password,
                                                         api_id,
                                                         api_hash,
                                                         postgreProvider,
                                                         mongoProvider,
                                                         tgHubProvider,
                                                         logger)
        {
        }
    }
}
