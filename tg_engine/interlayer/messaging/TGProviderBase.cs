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
    public abstract class TGProviderBase
    {
        #region vars
        Guid account_id;        

        IPostgreProvider postgreProvider;
        IMongoProvider mongoProvider;
        IChatsProvider chatsProvider;
        ILogger logger;
        string tag;
        #endregion

        public TGProviderBase(Guid account_id, IPostgreProvider postgreProvider, IMongoProvider mongoProvider, ILogger logger) {

            tag = $"prvdr";

            this.account_id = account_id;
            this.postgreProvider = postgreProvider;
            this.mongoProvider = mongoProvider;

            this.logger = logger;

            chatsProvider = new ChatsProvider(postgreProvider);
        }

        #region private
        Task processMessage(MessageBase message)
        {
            return Task.CompletedTask;
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
        //Сообщение получено из ТГ
        public async Task OnMessageRX(TL.UpdateNewMessage unm, TL.User user) {

            try
            {
                var u = new telegram_user()
                {
                    telegram_id = user.ID,
                    firstname = user.first_name,
                    lastname = user.last_name,
                    username = user.username
                };

                Stopwatch stopwatch = new Stopwatch();

                stopwatch.Start();
                var userChat = await chatsProvider.CollectUserChat(account_id, u);                

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

                bool exists = false;
                try
                {
                    await mongoProvider.SaveMessage(message);
                } catch (Exception e)
                {
                    exists = true;
                    logger.warn(tag, $"Сообщение с telegram_message_id={telegram_message_id} уже существует");
                }

                //var exists = await mongoProvider.CheckMessageExists(telegram_message_id);
                //if (!exists)
                //{
                //    var message = new MessageBase()
                //    {
                //        chat_id = chat_id,
                //        direction = direction,
                //        telegram_message_id = telegram_message_id,
                //        text = text,
                //        date = date
                //    };

                //    await mongoProvider.SaveMessage(message);
                //}

                logger.inf(tag, $"{direction}:{userChat.user.telegram_id} {userChat.user.firstname} {userChat.user.lastname} exists={exists} time={stopwatch.ElapsedMilliseconds} ms");

                stopwatch.Stop();

            } catch (Exception ex)
            {
                
            }

            //return Task.CompletedTask;
        }

        //Сообщение получено от Клиента и должно быть отправлено в ТГ 
        public Task OnMessageTX(MessageBase message) {
            MessageTXRequest?.Invoke(message);
            return Task.CompletedTask;
        }
        #endregion

        #region events
        public event Action<MessageBase> MessageTXRequest;
        #endregion
    }
}
