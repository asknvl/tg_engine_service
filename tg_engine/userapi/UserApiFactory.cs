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
    public class UserApiFactory : IUserApiFactory
    {
        #region vars
        ILogger logger;
        string api_id;
        string api_hash;
        IPostgreProvider postgreProvider;
        IMongoProvider mongoProvider;
        ITGHubProvider tgHubProvider;
        #endregion
        public UserApiFactory(string api_id, string api_hash, IPostgreProvider postgreProvider, IMongoProvider mongoProvider, ITGHubProvider tgHubProvider, ILogger logger)
        {
            this.logger = logger;
            this.api_id = api_id;
            this.api_hash = api_hash;

            this.postgreProvider = postgreProvider;
            this.mongoProvider = mongoProvider;
            this.tgHubProvider = tgHubProvider;
        }
        public UserApiHandlerBase Get(Guid account_id, string phone_number, string _2fa_password)
        {
            return new userapi_handler_v0(account_id, phone_number, _2fa_password, api_id, api_hash, postgreProvider, mongoProvider, tgHubProvider, logger);
        }
    }
}
