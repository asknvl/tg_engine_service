using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tg_engine.interlayer.messaging;

namespace tg_engine.userapi
{
    public interface IUserApiFactory
    {
        UserApiHandlerBase Get(string phone_number, string _2fa_password, TGProviderBase tgProvider);
    }
}
