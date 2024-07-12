using ReactiveUI;
using System;
using System.Reactive;
using tg_engine_launcher.Models.rest;

namespace tg_engine_launcher.ViewModels
{
    public class dmHandlerVM : ViewModelBase
    {
        #region properties       
        public Guid Id { get; set; }

        string source;
        public string Source
        {
            get => source;
            set => this.RaiseAndSetIfChanged(ref source, value);
        }

        string phoneNumber;
        public string PhoneNumber
        {
            get => phoneNumber;
            set => this.RaiseAndSetIfChanged(ref phoneNumber, value);   
        }

        dmHandlerStatus status;
        public dmHandlerStatus Status
        {
            get => status;
            set
            {
                NeedCode = value == dmHandlerStatus.verification;
                this.RaiseAndSetIfChanged(ref status, value);
            }
        }

        string code;
        public string Code
        {
            get => code;
            set => this.RaiseAndSetIfChanged(ref code, value);
        }

        bool needCode;
        public bool NeedCode
        {
            get => needCode;
            set => this.RaiseAndSetIfChanged(ref needCode, value);  
        }
        #endregion

        #region commands        
        public ReactiveCommand<Unit, Unit> setCodeCmd { get; }
        #endregion

        public dmHandlerVM(ITGEnginApi enginApi) {

            #region commands
            setCodeCmd = ReactiveCommand.CreateFromTask(async () => {
               try
                {
                    await enginApi.SetVerificationCode(Id, code); 

                } catch (Exception ex)
                {
                    showError(ex.Message);
                }         
            });
            #endregion

        }
    }

    public enum dmHandlerStatus : int
    {
        active = 1,
        banned = 2,
        inactive = 3,
        verification = 4,
        revoked = 5
    }
}
