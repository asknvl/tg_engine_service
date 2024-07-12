using logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tg_engine.interlayer.messaging;

namespace tg_engine.userapi
{
    public class UserApiFactory : IUserApiFactory
    {
        #region vars
        ILogger logger;
        string api_id;
        string api_hash;
        #endregion
        public UserApiFactory(string api_id, string api_hash, ILogger logger)
        {
            this.logger = logger;
            this.api_id = api_id;
            this.api_hash = api_hash;
        }
        public UserApiHandlerBase Get(string phone_number, string _2fa_password, TGProviderBase tgProvider)
        {
            return new userapi_handler_v0(phone_number, _2fa_password, api_id, api_hash, tgProvider, logger );
        }
    }
}
