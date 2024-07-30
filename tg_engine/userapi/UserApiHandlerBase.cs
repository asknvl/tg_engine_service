using logger;
using MongoDB.Driver;
using Npgsql.Replication.PgOutput.Messages;
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
using tg_engine.rest.updates;
using tg_engine.tg_hub;
using tg_engine.tg_hub.events;
using TL;
using TL.Layer46;
using WTelegram;
using static System.Net.Mime.MediaTypeNames;

namespace tg_engine.userapi
{
    public class UserApiHandlerBase : IMessageUpdatesObserver
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
        string state_path;

        protected Client user;
        UpdateManager manager;
        ILogger logger;

        string session_directory = Path.Combine("C:", "tgengine", "userpool");
        string updates_directory = Path.Combine("C:", "tgengine", "updates");

        string verifyCode;
        readonly ManualResetEventSlim verifyCodeReady = new();

        IMongoProvider mongoProvider;
        IPostgreProvider postgreProvider;
        ITGHubProvider tgHubProvider;

        protected ChatsProvider chatsProvider;
        #endregion

        public UserApiHandlerBase(Guid account_id, string phone_number, string _2fa_password, string api_id, string api_hash,
                                  IPostgreProvider postgreProvider, IMongoProvider mongoProvider, ITGHubProvider tgHubProvider, ILogger logger)
        {
            this.account_id = account_id;
            tag = $"usrapi ..{phone_number.Substring(phone_number.Length - 5, 4)}";

            this.phone_number = phone_number;
            this._2fa_password = _2fa_password;
            this.api_id = api_id;
            this.api_hash = api_hash;

            this.mongoProvider = mongoProvider;
            this.postgreProvider = postgreProvider;
            this.tgHubProvider = tgHubProvider;

            chatsProvider = new ChatsProvider(postgreProvider);

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
            {
                return message.message;
            }
            else
                throw new Exception("getText: message=null");
        }

        async Task<int> handleTextMessage(UpdateNewMessage unm, UserChat userChat)
        {

            var chat_id = userChat.chat.id;

            bool incomnig = isIncoming(unm);
            var direction = (incomnig) ? "in" : "out";

            int telegram_message_id = unm.message.ID;
            var text = getText(unm);
            var date = unm.message.Date;

            bool is_business_bot_reply = false;
            string? business_bot_username = null;

            if (!incomnig)
            {
                var m = unm.message as Message;

                is_business_bot_reply = m.flags2.HasFlag(Message.Flags2.has_via_business_bot_id);
                if (is_business_bot_reply)
                {
                    var uc = await getUserChat(m.via_business_bot_id);
                    if (uc != null)
                        business_bot_username = uc.user.username;
                }
            }

            var message = new interlayer.messaging.MessageBase()
            {
                chat_id = chat_id,
                direction = direction,
                telegram_id = userChat.user.telegram_id,
                telegram_message_id = telegram_message_id,
                text = text,
                date = date,
                is_business_bot_reply = is_business_bot_reply,
                business_bot_username = business_bot_username
            };

            try
            {
                await mongoProvider.SaveMessage(message);
                //событие о новом сообщении                
                await tgHubProvider.SendEvent(new newMessageEvent(account_id, message));

                await postgreProvider.UpdateTopMessage(chat_id, telegram_message_id, add_unread: incomnig);
            }
            catch (MongoWriteException e) when (e.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                logger.warn(tag, $"Сообщение с telegram_message_id={telegram_message_id} уже существует");
            }

            logger.inf(tag, $"{direction}:{userChat.user.telegram_id} {userChat.user.firstname} {userChat.user.lastname} (text) is_bb={is_business_bot_reply} bb_un={business_bot_username}");

            return telegram_message_id;
        }

        async Task<int> handleMediaDocument(UpdateNewMessage unm, MessageMediaDocument mmd, UserChat userChat)
        {
            Document document = mmd.document as Document;

            var chat_id = userChat.chat.id;
            bool incomnig = isIncoming(unm);
            var direction = (incomnig) ? "in" : "out";
            var telegram_message_id = unm.message.ID;
            var date = unm.message.Date;
            string? text = null;

            if (document != null)
            {
                var sticker = document.attributes.FirstOrDefault(a => a is TL.DocumentAttributeSticker);
                if (sticker != null)
                {
                    var stickerAttr = sticker as TL.DocumentAttributeSticker;
                    text = stickerAttr.alt;
                }
            }

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
                //событие о новом сообщении
                await tgHubProvider.SendEvent(new newMessageEvent(account_id, message));

                await postgreProvider.UpdateTopMessage(chat_id, telegram_message_id, add_unread: incomnig);
            }
            catch (MongoWriteException e) when (e.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                logger.warn(tag, $"Сообщение с telegram_message_id={telegram_message_id} уже существует");
            }

            logger.inf(tag, $"{direction}:{userChat.user.telegram_id} {userChat.user.firstname} {userChat.user.lastname} (media text={message.text})");

