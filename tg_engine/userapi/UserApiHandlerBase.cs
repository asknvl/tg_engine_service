using logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tg_engine.interlayer.messaging;
using TL;
using WTelegram;

namespace tg_engine.userapi
{
    public class UserApiHandlerBase
    {
        #region properties
        public string phone_number { get; set; }
        public string _2fa_password { get; set; }
        public string api_id { get; set; }
        public string api_hash { get; set; }
        public long tg_id { get; set; }
        public string username { get; set; }

        UserApiStatus _status;
        public UserApiStatus status
        {
            get => _status;
            set
            {
                _status = value;
                StatusChangedEvent?.Invoke(_status);
            }
        }
        #endregion

        #region vars
        string tag;
        protected Client user;
        UpdateManager manager;
        ILogger logger;

        string session_directory = Path.Combine("C:", "tgengine", "userpool");
        string updates_directory = Path.Combine("C:", "tgengine", "updates");

        string verifyCode;
        readonly ManualResetEventSlim verifyCodeReady = new();

        protected TGProviderBase tgProvider;
        #endregion

        public UserApiHandlerBase(string phone_number, string _2fa_password, string api_id, string api_hash, TGProviderBase tgProvider, ILogger logger)
        {
            tag = $"usrapi ..{phone_number.Substring(phone_number.Length - 5, 4)}";

            this.phone_number = phone_number;
            this._2fa_password = _2fa_password;
            this.api_id = api_id;
            this.api_hash = api_hash;

            this.logger = logger;

            this.tgProvider = tgProvider;          

            status = UserApiStatus.inactive;
        }

        #region private
        void processRpcException(RpcException ex)
        {
            switch (ex.Message)
            {
                case "PHONE_NUMBER_BANNED":
                    status = UserApiStatus.banned;
                    break;

                case "SESSION_REVOKED":
                case "AUTH_KEY_UNREGISTERED":
                    status = UserApiStatus.revoked;
                    break;

            }

            logger.err(tag, $"RcpException: {ex.Message}");
        }

        private string config(string what)
        {
            if (!Directory.Exists(session_directory))
                Directory.CreateDirectory(session_directory);

            switch (what)
            {
                case "api_id": return api_id;
                case "api_hash": return api_hash;
                case "session_pathname": return $"{session_directory}/{phone_number}.session";
                case "phone_number": return phone_number;
                case "verification_code":
                    status = UserApiStatus.verification;
                    logger.warn(tag, "Запрос кода верификации");
                    verifyCodeReady.Reset();
                    verifyCodeReady.Wait();
                    return verifyCode;                
                case "password": return _2fa_password;
                default: return null;
            }
        }

        private async void TgProvider_MessageTXRequest(interlayer.messaging.MessageBase message, long? access_hash)
        {
            try
            {

                //var u = await user.Users_GetFullUser(new InputUser((long)message.telegram_id, 0));
                //if (u != null)
                //{
                //    await user.SendMessageAsync(u.users[0], message.text);
                //}

                var peer = manager.Users.TryGetValue((long)message.telegram_id, out var u);
                if (u != null)                
                {
                    await user.SendMessageAsync(u, message.text);                    
                } else
                {
                    var upeer = new InputPeerUser(message.telegram_id, (long)access_hash);
                    await user.SendMessageAsync(upeer, message.text);
                }



            } catch (Exception ex)
            {
                logger.err(tag, $"TgProvider_MessageTXRequest: {ex.Message}");
            }

        }
        #endregion

        #region public
        public virtual async Task Start()
        {

            if (!Directory.Exists(updates_directory))
                Directory.CreateDirectory(updates_directory);
            var state_path = Path.Combine(updates_directory, $"{phone_number}.state");

            try
            {
                if (status == UserApiStatus.active)
                {
                    logger.err(tag, "Уже запущен");
                    return;
                }

                user = new Client(config);
                
                manager = user.WithUpdateManager(User_OnUpdate, state_path);
                await user.LoginUserIfNeeded();


                //var dialogs = await user.Messages_GetAllDialogs();

                //var dialogs = await user.Messages_GetDialogs();
                //dialogs.CollectUsersChats(manager.Users, manager.Chats);


                manager.SaveState(state_path);

                tgProvider.MessageTXRequest -= TgProvider_MessageTXRequest;
                tgProvider.MessageTXRequest += TgProvider_MessageTXRequest;

                status = UserApiStatus.active;
                
            }
            catch (RpcException ex)
            {
                processRpcException(ex);
                status = UserApiStatus.inactive;
                user.Dispose();
                throw;
            }
            catch (Exception ex)
            {
                status = UserApiStatus.inactive;
                user.Dispose();
                throw;
            }            
        }

        private async Task User_OnUpdate(Update update)
        {
            switch (update)
            {
                case UpdateNewChannelMessage:
                    break;

                case UpdateNewMessage unm:

                    try
                    {

                        manager.Users.TryGetValue(unm.message.Peer.ID, out var user);
                        await tgProvider.OnMessageRX(unm, user);

                        //logger.inf(tag, $"NewMessage from: {user.first_name} {user.last_name} {user.username} {user.id}");
                    } catch (Exception ex)
                    {
                        logger.err(tag, ex.Message);
                    }
                    break;
            }

            logger.inf(tag, update.ToString());
            await Task.CompletedTask;
        }
        public void SetVerificationCode(string code)
        {
            logger.user_input(tag, $"Ввод кода верификации {code}");
            verifyCode = code;
            verifyCodeReady.Set();
        }

        public virtual void Stop()
        {
            user?.Dispose();
            verifyCodeReady.Set();
            status = UserApiStatus.inactive;
        }
        #endregion

        #region events        
        public event Action<UserApiStatus> StatusChangedEvent;
        #endregion
    }

    public enum UserApiStatus : int
    {        
        active = 1,        
        banned = 2,
        inactive = 3,
        verification = 4,
        revoked = 5
    }
}
