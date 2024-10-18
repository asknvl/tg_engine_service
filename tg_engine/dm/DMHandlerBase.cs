using logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tg_engine.database.mongo;
using tg_engine.database.postgre;
using tg_engine.database.postgre.models;
using tg_engine.interlayer.messaging;
using tg_engine.s3;
using tg_engine.tg_hub;
using tg_engine.translator;
using tg_engine.userapi;

namespace tg_engine.dm
{
    public class DMHandlerBase
    {
        #region vars
        IUserApiFactory userApiFactory;
        IPostgreProvider postgreProvider;
        IMongoProvider mongoProvider;
        ITGHubProvider tgHubProvider;
        ILogger logger;
        string tag;
        #endregion

        #region properties                
        public DMStartupSettings settings { get; private set; }
        public UserApiHandlerBase user { get; private set; }                
        public DMHandlerStatus status { get; private set; }
        #endregion

        public DMHandlerBase(DMStartupSettings settings, IPostgreProvider postgreProvider, IMongoProvider mongoProvider, ITGHubProvider tgHubProvider, IS3Provider s3Provider, ITranslator translator, ILogger logger)
        {
            tag = $"dm {settings.source_name}";

            this.postgreProvider = postgreProvider;
            this.mongoProvider = mongoProvider;
            this.tgHubProvider = tgHubProvider;

            this.settings = settings;
            this.logger = logger;

            userApiFactory = new UserApiFactory(settings.account.api_id, settings.account.api_hash, postgreProvider, mongoProvider, tgHubProvider, s3Provider, translator, logger);

            user = userApiFactory.Get(settings.account.id, settings.source_id, settings.source_name, settings.direction_id, settings.account.phone_number, settings.account.two_fa);
            user.StatusChangedEvent += User_StatusChangedEvent;

            status = DMHandlerStatus.inactive;
        }

        #region private
        private void User_StatusChangedEvent(UserApiStatus _status)
        {
            switch (_status)
            {
                case UserApiStatus.active:
                    status = DMHandlerStatus.active;
                    break;

                case UserApiStatus.verification:
                    status = DMHandlerStatus.verification;
                    break;

                case UserApiStatus.banned:
                    status = DMHandlerStatus.banned;
                    break;

                case UserApiStatus.revoked:
                    status = DMHandlerStatus.inactive;
                    break;                
            }
        }       
        #endregion

        #region public
        public virtual async Task Start()
        {
            logger.user_input(tag, $"Запрос на запуск");

            if (status == DMHandlerStatus.active)
            {
                logger.err($"{tag}", "Уже запущен");
                return;
            }                

            try
            {
                await user.Start();

            } catch (Exception ex)
            {
                status = DMHandlerStatus.inactive;
                logger.err($"{tag}", $"Запуск не выполнен {ex.Message}");
                return;
            }
            
            status = DMHandlerStatus.active;
            logger.inf_urgent($"{tag}", "Запуск выполнен");
        }
        public virtual void SetVerificationCode(string code)
        {
            user.SetVerificationCode(code);
        }

        public virtual async void Stop()
        {
            logger.user_input(tag, $"Запрос на остановку");
            user.Stop();  
            status = DMHandlerStatus.inactive;
            logger.warn($"{tag}", "Остановлен");
        }
        #endregion

    }

    public enum DMHandlerStatus
    {
        active = 1,
        banned = 2,
        inactive = 3,
        verification = 4,
        revoked = 5,        
    }
}
