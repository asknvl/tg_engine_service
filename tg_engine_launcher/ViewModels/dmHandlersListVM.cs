using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using tg_engine.dm;
using tg_engine_launcher.Models.rest;

namespace tg_engine_launcher.ViewModels
{
    public class dmHandlersListVM : ViewModelBase
    {
        #region vars
        ITGEnginApi enginApi;
        Timer refreshTimer;
        #endregion

        #region properties
        public ObservableCollection<dmHandlerVM> DMHandlers { get; } = new();

        dmHandlerVM selectedDM;
        public dmHandlerVM SelectedDM
        {
            get => selectedDM;
            set => this.RaiseAndSetIfChanged(ref selectedDM, value);
        }
        #endregion

        #region commands
        public ReactiveCommand<Unit, Unit> startAllCmd { get; }
        public ReactiveCommand<Unit, Unit> stopAllCmd { get; }
        public ReactiveCommand<Unit, Unit> refreshCmd { get; }
        #endregion

        public dmHandlersListVM() {

            enginApi = new TGEngineApi("http://localhost:8080");

            #region commands
            startAllCmd = ReactiveCommand.CreateFromTask(async () => {
                List<Guid> ids = new();
                if (SelectedDM != null)
                    ids.Add(SelectedDM.Id);
                try
                {
                    await enginApi.StartDMHandlers(ids.ToArray());
                } catch (Exception ex)
                {
                    showError(ex.Message);
                }
            });

            stopAllCmd = ReactiveCommand.CreateFromTask(async () => {
                List<Guid> ids = new();
                if (SelectedDM != null)
                    ids.Add(SelectedDM.Id);
                try
                {
                    await enginApi.StopDMHandlers(ids.ToArray());
                }
                catch (Exception ex)
                {
                    showError(ex.Message);
                }
            });
            refreshCmd = ReactiveCommand.CreateFromTask(async () => {
                await refresh();
            });
            #endregion

            refreshTimer = new Timer();
            refreshTimer.Elapsed += (e, s) => {
                refresh();
            };
            refreshTimer.Interval = 2000;
            refreshTimer.AutoReset = true;            
            refreshTimer.Start();
        }

        #region private
        async Task refresh()
        {
            try
            {
                var dmHandlers = await enginApi.GetAllDMHandlers();

                foreach (var item in dmHandlers) {

                    var found = DMHandlers.FirstOrDefault(d => d.Id == item.id);
                    if (found == null)
                    {
                        var dm = new dmHandlerVM(enginApi)
                        {

                            Id = item.id,
                            Source = item.source,
                            PhoneNumber = item.phone_number,
                            Status = (dmHandlerStatus)item.status
                        };

                        DMHandlers.Add(dm);
                    } else
                    {
                        found.Source = item.source;
                        found.PhoneNumber = item.phone_number;
                        found.Status = (dmHandlerStatus)item.status;
                    }                    
                }

            } catch (Exception ex)
            {
                showError(ex.Message);
            }
        }
        #endregion
    }
}
