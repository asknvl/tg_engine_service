using logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tg_engine.database.mongo;
using tg_engine.database.postgre;
using tg_engine.database.postgre.models;
using tg_engine.interlayer.chats;
using TL;

namespace tg_engine.interlayer.messaging
{
    public abstract class TGProviderBase : IMessageObserver
    {
        #region vars        
        IPostgreProvider postgreProvider;
        IMongoProvider mongoProvider;
        IChatsProvider chatsProvider;
        ILogger logger;
        string tag;
        #endregion

        #region prperties
        public Guid account_id { get; }
        #endregion

        public TGProviderBase(Guid account_id, IPostgreProvider postgreProvider, IMongoProvider mongoProvider, ILogger logger) {

            tag = $"prvdr";

            this.account_id = account_id;
            this.postgreProvider = postgreProvider;
            this.mongoProvider = mongoProvider;

            this.logger = logger;

            chatsProvider = new ChatsProvider(postgreProvider);
        }

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
        //Сообщение получено из ТГ
        public async Task OnMessageRX(TL.UpdateNewMessage unm, TL.User user) {

            try
            {
                var u = new telegram_user()
                {
                    telegram_id = user.ID,
                    access_hash = user.access_hash,
                    firstname = user.first_name,
                    lastname = user.last_name,
                    username = user.username                    
                };

                

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                var userChat = await chatsProvider.CollectUserChat(account_id, u);

                logger.inf(tag, $"userChat:{userChat.user.telegram_id} {userChat.user.access_hash} {userChat.user.firstname} {userChat.user.lastname}");

                var chat_id = userChat.chat.id;
                var direction = (isIncoming(unm)) ? "in" : "out";
                var telegram_message_id = unm.message.ID;
                var text = getText(unm);
                var date = unm.message.Date;

                //var messages = await mongoProvider.GetMessages(chat_id);

                var message = new MessageBase()
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
                } catch (Exception e)
                {                
                    logger.warn(tag, $"Сообщение с telegram_message_id={telegram_message_id} уже существует");
                }

                logger.inf(tag, $"{direction}:{userChat.user.telegram_id} {userChat.user.firstname} {userChat.user.lastname} time={stopwatch.ElapsedMilliseconds} ms");

                stopwatch.Stop();

            } catch (Exception ex)
            {
                logger.err(tag, $"OnMessageRX: {ex.Message}");
            }
        }

        //Сообщение получено от Клиента и должно быть отправлено в ТГ 
        public async Task OnMessageTX(MessageBase message) {

            if (message.media == null && !string.IsNullOrEmpty(message.text))
            {

                long? access_hash = null;
                var userChat = await chatsProvider.GetUserChat(account_id, message.telegram_id);
                if (userChat != null)
                    access_hash = userChat.user.access_hash;

                MessageTXRequest?.Invoke(message, access_hash);
            }            
            
        }
        #endregion

        #region events
        public event Action<MessageBase, long?> MessageTXRequest;
        #endregion
    }
}
