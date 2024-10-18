using logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tg_engine.database.mongo;
using tg_engine.database.postgre;
using tg_engine.interlayer.messaging;
using tg_engine.s3;
using tg_engine.tg_hub;
using tg_engine.translator;

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
        IS3Provider s3Provider;
        ITranslator translator;
        #endregion
        public UserApiFactory(string api_id, string api_hash, IPostgreProvider postgreProvider, IMongoProvider mongoProvider, ITGHubProvider tgHubProvider, IS3Provider s3Provider, ITranslator translator, ILogger logger)
        {
            this.logger = logger;
            this.api_id = api_id;
            this.api_hash = api_hash;

            this.postgreProvider = postgreProvider;
            this.mongoProvider = mongoProvider;
            this.tgHubProvider = tgHubProvider;
            this.s3Provider = s3Provider;
            this.translator = translator;
        }
        public UserApiHandlerBase Get(Guid account_id, Guid source_id, string source_name, Guid direction_id, string phone_number, string _2fa_password)
        {
            return new userapi_handler_v0(account_id, source_id, source_name, direction_id, phone_number, _2fa_password, api_id, api_hash, postgreProvider, mongoProvider, tgHubProvider, s3Provider, translator, logger);
        }
    }
}