            return telegram_message_id;
        }

        async Task handlePhoto(UpdateNewMessage unm, MessageMediaPhoto mmp, UserChat userChat)
        {
            Photo photo = (Photo)mmp.photo;
            Stream stream = new MemoryStream();
            await user.DownloadFileAsync(photo, stream);
            
        }

        async Task handleMessageDeletion(int[] message_ids)
        {
            await mongoProvider.MarkMessagesDeleted(message_ids);
        }

        async Task handleMessageRead(UserChat userChat, string direction, int max_id)
        {
            int unread_count = 0;
            int max_read_id = 0;
            (unread_count, max_read_id) = await mongoProvider.MarkMessagesRead(userChat.chat.id, direction, max_id);

            switch (direction)
            {
                case "in":
                    await postgreProvider.UpdateUnreadCount(userChat.chat.id, unread_count: unread_count, read_inbox_max_id: max_read_id);
                    break;

                case "out":
                    await postgreProvider.UpdateUnreadCount(userChat.chat.id, unread_count: unread_count, read_outbox_max_id: max_read_id);
                    break;
            }
        }
        #endregion

        #region public
        public virtual async Task Start()
        {

            if (!Directory.Exists(updates_directory))
                Directory.CreateDirectory(updates_directory);
            state_path = Path.Combine(updates_directory, $"{phone_number}.state");

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

                //var dialogs = await user.Messages_GetDialogs(limit: 100); //сделать 500 ?
                //dialogs.CollectUsersChats(manager.Users, manager.Chats);

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


        async Task<UserChat> getUserChat(long telegram_id)
        {
            manager.Users.TryGetValue(telegram_id, out var user);

            var tlUser = new telegram_user(user);
            var userChat = await chatsProvider.CollectUserChat(account_id, tlUser);

            return userChat;
        }

        async Task<InputPeer?> getInputPeer(long telegram_id)
        {
            InputPeer? result = null;

            var userChat = await chatsProvider.GetUserChat(account_id, telegram_id);

            var peer = manager.Users.TryGetValue(telegram_id, out var u);
            if (u != null)
                result = u;
            else
            {
                if (userChat != null)
                {
                    result = new InputPeerUser(telegram_id, (long)userChat.user.access_hash);

                }
            }
            return result;
        }

        private async Task User_OnUpdate(Update update)
        {

            UserChat userChat = null;
            long telegram_id = 0;

            switch (update)
            {
                case UpdateNewChannelMessage:
                    break;

                case UpdateReadHistoryInbox uhi:
                    //мы прочли
                    telegram_id = uhi.peer.ID;
                    try
                    {
                        userChat = await getUserChat(telegram_id);
                        await handleMessageRead(userChat, "in", uhi.max_id);
                    }
                    catch (Exception ex)
                    {
                        logger.err(tag, $"UpdateReadHisotryInbox: {telegram_id} {ex.Message} {ex?.InnerException?.Message}");
                    }
                    break;

                case UpdateReadHistoryOutbox uho:
                    //лид прочел
                    telegram_id = uho.peer.ID;
                    try
                    {
                        userChat = await getUserChat(telegram_id);
                        await handleMessageRead(userChat, "out", uho.max_id);
                    }
                    catch (Exception ex)
                    {
                        logger.err(tag, $"UpdateReadHisotryOutbox: {telegram_id} {ex.Message} {ex?.InnerException?.Message}");
                    }
                    break;

                case UpdateDeleteMessages udm:
                    await handleMessageDeletion(udm.messages);
                    break;

                case UpdateNewMessage unm:

                    try
                    {
                        userChat = await getUserChat(unm.message.Peer.ID);

                        if (userChat.is_new)
                        {
                            //событие о новом чате
                            try
                            {
                                await tgHubProvider.SendEvent(new newChatEvent(userChat));
                            }
                            catch (Exception ex)
                            {
                                logger.err(tag, ex.Message);
                            }
                        }

                        logger.inf(tag, $"userChat:{userChat.user.telegram_id} {userChat.user.access_hash} {userChat.user.firstname} {userChat.user.lastname}");

                        var message = unm.message as Message;

                        if (message != null)
                        {

                            switch (message.media)
                            {
                                case null:
                                    await handleTextMessage(unm, userChat);
                                    break;

                                case MessageMediaDocument mmd:
                                    await handleMediaDocument(unm, mmd, userChat);
                                    break;

                                case MessageMediaPhoto mmp:
                                    await handlePhoto(unm, mmp, userChat);
                                    break;

                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        logger.err(tag, ex.Message);
                    }
                    break;
            }

            logger.inf(tag, update.ToString());
        }

        public async Task OnNewMessage(interlayer.messaging.MessageBase message)
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

                    //собыьте о новом сообщении

                    await postgreProvider.UpdateTopMessage(message.chat_id, message.telegram_message_id);

                    logger.inf(tag, $"{message.direction}:{userChat.user.telegram_id} {userChat.user.firstname} {userChat.user.lastname} time={stopwatch.ElapsedMilliseconds} ms");
                }


            }
            catch (Exception ex)
            {
                logger.err(tag, $"OnMessageTX: {ex.Message}");
            }
        }

        public async Task OnNewUpdate(UpdateBase update)
        {
            switch (update)
            {
                case readHistory rh:
                    try
                    {
                        var peer = await getInputPeer(rh.user_telegram_id);
                        await user.ReadHistory(peer);
                    }
                    catch (Exception ex)
                    {
                        logger.err(tag, $"OnNewUpdate readHistory tg_id={update.user_telegram_id} {ex.Message}");
                    }
                    break;
            }

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
            manager.SaveState(state_path);
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
