using logger;
using MediaInfo;
using MongoDB.Driver;
using System;
using System.Diagnostics;
using System.Runtime.Intrinsics.Arm;
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
using TL.Methods;
using WTelegram;
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

        protected uint updateCounter;
        protected uint updateCounterPrev = 1;

        protected System.Timers.Timer updateWatchdogTimer;
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

            this.logger = logger;

            chatsProvider = new ChatsProvider(postgreProvider, logger);

            updateWatchdogTimer = new System.Timers.Timer();
            updateWatchdogTimer.AutoReset = true;
            updateWatchdogTimer.Interval = 5 * 60 * 1000;
            updateWatchdogTimer.Elapsed += UpdateWatchdogTimer_Elapsed;         

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
            string type = ChatTypes.user;

            bool isUser = manager.Users.TryGetValue(telegram_id, out var tuser);
            bool isChat = manager.Chats.TryGetValue(telegram_id, out var tchat);

            long access_hash = 0;

            telegram_user tlUser = new();
            if (isUser)
            {
                tlUser = new telegram_user(tuser);
                access_hash = tuser.access_hash;
            }
            else
                if (isChat)
            {

                switch (tchat)
                {
                    case TL.Channel channel: //канал
                        tlUser.telegram_id = channel.ID;
                        //tlUser.access_hash = channel.access_hash;
                        access_hash = channel.access_hash;
                        tlUser.firstname = channel.Title;
                        type = (tlUser.firstname.ToLower().Contains("service_channel") && channel.IsActive) ? ChatTypes.service_channel : ChatTypes.channel;
                        break;

                    case TL.Chat chat:
                        throw new ArgumentException("Groups not supported");
                }
            }
            else
            {
                tlUser.telegram_id = telegram_id;
            }

            var userChat = await chatsProvider.CollectUserChat(account_id, source_id, tlUser, access_hash, type);

            return userChat;
        }

        string getExtensionFromMimeType(string input)
        {
            var res = input;
            var index = input.IndexOf('/');
            if (index >= 0)
                res = input.Substring(index + 1);
            return res;
        }
        #endregion

        #region updates
        async Task<IL.MessageBase> handleTextMessage(TL.MessageBase input, UserChat userChat)
        {
            var message = await messageConstructor.Text(userChat, input, getUserChat);
            return message;
        }

        async Task<IL.MessageBase> handleImage(TL.MessageBase input, MessageMediaPhoto mmp, UserChat userChat)
        {

            IL.MessageBase message = null;

            Photo photo = (Photo)mmp.photo;

            if (photo != null)
            {

                MemoryStream stream = new MemoryStream();
                var ext = await client.DownloadFileAsync(photo, stream);
                var s3info = await s3Provider.Upload(stream.ToArray(), $"{ext}");

                message = await messageConstructor.Image(userChat, input, photo, getUserChat, s3info);
            }

            return message;
        }

        async Task<IL.MessageBase> handleMediaDocument(TL.MessageBase input, MessageMediaDocument mmd, UserChat userChat)
        {
            Document document = mmd.document as Document;

            IL.MessageBase? message = null;

            if (document != null)
            {

                MemoryStream stream = new MemoryStream();
                var ext = await client.DownloadFileAsync(document, stream, progress: (a, b) => { logger.inf(tag, $"dowloaded {a} of {b}"); });

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                var s3info = await s3Provider.Upload(stream.ToArray(), getExtensionFromMimeType(ext));
                stopwatch.Stop();

                logger.inf(tag, $"S3 upload t={stopwatch.ElapsedMilliseconds}");

                //мб нужно кешировать

                switch (document.mime_type)
                {
                    case "application/x-tgsticker":
                        message = await messageConstructor.Sticker(userChat, input, document, getUserChat, s3info);
                        break;

                    case "image/jpeg":
                        message = await messageConstructor.Photo(userChat, input, document, getUserChat, s3info);
                        break;

                    case "video/mp4":
                        message = await messageConstructor.Video(userChat, input, document, getUserChat, s3info);
                        break;

                    case "audio/ogg":
                        message = await messageConstructor.Voice(userChat, input, document, getUserChat, s3info);
                        break;

                    case "":
                        break;
                }
            }

            return message;
        }

        async Task<IL.MessageBase> handleMessageType(TL.MessageBase input, UserChat userChat)
        {
            IL.MessageBase messageBase = null;
            var message = input as Message;

            switch (message.media)
            {
                case null:
                case MessageMediaWebPage:
                    messageBase = await handleTextMessage(input, userChat);
                    break;

                case MessageMediaDocument mmd:
                    messageBase = await handleMediaDocument(input, mmd, userChat);
                    break;

                case MessageMediaPhoto mmp:
                    messageBase = await handleImage(input, mmp, userChat);
                    break;
            }

            return messageBase;
        }

        async Task handleNewMessage(TL.MessageBase input, bool? update = false)
        {
            try
            {
                //var userChat = await getUserChat(unm.message.Peer.ID);
                var userChat = await getUserChat(input.Peer.ID);

                logger.dbg(tag, $"getUserChat: {userChat.user}");

                var message = input as Message;

                if (update != true)
                {
                    var exists = await mongoProvider.CheckMessageExists(userChat.chat.id, input.ID);
                    if (exists)
                    {
                        logger.warn(tag, $"Сообщение с telegra123m_message_id={input.ID} уже существует (1)");
                        return;
                    }
                }

                if (userChat.chat.chat_type == ChatTypes.user && userChat.is_new)
                {
                    try
                    {
                        var peer = new InputPeerUser(userChat.user.telegram_id, (long)userChat.access_hash);
                        var dialog = await client.Messages_GetPeerDialogs(new InputDialogPeerBase[] { peer });
                        var dlg = dialog.dialogs.FirstOrDefault() as Dialog;

                        var history = await client.Messages_GetHistory(peer, limit: 50);
                        List<IL.MessageBase> messagesToProcess = new();

                        foreach (var m in history.Messages)
                        {
                            try
                            {
                                var mb = m as TL.MessageBase;
                                var messageBase = await handleMessageType(m, userChat);
                                if (messageBase != null)
                                {
                                    await mongoProvider.SaveMessage(messageBase);
                                    messagesToProcess.Add(messageBase);
                                }
                            

                            } catch (Exception ex)
                            {
                                logger.err(tag, $"getHistory: messages {m} {m.ID} {userChat.user.telegram_id} {ex.Message}");
                            }
                        }

                        userChat = await handleMessageRead(userChat, "out", dlg.read_outbox_max_id);
                        userChat = await handleMessageRead(userChat, "in", dlg.read_inbox_max_id);

                        var lastMsg = history.Messages.FirstOrDefault() as TL.MessageBase;

                        userChat.chat = await postgreProvider.UpdateTopMessage(userChat.chat.id,
                                                                               messagesToProcess[0].direction,
                                                                               messagesToProcess[0].telegram_message_id,
                                                                               messagesToProcess[0].text ?? "Медиа",
                                                                               messagesToProcess[0].date,
                                                                               igonreUnread: true);

                        var chEvent = new newChatEvent(userChat, source_id, source_name);
                        await tgHubProvider.SendEvent(chEvent);

                        return;

                    } catch (Exception ex)
                    {
                        logger.err(tag, $"getHistory: {ex.Message}");
                    }
                }

                if (message != null)
                {
                    var messageBase = await handleMessageType(input, userChat);

                    if (messageBase != null)
                    {
                        try
                        {
                            if (update != true)
                                await mongoProvider.SaveMessage(messageBase);
                            else
                                messageBase = await mongoProvider.UpdateMessage(messageBase);

                            userChat.chat = await postgreProvider.UpdateTopMessage(messageBase.chat_id,
                                                                                     messageBase.direction,
                                                                                     messageBase.telegram_message_id,
                                                                                     messageBase.text ?? "Медиа",
                                                                                     messageBase.date);


                            var chEvent = (userChat.is_new) ? new newChatEvent(userChat, source_id, source_name) : new updateChatEvent(userChat, source_id, source_name);

                            await tgHubProvider.SendEvent(chEvent);

                            if (userChat.is_new) //тоже временное уловие
                                logger.inf(tag, $"userChat:{source_name} {userChat.user}");

                            //событие о новом сообщении                
                            await tgHubProvider.SendEvent(new newMessageEvent(userChat, messageBase));

                            logger.inf(tag, $"{messageBase.direction}:" +
                                            $"{userChat.user} " +
                                            $"({messageBase.media?.type ?? "text"}) " +
                                            $"{messageBase.telegram_message_id}");

                        }
                        catch (MongoWriteException e) when (e.WriteError?.Category == ServerErrorCategory.DuplicateKey)
                        {
                            logger.warn(tag, $"Сообщение с telegram_message_id={messageBase.telegram_message_id} уже существует (2)");
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                logger.err(tag, ex.Message);
            }
        }

        async Task handleUpdateMessage(TL.MessageBase input)
        {
            try
            {
                //var userChat = await getUserChat(unm.message.Peer.ID);
                var userChat = await getUserChat(input.Peer.ID);

                logger.dbg(tag, $"getUserChat: {userChat.user}");

                var message = input as Message;

                //var exists = await mongoProvider.CheckMessageExists(userChat.chat.id, input.ID);
                //if (exists)
                //{
                //    logger.warn(tag, $"Сообщение с telegram_message_id={input.ID} уже существует (1)");
                //    return;
                //}

                if (message != null)
                {
                    IL.MessageBase messageBase = null;

                    switch (message.media)
                    {
                        case null:
                        case MessageMediaWebPage:
                            messageBase = await handleTextMessage(input, userChat);
                            break;

                        case MessageMediaDocument mmd:
                            messageBase = await handleMediaDocument(input, mmd, userChat);
                            break;

                        case MessageMediaPhoto mmp:
                            messageBase = await handleImage(input, mmp, userChat);
                            break;
                    }

                    if (messageBase != null)
                    {
                        try
                        {
                            var updated = await mongoProvider.UpdateMessage(messageBase);
                            //событие об обновлении сообщения
                            //await tgHubProvider.SendEvent(new newMessageEvent(userChat, messageBase));
                            await tgHubProvider.SendEvent(new newMessageEvent(userChat, updated));

                            logger.inf(tag, $"{messageBase.direction}:" +
                                            $"{userChat.user} " +
                                            $"(updated message_id={messageBase.telegram_message_id})");
                        }
                        catch (MongoWriteException e) when (e.WriteError?.Category == ServerErrorCategory.DuplicateKey)
                        {
                            logger.warn(tag, $"Сообщение с telegram_message_id={messageBase.telegram_message_id} уже существует (3)");
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                logger.err(tag, ex.Message);
            }
        }

        //TODO добавить удаление всего чата, нужно поменить чат как удаленный и прокинуть ивент
        async Task handleMessageDeletion(int[] message_ids)
        {
            var messages = await mongoProvider.MarkMessagesDeleted(account_id, message_ids);
            if (messages.Count > 0)
            {
                await tgHubProvider.SendEvent(new deleteMessagesEvent(account_id, messages[0].chat_id, message_ids));
            }
        }
        async Task<UserChat> handleMessageRead(UserChat userChat, string direction, int max_id)
        {
            int unread_count = 0;
            int max_read_id = 0;
            telegram_chat updatedChat = null;

            (unread_count, max_read_id) = await mongoProvider.MarkMessagesRead(userChat.chat.id, direction, max_id);

            switch (direction)
            {
                case "in":                    
                    updatedChat = await postgreProvider.UpdateUnreadCount(userChat.chat.id, unread_inbox_count: unread_count, read_inbox_max_id: max_read_id);
                    break;

                case "out":                    
                    updatedChat = await postgreProvider.UpdateUnreadCount(userChat.chat.id, unread_outbox_count: unread_count, read_outbox_max_id: max_read_id);
                    break;
            }

            if (updatedChat != null)
                userChat.chat = updatedChat;

           return userChat;
        }

        async Task handleUpdateChannel(UpdateChannel udc)
        {
            var channels = manager.Chats;
            if (channels.ContainsKey(udc.channel_id))
            {                
                await getUserChat(udc.channel_id);
            }
            await Task.CompletedTask;
        }

        private async Task User_OnUpdate(Update update)
        {

            logger.inf(tag, $"{update}");

            if (update is UpdateMessagePoll)
                return;

            updateCounter++;

            UserChat userChat = null;
            long telegram_id = 0;            

            switch (update)
            {

                case UpdateChannel udc:

                    try
                    {
                        await handleUpdateChannel(udc);
                    } catch (Exception ex)
                    {
                        logger.err(tag, $"UpdateChannel: {udc.channel_id} {ex.Message}");
                    }

                    break;

                case UpdateReadHistoryInbox uhi:
                    //мы прочли
                    telegram_id = uhi.peer.ID;
                    try
                    {
                        userChat = await getUserChat(telegram_id);
                        userChat = await handleMessageRead(userChat, "in", uhi.max_id);
                        await tgHubProvider.SendEvent(new updateChatEvent(userChat, source_id, source_name)); //обновляем чат чтобы прочитанные поменить на фронте
                        await tgHubProvider.SendEvent(new readHistoryEvent(userChat, "in", uhi.max_id));                                                                                                              
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
                        userChat = await handleMessageRead(userChat, "out", uho.max_id);
                        await tgHubProvider.SendEvent(new updateChatEvent(userChat, source_id, source_name)); //обновляем чат чтобы прочитанные поменить на фронте
                        await tgHubProvider.SendEvent(new readHistoryEvent(userChat, "out", uho.max_id));
                    }
                    catch (Exception ex)
                    {
                        logger.err(tag, $"UpdateReadHisotryOutbox: {telegram_id} {ex.Message} {ex?.InnerException?.Message}");
                    }
                    break;

                case UpdateDeleteChannelMessages udcm:
                    try
                    {

                    } catch (Exception ex)
                    {

                    }
                    break;
                case UpdateDeleteMessages udm:
                    try
                    {                          
                        await handleMessageDeletion(udm.messages);                        
                        
                    } catch (Exception ex)
                    {
                        logger.err(tag, $"UpdateDeleteMessages: {ex.Message} {ex?.InnerException?.Message}");
                    }
                    break;               

                case UpdateNewChannelMessage uncm:
                    await handleNewMessage(uncm.message);
                    break;

                case UpdateNewMessage unm:
                    await handleNewMessage(unm.message);                  
                    break;

                case UpdateEditChannelMessage uecm:
                    await handleUpdateMessage(uecm.message);
                    break;

                case UpdateEditMessage uem:
                    await handleUpdateMessage(uem.message);
                    break;                    
            }
        }
        #endregion

        #region rx message
        async Task<TL.Message> SendTextMessage(InputPeer peer, string text)
        {
            return await client.SendMessageAsync(peer, text);
        }


        class mediaCahceItem
        {
            public long file_id { get; set; }
            public byte[] file_reference { get; set; }
            public long acess_hash { get; set; }

        }

        Dictionary<string, mediaCahceItem> cachedMedia = new();

        async Task<TL.Message> SendImage(InputPeer peer, string? text, string storage_id, IL.MessageBase message)
        {

            bool needUpload = !(cachedMedia.ContainsKey(storage_id));
            Message res = null;
            S3ItemInfo s3info = null;

            if (!needUpload)
            {
                s3info = await s3Provider.GetInfo(storage_id);

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

                }
                catch (Exception ex)
                {
                    needUpload = true;
                }
            }

            if (needUpload)
            {

                byte[] bytes = null;
                (bytes, s3info) = await s3Provider.Download(storage_id);
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
                }
            }

            message.media = new IL.MediaInfo()
            {
                type = MediaTypes.image,
                storage_id = s3info.storage_id,
                storage_url = s3info.url,
                extension = s3info.extension
            };

            return res;
        }

        async Task<TL.Message> SendMediaDocument(InputPeer peer, string? text, string type, string? file_name, string storage_id, IL.MessageBase message)
        {
            bool needUpload = !(cachedMedia.ContainsKey(storage_id));
            Message res = null;
            S3ItemInfo s3info = null;

            if (!needUpload)
            {
                 s3info = await s3Provider.GetInfo(storage_id);

                try
                {
                    var cached = cachedMedia[storage_id];

                    var document = new InputMediaDocument()
                    {
                        id = new InputDocument()
                        {
                            id = cached.file_id,
                            access_hash = cached.acess_hash,
                            file_reference = cached.file_reference
                        }
                    };

                    res = await client.SendMessageAsync(peer, text, document);

                }
                catch (Exception ex)
                {
                    needUpload = true;
                }
            }

            if (needUpload)
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                byte[] bytes = null;                

                (bytes, s3info) = await s3Provider.Download(storage_id);
                stopwatch.Stop();

                logger.inf(tag, $"S3 dowload t={stopwatch.ElapsedMilliseconds}");

                using (var stream = new MemoryStream(bytes))
                {
                    var mediaProperties = new MediaInfoWrapper(new MemoryStream(bytes));
                    int cntr = 0;
                    var file = await client.UploadFileAsync(stream, $"{file_name}", progress: (a, b) => { logger.inf(tag, $"uploaded {a} of {b} cntr={cntr++}"); });                    

                    string? mime_type = null;
                    DocumentAttribute[]? attributes = null;

                    InputFileBase inputFile = (mediaProperties.Size <= 10 * 1024 * 1024) ? new InputFile() : new InputFileBig();
                    inputFile.ID = file.ID;
                    inputFile.Parts = file.Parts;
                    
                    switch (type)
                    {
                        case MediaTypes.circle:
                            mime_type = "video/mp4";
                            attributes = new DocumentAttribute[] {
                                new DocumentAttributeVideo {
                                    duration = mediaProperties.Duration / 1000.0,
                                    w = mediaProperties.Width,
                                    h = mediaProperties.Height,
                                    flags = DocumentAttributeVideo.Flags.supports_streaming | DocumentAttributeVideo.Flags.round_message
                                },
                                new DocumentAttributeFilename {
                                    file_name = file_name
                                }
                            };
                            break;
                        case MediaTypes.video:
                            mime_type = "video/mp4";
                            attributes = new DocumentAttribute[] {
                                new DocumentAttributeVideo {
                                    duration = mediaProperties.Duration / 1000.0,
                                    w = mediaProperties.Width,
                                    h = mediaProperties.Height,
                                    flags = DocumentAttributeVideo.Flags.supports_streaming
                                },
                                new DocumentAttributeFilename {
                                    file_name = file_name
                                }
                            };
                            break;
                        case MediaTypes.photo:
                            mime_type = "image/jpeg";
                            attributes = new DocumentAttribute[] {
                                new DocumentAttributeFilename {
                                   file_name = file_name
                                }
                            };                        
                            break;
                        case MediaTypes.voice:
                            mime_type = "audio/ogg";
                            attributes = new DocumentAttribute[] {
                                new DocumentAttributeAudio()
                                {
                                    flags = DocumentAttributeAudio.Flags.voice
                                }
                            };
                            break;
                    }

                    var document = new InputMediaUploadedDocument()
                    {
                        file = inputFile,
                        mime_type = mime_type,
                        attributes = attributes
                    };

                    res = await client.SendMessageAsync(peer, text, document);

                    var mmd = res.media as MessageMediaDocument;
                    var doc = mmd?.document as Document;

                    mediaCahceItem cahed = new mediaCahceItem()
                    {
                        file_reference = doc.file_reference,
                        file_id = doc.ID,
                        acess_hash = doc.access_hash
                    };

                    if (cachedMedia.ContainsKey(storage_id))
                        cachedMedia.Remove(storage_id);

                    cachedMedia.Add(storage_id, cahed);                    
                }
            }

            message.media = new IL.MediaInfo()
            {
                type = type,
                storage_id = s3info.storage_id,
                file_name = file_name,
                storage_url = s3info.url,
                extension = s3info.extension
            };

            return res;
        }
        private async void UpdateWatchdogTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {

            if (status == UserApiStatus.verification)
                return;

            logger.inf(tag, $"updateWatchDog: updateCounter={updateCounter} updateCounterPrev={updateCounterPrev}");

            if (updateCounter == updateCounterPrev)
            {

                logger.warn(tag, $"updateWatchDog: updateCounter={updateCounter} updateCounterPrev={updateCounterPrev}");

                try
                {
                    manager.SaveState(state_path);
                    client.Dispose();
                    client = new Client(config);
                    manager = client.WithUpdateManager(User_OnUpdate, state_path);
                    await client.LoginUserIfNeeded();
                } catch (Exception ex)
                {
                    logger.err(tag, $"updateWatchDog: {ex.Message}");
                }
            }

            updateCounterPrev = updateCounter;
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

                IL.MessageBase message = new()
                {
                    account_id = account_id,
                    chat_id = userChat.chat.id
                };

                string access_hash = "";

                if (user != null)
                {
                    peer = user;
                    access_hash = "TG_" + user.access_hash;
                }
                else
                {
                    if (userChat != null)
                    {
                        peer = new InputPeerUser(userChat.user.telegram_id, userChat.access_hash);
                        access_hash = "DB_" + userChat.access_hash;
                    }
                }

                logger.inf(tag, $"OnNewMessage: {userChat.user}, {access_hash}");

                //Временное сообщение о прочтении чата
                try
                {
                    await client.ReadHistory(peer, (int)userChat.chat.top_message);
                    await handleMessageRead(userChat, "in", (int)userChat.chat.top_message);
                }
                catch (Exception ex)
                {

                }

                switch (messageDto.media)
                {

                    case mediaDto mediaInfo:

                        switch (mediaInfo.type)
                        {
                            case MediaTypes.image:
                                result = await SendImage(peer, messageDto.text, mediaInfo.storage_id, message);
                                break;

                            case MediaTypes.video:
                            case MediaTypes.circle:
                            case MediaTypes.photo:
                            case MediaTypes.voice:
                                result = await SendMediaDocument(peer, messageDto.text, mediaInfo.type, mediaInfo.file_name, mediaInfo.storage_id, message);
                                break;

                        }
                        break;

                    default:
                        result = await SendTextMessage(peer, messageDto.text);
                        break;

                }

                if (result != null && userChat != null)
                {                                    
                    message.direction = "out";
                    message.text = messageDto.text;                    
                    message.telegram_message_id = result.ID;
                    message.date = result.Date;
                    message.operator_id = messageDto.operator_id;

                    await mongoProvider.SaveMessage(message);                    

                    var updatedChat = await postgreProvider.UpdateTopMessage(message.chat_id,
                                                                             message.direction,
                                                                             message.telegram_message_id,
                                                                             message.text ?? "Медиа", message.date);

                    userChat.chat = updatedChat;
                    await tgHubProvider.SendEvent(new updateChatEvent(userChat, source_id, source_name));
                    await tgHubProvider.SendEvent(new newMessageEvent(userChat, message));

                    stopwatch.Stop();

                    logger.inf(tag, $"{message.direction}:{userChat.user} time={stopwatch.ElapsedMilliseconds} ms");
                }

            }
            catch (Exception ex)
            {
                logger.err(tag, $"OnMessageTX: {ex.Message}");
            }
        }
        public async Task OnNewUpdate(UpdateBase update)
        {

            var userChat = await chatsProvider.GetUserChat(account_id, update.telegram_user_id);
            InputPeer peer = null;

            manager.Users.TryGetValue(userChat.user.telegram_id, out var user);

            if (user != null)
            {
                peer = user;
            }
            else
            {
                if (userChat != null)
                {
                    peer = new InputPeerUser(userChat.user.telegram_id, userChat.access_hash);
                }
            }

            if (peer == null)
                return;

            switch (update)
            {
                case readHistory rh:
                    try
                    {                        
                        await client.ReadHistory(peer, rh.max_id);
                        await handleMessageRead(userChat, "in", rh.max_id);
                        await tgHubProvider.SendEvent(new updateChatEvent(userChat, source_id, source_name));
                    }
                    catch (Exception ex)
                    {
                        logger.err(tag, $"OnNewUpdate readHistory tg_id={userChat.user.telegram_id} peer={peer.ID} {ex.Message}");
                    }
                    break;

                case deleteMessage dm:
                    try
                    {
                        var ids = dm.ids.ToArray();
                        var res = await client.DeleteMessages(peer, ids);
                        await handleMessageDeletion(ids);                        

                    } catch (Exception ex)
                    {
                        logger.err(tag, $"OnNewUpdate deleteMessage tg_id={userChat.user.telegram_id} peer={peer.ID} {ex.Message}");
                    }
                    break;
                    
            }

            await Task.CompletedTask;
        }


        public async Task<Messages_Dialogs> GetAllDialogs(int? folder_id = null)
        {

            int itteratonCntr = 5;

            var dialogs = await client.Messages_GetDialogs(folder_id: folder_id, limit:100);
            switch (dialogs)
            {
                case Messages_DialogsSlice mds:
                    var dialogList = new List<DialogBase>();
                    var messageList = new List<TL.MessageBase>();

                    var found = mds.chats.Any(c => c.Value.Title.ToLower().Contains("service_chat"));
                    if (found)
                        return mds;

                    while (dialogs.Dialogs.Length != 0)
                    {
                        dialogList.AddRange(dialogs.Dialogs);
                        messageList.AddRange(dialogs.Messages);
                        int last = dialogs.Dialogs.Length - 1;
                        var lastDialog = dialogs.Dialogs[last];
                        var lastPeer = dialogs.UserOrChat(lastDialog).ToInputPeer();
                        var lastMsgId = lastDialog.TopMessage;
                    retryDate:
                        var lastDate = dialogs.Messages.LastOrDefault(m => m.Peer.ID == lastDialog.Peer.ID && m.ID == lastDialog.TopMessage)?.Date ?? default;
                        if (lastDate == default)
                            if (--last < 0) break; else { lastDialog = dialogs.Dialogs[last]; goto retryDate; }

                        dialogs = await client.Messages_GetDialogs(lastDate, lastMsgId, lastPeer, folder_id: folder_id);
                        if (dialogs is not Messages_Dialogs md) break;
                        
                        foreach (var (key, value) in md.chats) mds.chats[key] = value;
                        foreach (var (key, value) in md.users) mds.users[key] = value;

                        found = mds.chats.Any(c => c.Value.Title.ToLower().Contains("service_channel"));
                        if (found) break;

                        itteratonCntr--;
                        if (itteratonCntr == 0)
                            break;

                    }
                    mds.dialogs = [.. dialogList];
                    mds.messages = [.. messageList];
                    return mds;
                case Messages_Dialogs md: return md;
                default: throw new WTException("Messages_GetDialogs returned unexpected " + dialogs?.GetType().Name);
            }
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


                var dialogs = await GetAllDialogs();//await client.Messages_GetDialogs(limit: 100); //сделать 500 ?                
                dialogs.CollectUsersChats(manager.Users, manager.Chats);
                var chats = manager.Chats;

                InputPeer peer = chats.Values.FirstOrDefault(c => c.Title.ToLower().Contains("service") && c.IsActive);
                //InputPeer peer = chats[2248416752];

                if (peer != null)
                {
                    for (int offset_id = 0; ;)
                    {
                        var messages = await client.Messages_GetHistory(peer, offset_id);
                        if (messages.Messages.Length == 0) break;
                        foreach (var msgBase in messages.Messages)
                        {
                            var from = messages.UserOrChat(msgBase.From ?? msgBase.Peer); // from can be User/Chat/Channel
                            if (msgBase is Message msg)
                            {
                                await handleNewMessage(msgBase/*, update: true*/);
                            }

                            //else if (msgBase is MessageService ms)
                            //    Console.WriteLine($"{from} [{ms.action.GetType().Name[13..]}]");
                        }
                        offset_id = messages.Messages[^1].ID;

                    }
                }

                manager.SaveState(state_path);


                updateWatchdogTimer?.Start();
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
            updateWatchdogTimer?.Stop();
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
