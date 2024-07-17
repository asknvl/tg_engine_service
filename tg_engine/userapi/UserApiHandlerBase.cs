using logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tg_engine.database.mongo;
using tg_engine.database.postgre;
using tg_engine.database.postgre.dtos;
using tg_engine.database.postgre.models;
using tg_engine.interlayer.chats;
using tg_engine.interlayer.messaging;
using TL;
using WTelegram;

namespace tg_engine.userapi
{
    public class UserApiHandlerBase : IMessageObserver
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

        public Guid account_id { get; }
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

        IMongoProvider mongoProvider;
        IPostgreProvider postgreProvider;

        protected TGProviderBase tgProvider;
        protected ChatsProvider chatsProvider;
        #endregion

        public UserApiHandlerBase(Guid account_id, string phone_number, string _2fa_password, string api_id, string api_hash, IPostgreProvider postgreProvider, IMongoProvider mongoProvider, ILogger logger)
        {
            this.account_id = account_id;
            tag = $"usrapi ..{phone_number.Substring(phone_number.Length - 5, 4)}";

            this.phone_number = phone_number;
            this._2fa_password = _2fa_password;
            this.api_id = api_id;
            this.api_hash = api_hash;

            this.mongoProvider = mongoProvider;
            this.postgreProvider = postgreProvider;

            chatsProvider = new ChatsProvider(postgreProvider);
            tgProvider = new tg_provider_v0(account_id, postgreProvider, mongoProvider, logger);

            this.logger = logger;            

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
        #endregion

        #region helpers
        bool isIncoming(UpdateNewMessage unm)
        {
            var message = unm.message as Message;
            if (message != null)
                return !message.flags.HasFlag(TL.Message.Flags.out_);
            else
                throw new Exception("isIncoming: message=null");
        }

        string getText(UpdateNewMessage unm)
        {
            var message = unm.message as Message;
            if (message != null)
                return message.message;
            else
                throw new Exception("getText: message=null");
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

                manager.SaveState(state_path);            
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

                        var tlUser = new telegram_user(user);

                        Stopwatch stopwatch = new Stopwatch();
                        stopwatch.Start();

                        var userChat = await chatsProvider.CollectUserChat(account_id, tlUser);

                        logger.inf(tag, $"userChat:{userChat.user.telegram_id} {userChat.user.access_hash} {userChat.user.firstname} {userChat.user.lastname}");

                        var chat_id = userChat.chat.id;
                        var direction = (isIncoming(unm)) ? "in" : "out";
                        var telegram_message_id = unm.message.ID;
                        var text = getText(unm);
                        var date = unm.message.Date;

                        var message = new interlayer.messaging.MessageBase()
                        {
                            chat_id = chat_id,
                            direction = direction,
                            telegram_id = userChat.user.telegram_id,
                            telegram_message_id = telegram_message_id,
                            text = text,
                            date = date
                        };

                        try
                        {
                            await mongoProvider.SaveMessage(message);
                        }
                        catch (Exception e)
                        {
                            logger.warn(tag, $"Сообщение с telegram_message_id={telegram_message_id} уже существует");
                        }

                        stopwatch.Stop();

                        logger.inf(tag, $"{direction}:{userChat.user.telegram_id} {userChat.user.firstname} {userChat.user.lastname} time={stopwatch.ElapsedMilliseconds} ms");

                        
                    } catch (Exception ex)
                    {
                        logger.err(tag, ex.Message);
                    }
                    break;
            }

            logger.inf(tag, update.ToString());
            await Task.CompletedTask;
        }

        public async Task OnMessageTX(interlayer.messaging.MessageBase message)
        {
            try
            {

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                TL.Message result = null;
                var userChat = await chatsProvider.GetUserChat(account_id, message.telegram_id);

                var peer = manager.Users.TryGetValue((long)message.telegram_id, out var u);
                if (u != null)
                {
                    result = await user.SendMessageAsync(u, message.text);
                }
                else
                {                    
                    if (userChat != null)
                    {
                        var upeer = new InputPeerUser(message.telegram_id, (long)userChat.user.access_hash);
                        result = await user.SendMessageAsync(upeer, message.text);
                    }
                }

                if (result != null && userChat != null)
                {
                    message.chat_id = userChat.chat.id;
                    message.direction = "out";
                    message.telegram_message_id = result.ID;
                    message.date = result.Date;

                    await mongoProvider.SaveMessage(message);
                    stopwatch.Stop();

                    logger.inf(tag, $"{message.direction}:{userChat.user.telegram_id} {userChat.user.firstname} {userChat.user.lastname} time={stopwatch.ElapsedMilliseconds} ms");
                }


            } catch (Exception ex)
            {
                logger.err(tag, $"OnMessageTX: {ex.Message}");
            }
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
