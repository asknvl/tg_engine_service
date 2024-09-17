using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tg_engine.database.postgre.dtos;
using tg_engine.database.postgre.models;
using tg_engine.interlayer.chats;
using tg_engine.s3;
using tg_engine.translator;
using TL;

namespace tg_engine.interlayer.messaging
{
    public class MessageConstructor
    {
        #region vars
        ITranslator translator;
        #endregion

        public MessageConstructor(ITranslator translator) {
            this.translator = translator;
        }

        #region helpers
        bool isIncoming(TL.MessageBase input)
        {
            var message = input as Message;
            if (message != null)
                return !message.flags.HasFlag(TL.Message.Flags.out_);
            else
                throw new Exception("isIncoming: message=null");
        }

        string getText(TL.MessageBase input)
        {
            var message = input as Message;
            if (message != null)
            {
                return message.message;
            }
            else
                throw new Exception("getText: message=null");
        }

        async Task<MessageBase> getBase(UserChat userChat, TL.MessageBase input, Func<long, Task<UserChat>> getUserChat)
        {
            var chat_id = userChat.chat.id;
            var account_id = userChat.chat.account_id;
            var chat_type = userChat.chat.chat_type;

            bool incomnig = isIncoming(input);
            var direction = (incomnig) ? "in" : "out";

            int telegram_message_id = input.ID;
            
            var text = await translator.translate_incoming(getText(input));
            
            var date = input.Date;

            bool is_business_bot_reply = false;
            string? business_bot_username = null;

            if (!incomnig)
            {
                var m = input as Message;

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
                account_id = account_id,
                chat_id = chat_id,      
                chat_type = chat_type,
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
        public async Task<MessageBase> Text(UserChat userChat, TL.MessageBase input, Func<long, Task<UserChat>> getUserChat)
        {
            var message = await getBase(userChat, input, getUserChat);
            //message.text = getText(input);
            return message;
        }

        public async Task<MessageBase> Image(UserChat userChat, TL.MessageBase input, Photo photo, Func<long, Task<UserChat>> getUserChat, S3ItemInfo s3info)
        {
            var message = await getBase(userChat, input, getUserChat);

            message.media = new MediaInfo()
            {
                type = MediaTypes.image,
                file_name = null,
                extension = s3info.extension,
                storage_id = s3info.storage_id,
                storage_url = s3info.url

            };
            return message;
        }

        public async Task<MessageBase> Photo(UserChat userChat, TL.MessageBase input, Document document, Func<long, Task<UserChat>> getUserChat, S3ItemInfo s3info)
        {
            var message = await getBase(userChat, input, getUserChat);
            string mediaType = MediaTypes.photo;

            var photo = document.attributes.FirstOrDefault(a => a is TL.DocumentAttributeImageSize) as TL.DocumentAttributeImageSize;

            var m = input as TL.Message;
            if (m != null)
            {
                message.text = m.message;
            }

            message.media = new MediaInfo()
            {
                type = mediaType,
                file_name = document.Filename,
                extension = s3info.extension,
                length = document.size,
                duration = null,
                width = photo?.w,
                height = photo?.h,
                storage_id = s3info.storage_id,
                storage_url = s3info.url
            };

            return message;
        }

        public async Task<MessageBase> Video(UserChat userChat, TL.MessageBase input, Document document, Func<long, Task<UserChat>> getUserChat, S3ItemInfo s3info)
        {
            var message = await getBase(userChat, input, getUserChat);
            string mediaType = MediaTypes.video;

            var video = document.attributes.FirstOrDefault(a => a is TL.DocumentAttributeVideo) as DocumentAttributeVideo;
            if (video != null)
            {
                mediaType = video.flags.HasFlag(DocumentAttributeVideo.Flags.round_message) ? MediaTypes.circle : MediaTypes.video;
            }

            var m = input as TL.Message;
            if (m != null)
            {
                message.text = m.message;
            }

            message.media = new MediaInfo()
            {
                type = mediaType,
                file_name = document.Filename,
                extension = s3info.extension,
                length = document.size,
                duration = video?.duration,
                width = video?.w,
                height = video?.h,
                storage_id = s3info.storage_id,
                storage_url = s3info.url
            };

            return message;
        }

        public async Task<MessageBase> Voice(UserChat userChat, TL.MessageBase input, Document document, Func<long, Task<UserChat>> getUserChat, S3ItemInfo s3info)
        {
            var message = await getBase(userChat, input, getUserChat);
            string mediaType = MediaTypes.voice;

            //var video = document.attributes.FirstOrDefault(a => a is TL.DocumentAttributeVideo) as DocumentAttributeVideo;
            //if (video != null)
            //{
            //    mediaType = video.flags.HasFlag(DocumentAttributeVideo.Flags.round_message) ? MediaTypes.circle : MediaTypes.video;
            //}

            var m = input as TL.Message;
            if (m != null)
            {
                message.text = m.message;
            }

            message.media = new MediaInfo()
            {
                type = mediaType,
                file_name = document.Filename,
                extension = s3info.extension,
                length = document.size,
                //duration = video?.duration,
                //width = video?.w,
                //height = video?.h,
                storage_id = s3info.storage_id,
                storage_url = s3info.url
            };

            return message;
        }

        public async Task<MessageBase> Sticker(UserChat userChat, TL.MessageBase input, Document document, Func<long, Task<UserChat>> getUserChat, S3ItemInfo s3info)
        {
            var message = await getBase(userChat, input, getUserChat);

            var sticker = document.attributes.FirstOrDefault(a => a is TL.DocumentAttributeSticker) as DocumentAttributeSticker;

            message.text = sticker?.alt;

            return message;
        }

        //public async Task<MessageBase> Voice(UserChat userChat, TL.MessageBase input, Document document, Func<long, Task<UserChat>> getUserChat, S3ItemInfo s3info)
        //{
        //    var message = await getBase(userChat, input, getUserChat);
        //    string mediaType = MediaTypes.voice;
        //}
        #endregion
    }
}
