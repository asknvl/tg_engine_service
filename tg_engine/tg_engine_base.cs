using logger;
using System.Diagnostics;
using System.Reflection;
using tg_engine.config;
using tg_engine.database.mongo;
using tg_engine.database.postgre;
using tg_engine.dm;
using tg_engine.rest;

namespace tg_engine
{
    public class tg_engine_base
    {
        #region const
        const string tag = "tgengn";
        #endregion

        #region vars
        ILogger logger;                
        IPostgreProvider postgreProvider;
        IMongoProvider mongoProvider;
        IRestService restService;
        #endregion

        #region properties
        public Version Version
        { 
            get => Assembly.GetExecutingAssembly().GetName().Version;
        }
        public List<DMHandlerBase> DMHandlers { get; } = new();

        public bool IsActive { get; set; }
        #endregion

        public tg_engine_base(ILogger logger)
        {
            this.logger = logger;

            #region dependencies              
            #endregion
        }

        #region private
        async Task initDMhandlers(List<DMStartupSettings> dmStartupSettings, IPostgreProvider postgreProvider, IMongoProvider mongoProvider)
        {
            foreach (var settings in dmStartupSettings)
            {
                Debug.WriteLine($"{settings.source} {settings.account.phone_number}");

                var found = DMHandlers.FirstOrDefault(d => d.settings.account.id == settings.account.id);
                if (found == null)
                {
                    var dm = new DMHandlerBase(settings, postgreProvider, mongoProvider, logger);
                    DMHandlers.Add(dm);
                }
            }
        }
        async Task initService()
        {
            try
            {
                var vars = variables.getInstance();

                restService = new RestService(logger, vars.tg_engine_variables.settings_rest);
                restService.RequestProcessors.Add(new EngineControlRequestProcessor(this));
                restService.Listen();

                postgreProvider = new PostgreProvider(vars.tg_engine_variables.accounts_settings_db);
                var dMStartupSettings  = await postgreProvider.GetStatupData();

                mongoProvider = new MongoProvider(vars.tg_engine_variables.messaging_settings_db);

                await initDMhandlers(dMStartupSettings, postgreProvider, mongoProvider);

                logger?.inf_urgent(tag, $"Инициализация выполнена");

            } catch (Exception ex)
            {
                throw new Exception($"Не удалось выполнить инициализацию сервиса {ex.Message}");
            }
        }
        #endregion

        #region public
        public virtual async Task Run()
        {
            try
            {
                if (IsActive)
                    throw new Exception("Сервис уже запущен");

                await initService();
                //await ToggleDMHandlers(null, true);

            } catch (Exception ex)
            {
                logger?.err(tag, $"Не удалось запустить сервис {ex.Message}");
                return;
            }

            await Task.CompletedTask;
            IsActive = true;
            logger?.inf_urgent(tag, $"Запуск выполнен, вер. {Version}");
        }
        public virtual async Task Stop()
        {
            await Task.CompletedTask;
            logger.warn(tag, "Cервис остановлен");
        }
        public virtual async Task ToggleDMHandlers(List<Guid> guids, bool state)
        {
            var dMStartupSettings = await postgreProvider.GetStatupData();
            await initDMhandlers(dMStartupSettings, postgreProvider, mongoProvider);

            if (guids == null || guids.Count == 0)
            {
                foreach (var dm in DMHandlers)
                {
                    if (state)
                        dm.Start();
                    else
                        dm.Stop();
                }
            } else
            {
                foreach (var guid in guids)
                {
                    var dm = DMHandlers.FirstOrDefault(d => d.settings.account.id == guid);
                    if (dm != null)
                    {
                        if (state)
                            dm.Start();
                        else
                            dm.Stop();
                    }
                }
            }
        }
        #endregion        
    }
}
