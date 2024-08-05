using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tg_engine.database.postgre.dtos;
using tg_engine.database.postgre.models;
using tg_engine.interlayer.chats;
using TL;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace tg_engine.interlayer.messaging
{
    public class MessageConstructor
    {
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

        async Task<MessageBase>getBase(UserChat userChat, UpdateNewMessage unm, Func<long, Task<UserChat>> getUserChat)
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

            var message = new MessageBase()
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

            return message;
        }
        #endregion

        #region public
        public async Task<MessageBase> Text(UserChat userChat, UpdateNewMessage unm,  Func<long, Task<UserChat>> getUserChat)
        {
            var message = await getBase(userChat, unm, getUserChat);
            message.text = getText(unm);
            return message;
        }

        public async Task<MessageBase> Image(UserChat userChat, UpdateNewMessage unm, Func<long, Task<UserChat>> getUserChat, Photo photo, string storage_id)
        {
            var message = await getBase(userChat, unm, getUserChat);

            message.media = new MediaInfo()
            {
                type = MediaTypes.image,                
                storage_id = storage_id

            };
            return message;
        }

        public async Task<MessageBase> Circle(UserChat userChat, UpdateNewMessage unm, Func<long, Task<UserChat>> getUserChat, string storage_id)
        {
            var message = await getBase(userChat, unm, getUserChat);

            message.media = new MediaInfo()
            {
                type = MediaTypes.circle,
                storage_id = storage_id
            };

            return message;
        }
        #endregion
    }
}
