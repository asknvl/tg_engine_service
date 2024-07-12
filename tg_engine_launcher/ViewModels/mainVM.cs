using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using tg_engine;

namespace tg_engine_launcher.ViewModels
{
    public class mainVM : ViewModelBase
    {
        #region properties        
        public string Title { get => $"tg_engine_launcher {Assembly.GetExecutingAssembly().GetName().Version}"; }

        loggerVM logger;
        public loggerVM Logger
        {
            get => logger;
            set => this.RaiseAndSetIfChanged(ref logger, value);    
        }

        dmHandlersListVM dmHandlers;
        public dmHandlersListVM DMHandlers
        {
            get => dmHandlers;
            set => this.RaiseAndSetIfChanged(ref  dmHandlers, value);   
        }
        #endregion

        public mainVM()
        {
            Logger = new loggerVM();    

            tg_engine_v0 engine = new tg_engine_v0(Logger);
            dmHandlers = new dmHandlersListVM();

            engine.Run();
        }

    }
}
