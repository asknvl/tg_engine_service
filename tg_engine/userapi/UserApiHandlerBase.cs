﻿using logger;
using MediaInfo;
using MongoDB.Bson.Serialization.Serializers;
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
using tg_engine.s3;
using tg_engine.tg_hub;
using tg_engine.tg_hub.events;
using TL;
using WTelegram;
using static System.Net.Mime.MediaTypeNames;
using static tg_engine.rest.MessageUpdatesRequestProcessor;
 

using IL = tg_engine.interlayer.messaging;

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
        public Guid source_id { get; }
        public string source_name { get; }
        #endregion

        #region vars
        string tag;
        string state_path;

        protected Client client;
        UpdateManager manager;
        ILogger logger;

        string session_directory = Path.Combine("C:", "tgengine", "userpool");
        string updates_directory = Path.Combine("C:", "tgengine", "updates");

        string verifyCode;
        readonly ManualResetEventSlim verifyCodeReady = new();

        IMongoProvider mongoProvider;
        IPostgreProvider postgreProvider;
        ITGHubProvider tgHubProvider;

        IS3Provider s3Provider;

        protected ChatsProvider chatsProvider;
        MessageConstructor messageConstructor = new MessageConstructor();
        #endregion

        public UserApiHandlerBase(Guid account_id, Guid source_id, string source_name, string phone_number, string _2fa_password, string api_id, string api_hash,
                                  IPostgreProvider postgreProvider, IMongoProvider mongoProvider, ITGHubProvider tgHubProvider, IS3Provider s3Provider, ILogger logger)
        {
            this.account_id = account_id;
            this.source_id = source_id;
            this.source_name = source_name;

            tag = $"usrapi ..{phone_number.Substring(phone_number.Length - 5, 4)}";

            this.phone_number = phone_number;
            this._2fa_password = _2fa_password;
            this.api_id = api_id;
            this.api_hash = api_hash;

            this.mongoProvider = mongoProvider;
            this.postgreProvider = postgreProvider;
            this.tgHubProvider = tgHubProvider;
            this.s3Provider = s3Provider;   

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
        #endregion

        #region updates
        async Task<IL.MessageBase> handleTextMessage(UpdateNewMessage unm, UserChat userChat)
        {
            var message = await messageConstructor.Text(userChat, unm, getUserChat);          
            return message;
        }
        async Task<IL.MessageBase> handleMediaDocument(UpdateNewMessage unm, MessageMediaDocument mmd, UserChat userChat)
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
                switch (document.mime_type)
                {
                    case "application/x-tgsticker":
                        var sticker = document.attributes.FirstOrDefault(a => a is TL.DocumentAttributeSticker);
                        if (sticker != null)
                        {
                            var stickerAttr = sticker as TL.DocumentAttributeSticker;
                            text = stickerAttr.alt;
                        }
                        break;

                    case "image/jpeg":                        
                        break;

                    case "video/mp4":

                        DocumentAttributeVideo videoAttr = new DocumentAttributeVideo();

                        var video = document.attributes.FirstOrDefault(a => a is TL.DocumentAttributeVideo);
                        if (video != null)
                        {
                            videoAttr = video as TL.DocumentAttributeVideo;
                            bool is_round = videoAttr.flags.HasFlag(DocumentAttributeVideo.Flags.round_message);
                        }

                        MemoryStream stream = new MemoryStream();
                        await client.DownloadFileAsync(document, stream);
                        byte[] bytes = stream.ToArray();
                        using (var news = new MemoryStream(bytes))
                        {
                            var u = await client.UploadFileAsync(news, $"{document.Filename}");
                            //var upeer = new InputPeerUser(unm.message.Peer.ID, (long)userChat.user.access_hash);
                            //var result = await user.SendMediaAsync(upeer, "123", u, mimeType: s.ToString());

                            //var media = new MediaInfoWrapper(news);

                            var doc = new InputMediaUploadedDocument()
                            {
                                file = u,
                                mime_type = "video/mp4",
                                attributes = new[] {
                                    new DocumentAttributeVideo {
                                        duration = videoAttr.duration,
                                        w = videoAttr.w,
                                        h = videoAttr.h,
                                        flags = DocumentAttributeVideo.Flags.supports_streaming | DocumentAttributeVideo.Flags.round_message
                                    }
                                }                                
                            };

                            //InputMediaUploadedDocument d = new InputMediaUploadedDocument()
                            //{
                            //    file = new InputFile()
                            //    {
                            //        id = u.ID,
                            //        Parts = 1                                    
                            //    },
                            //    mime_type = "video/mp4",
                            //    attributes = new[] {
                            //        new DocumentAttributeVideo {
                            //            duration = videoAttr.duration,
                            //            w = videoAttr.w,
                            //            h = videoAttr.h,
                            //            flags = DocumentAttributeVideo.Flags.supports_streaming | DocumentAttributeVideo.Flags.round_message
                            //        }
                            //    }
                            //};


                            
                            //await user.SendMessageAsync(upeer, "", doc);
                            //await user.SendMessageAsync(upeer, "", d);

                        }
                        break;

                    case "":
                        break;
                }


            }

            var message = new IL.MessageBase()
            {
                chat_id = chat_id,
                direction = direction,
                telegram_id = userChat.user.telegram_id,
                telegram_message_id = telegram_message_id,
                text = text,
                date = date
            };         

            return message;
        }
        async Task<IL.MessageBase> handleImage(UpdateNewMessage unm, MessageMediaPhoto mmp, UserChat userChat)
        {

            IL.MessageBase message = null;

            Photo photo = (Photo)mmp.photo;

            if (photo != null)
            {

                MemoryStream stream = new MemoryStream();
                await client.DownloadFileAsync(photo, stream);
                var storage_id = await s3Provider.Upload(stream.ToArray());

                message = await messageConstructor.Image(userChat, unm, getUserChat, photo, storage_id);
            }

            return message;           
        }
        //TODO добавить удаление всего чата, нужно поменить чат как удаленный и прокинуть ивент
        async Task handleMessageDeletion(int[] message_ids)
        {
            await mongoProvider.MarkMessagesDeleted(message_ids);
        }
        async Task handleMessageRead(UserChat userChat, string direction, int max_id)
        {
            int unread_count = 0;
            int max_read_id = 0;
            telegram_chat updatedChat = null;

            (unread_count, max_read_id) = await mongoProvider.MarkMessagesRead(userChat.chat.id, direction, max_id);

            switch (direction)
            {
                case "in":
                    updatedChat = await postgreProvider.UpdateUnreadCount(userChat.chat.id, unread_count: unread_count, read_inbox_max_id: max_read_id);
                    break;

                case "out":
                    updatedChat = await postgreProvider.UpdateUnreadCount(userChat.chat.id, unread_count: unread_count, read_outbox_max_id: max_read_id);
                    break;                
            }

            if (updatedChat != null)
                userChat.chat = updatedChat;

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
                        await tgHubProvider.SendEvent(new newChatEvent(userChat, source_id, source_name)); //обновляем чат чтобы прочитанные поменить на фронте
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
                        await tgHubProvider.SendEvent(new newChatEvent(userChat, source_id, source_name)); //обновляем чат чтобы прочитанные поменить на фронте
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

                        var message = unm.message as Message;

                        if (message != null)
                        {
                            IL.MessageBase messageBase = null;

                            switch (message.media)
                            {
                                case null:
                                case MessageMediaWebPage:
                                    messageBase = await handleTextMessage(unm, userChat);
                                    break;

                                case MessageMediaDocument mmd:
                                    messageBase = await handleMediaDocument(unm, mmd, userChat);
                                    break;

                                case MessageMediaPhoto mmp:
                                    messageBase = await handleImage(unm, mmp, userChat);
                                    break;
                            }

                            if (messageBase != null)
                            {
                                try
                                {                                   

                                    await mongoProvider.SaveMessage(messageBase);

                                    var updatedChat = await postgreProvider.UpdateTopMessage(messageBase.chat_id,                                                                           
                                                                           messageBase.telegram_message_id,
                                                                           messageBase.text ?? "Медиа",
                                                                           messageBase.date,
                                                                           add_unread: messageBase.direction.Equals("in"));

                                    if (/*userChat.is_new*/true) //временно посылаем новый чат, чтобы на фронте все обновилось
                                    {

                                        userChat.chat = updatedChat;

                                        try
                                        {
                                            await tgHubProvider.SendEvent(new newChatEvent(userChat, source_id, source_name));

                                            if (userChat.is_new) //тоже временное уловие
                                                logger.inf(tag, $"userChat:{source_name} {userChat.user.telegram_id} {userChat.user.access_hash} {userChat.user.firstname} {userChat.user.lastname}");
                                        }
                                        catch (Exception ex)
                                        {
                                            logger.err(tag, ex.Message);
                                        }
                                    }



                                    //событие о новом сообщении                
                                    await tgHubProvider.SendEvent(new newMessageEvent(userChat, messageBase));
                                    //обновить счетчик непрочитанных для входящих и top_message                             


                                    logger.inf(tag, $"{messageBase.direction}:" +
                                                    $"{userChat.user.telegram_id} " +
                                                    $"{userChat.user.firstname} " +
                                                    $"{userChat.user.lastname} " +
                                                    $"({messageBase.media?.type ?? "text"}) " +
                                                    $"by={messageBase.business_bot_username ?? "closer"}");

                                }
                                catch (MongoWriteException e) when (e.WriteError?.Category == ServerErrorCategory.DuplicateKey)
                                {
                                    logger.warn(tag, $"Сообщение с telegram_message_id={messageBase.telegram_message_id} уже существует");
                                }
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
        #endregion

        #region rx message
        async Task<TL.Message> SendTextMessage(InputPeer peer, string text)
        {
            return await client.SendMessageAsync(peer, text);            
        }


        class mediaCahceItem {
            public long file_id { get; set; }
            public byte[] file_reference { get; set; }
            public long acess_hash { get; set; }

        }

        Dictionary<string, mediaCahceItem> cachedMedia = new();

        async Task<TL.Message> SendImage(InputPeer peer, string? text, string storage_id, IL.MessageBase message)
        {

            bool needUpload = !(cachedMedia.ContainsKey(storage_id));
            Message res = null;

            if (!needUpload)
            {
                try
                {
                    var cached = cachedMedia[storage_id];

                    var media = new InputMediaPhoto
                    {
                        id = new InputPhoto()
                        {
                            id = cached.file_id,
                            access_hash = cached.acess_hash,
                            file_reference = cached.file_reference
                        }
                    };

                    res = await client.SendMessageAsync(peer, text, media);

                    message.media = new IL.MediaInfo()
                    {
                        storage_id = storage_id
                    };

                }
                catch (Exception ex)
                {
                    needUpload = true;
                }
            }

            if (needUpload)
            {

                var bytes = await s3Provider.Download(storage_id);
                using (var stream = new MemoryStream(bytes))
                {

                    var file = await client.UploadFileAsync(stream, $"{storage_id}.jpeg");

                    var uploadedPhoto = new InputMediaUploadedPhoto()
                    {
                        file = file
                    };

                    res = await client.SendMessageAsync(peer, text, uploadedPhoto);

                    var mediaPhoto = (MessageMediaPhoto)res.media;
                    var photo = (Photo)mediaPhoto.photo;

                    mediaCahceItem cahed = new mediaCahceItem()
                    {
                        file_reference = photo.file_reference,
                        file_id = photo.ID,
                        acess_hash = photo.access_hash
                    };

                    if (cachedMedia.ContainsKey(storage_id))
                        cachedMedia.Remove(storage_id);

                    cachedMedia.Add(storage_id, cahed);

                    message.media = new IL.MediaInfo()
                    {
                        type = MediaTypes.image,
                        storage_id = storage_id,
                    };
                }
            }

            return res;
        }
        #endregion

        #region public       
        public async Task OnNewMessage(messageDto messageDto)
        {
            try
            {

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                TL.Message result = null;
                var userChat = await chatsProvider.GetUserChat(account_id, messageDto.telegram_user_id);

                InputPeer peer = null;  
                manager.Users.TryGetValue(userChat.user.telegram_id, out var user);

                IL.MessageBase message = new();

                if (user != null)
                {
                    peer = user;
                } else
                {
                    if (userChat != null)
                    {
                        peer = new InputPeerUser(userChat.user.telegram_id, (long)userChat.user.access_hash);
                    }
                }

                switch (messageDto.media)                {                  

                    case mediaDto mediaInfo:

                        switch (mediaInfo.type)
                        {
                            case MediaTypes.image:                                
                                result = await SendImage(peer, messageDto.text, mediaInfo.storage_id, message);
                                break;
                        }
                        break;

                    default:
                        result = await SendTextMessage(peer, messageDto.text);                        
                        break;
                        
                }               

                if (result != null && userChat != null)
                {
                    message.chat_id = userChat.chat.id;
                    message.direction = "out";
                    message.text = messageDto.text;
                    message.telegram_message_id = result.ID;
                    message.date = result.Date;


                    await mongoProvider.SaveMessage(message);
                    stopwatch.Stop();

                    //собыьте о новом сообщении

                    var updatedChat = await postgreProvider.UpdateTopMessage(message.chat_id, message.telegram_message_id, message.text ?? "Медиа" , message.date);
                    userChat.chat = updatedChat;
                    await tgHubProvider.SendEvent(new newChatEvent(userChat, source_id, source_name));
                    await tgHubProvider.SendEvent(new newMessageEvent(userChat, message));

                    //var bytes = Encoding.ASCII.GetBytes(s);


                    logger.inf(tag, $"{message.chat_id} {message.direction}:{userChat.user.telegram_id} {userChat.user.firstname} {userChat.user.lastname} time={stopwatch.ElapsedMilliseconds} ms");
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
                        await client.ReadHistory(peer);
                    }
                    catch (Exception ex)
                    {
                        logger.err(tag, $"OnNewUpdate readHistory tg_id={update.user_telegram_id} {ex.Message}");
                    }
                    break;
            }

            await Task.CompletedTask;
        }
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

                client = new Client(config);

                manager = client.WithUpdateManager(User_OnUpdate, state_path);
                await client.LoginUserIfNeeded();

                //var dialogs = await user.Messages_GetDialogs(limit: 100); //сделать 500 ?
                //dialogs.CollectUsersChats(manager.Users, manager.Chats);

                manager.SaveState(state_path);
                status = UserApiStatus.active;

            }
            catch (RpcException ex)
            {
                processRpcException(ex);
                status = UserApiStatus.inactive;
                client.Dispose();
                throw;
            }
            catch (Exception ex)
            {
                status = UserApiStatus.inactive;
                client.Dispose();
                throw;
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
            manager.SaveState(state_path);
            client?.Dispose();
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
