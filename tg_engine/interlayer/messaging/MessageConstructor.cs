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
        TL.User me;
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

        string getInitials(string? fn, string? ln)
        {
            var _1 = (!string.IsNullOrEmpty(fn)) ? fn.Substring(0, 1) : "";
            var _2 = (!string.IsNullOrEmpty(ln)) ? ln.Substring(0, 1) : "";            

            var combine = $"{_1}{_2}";                        

            return combine;
        }

        async Task<MessageBase> getBase(UserChat userChat,TL.User me, TL.MessageBase input, string? business_bot_un)
        {
            var chat_id = userChat.chat.id;
            var account_id = userChat.chat.account_id;
            var chat_type = userChat.chat.chat_type;

            bool incomnig = isIncoming(input);
            var direction = (incomnig) ? "in" : "out";

            int reply_to_message_id = 0;
            var replyHeader = input.ReplyTo as MessageReplyHeader;
            if (replyHeader != null)
                reply_to_message_id = replyHeader.reply_to_msg_id;

            int telegram_message_id = input.ID;
            
            var text = getText(input);

            var screen_text = await translator.translate(text); 
            
            var date = input.Date;

            bool is_business_bot_reply = false;
            string? business_bot_username = null;

            var m = input as Message;

            if (!incomnig)
            {
                //var m = input as Message;

                is_business_bot_reply = m.flags2.HasFlag(Message.Flags2.has_via_business_bot_id);
                if (is_business_bot_reply)
                {                    
                    business_bot_username = business_bot_un;
                }
            }

            Reactions? reactions = null;

            List<Reaction> reactions_tmp = null;

            if (m.reactions != null && m.reactions.results.Length > 0)
            {
                //reactions = new Reactions();
                reactions_tmp = new List<Reaction>();

                string initials = "";

                foreach (var reaction in m.reactions.results)
                {
                    var emodjiReaction = reaction.reaction as ReactionEmoji;

                    Reaction il_reaction = new Reaction();
                    il_reaction.emoji = emodjiReaction.emoticon;

                    if (reaction.count > 1)
                    {
                        il_reaction.initials.Add(getInitials(userChat.user.firstname, userChat.user.lastname));
                        il_reaction.initials.Add(getInitials(me.first_name, me.last_name));
                    } else
                    {
                        var recent = m.reactions.recent_reactions.FirstOrDefault(r => (r.reaction as ReactionEmoji).emoticon.Equals(emodjiReaction.emoticon));
                        if (recent != null)
                        {
                            var peerReaction = (MessagePeerReaction)recent;
                            initials = (peerReaction.peer_id == userChat.user.telegram_id) ?
                            getInitials(userChat.user.firstname, userChat.user.lastname) :
                            getInitials(me.first_name, me.last_name);
                            il_reaction.initials.Add(initials);
                        } else
                            il_reaction.initials.Add($"!{initials}");

                    }

                    reactions_tmp.Add(il_reaction);

                    

                }


                //foreach (var reaction in m.reactions.recent_reactions)
                //{
                //    var emodjiReaction = reaction.reaction as ReactionEmoji;
                //    if (emodjiReaction != null)
                //    {


                //        var found = reactions_tmp.FirstOrDefault(r => r.Equals(emodjiReaction.emoticon));
                //        var initials = (reaction.peer_id == userChat.user.telegram_id) ? 
                //            getInitials(userChat.user.firstname, userChat.user.lastname):
                //            getInitials(me.first_name, me.last_name);

                //        if (found == null)
                //        {
                //            found = new Reaction()
                //            {
                //                emoji = emodjiReaction.emoticon,
                //                initials = new List<string>() { initials }
                //            };

                //            reactions_tmp.Add(found);
                //        }
                //        else
                //        {
                //            found.initials.Add(initials);   
                //        }
                //    }
                //}
            }

            var message = new MessageBase()
            {
                account_id = account_id,
                chat_id = chat_id,
                chat_type = chat_type,
                direction = direction,
                reply_to_message_id = reply_to_message_id,
                telegram_id = userChat.user.telegram_id,
                telegram_message_id = telegram_message_id,
                text = text,
                screen_text = screen_text,
                reactions = reactions_tmp,
                date = date,
                is_business_bot_reply = is_business_bot_reply,
                business_bot_username = business_bot_username
            };

            return message;
        }
        #endregion

        #region public
        public async Task<MessageBase> Text(UserChat userChat, TL.User me, TL.MessageBase input, string? business_bot_un)
        {
            var message = await getBase(userChat, me, input, business_bot_un);
            //message.text = getText(input);
            return message;
        }

        public async Task<MessageBase> Image(UserChat userChat, TL.User me, TL.MessageBase input, Photo photo, string? business_bot_username, S3ItemInfo? s3info)
        {
            var message = await getBase(userChat, me, input, business_bot_username);

            if (s3info != null)
            {
                message.media = new MediaInfo()
                {
                    type = MediaTypes.image,
                    file_name = null,
                    extension = s3info.extension,
                    storage_id = s3info.storage_id,
                    storage_url = s3info.url
                };
            }

            return message;
        }

        public async Task<MessageBase> Photo(UserChat userChat, TL.User me, TL.MessageBase input, Document document, string? business_bot_un, S3ItemInfo? s3info)
        {
            var message = await getBase(userChat, me, input, business_bot_un);
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

        public async Task<MessageBase> Video(UserChat userChat, TL.User me, TL.MessageBase input, Document document, string? business_bot_un, S3ItemInfo? s3info)
        {
            var message = await getBase(userChat, me, input, business_bot_un);
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

            if (s3info != null)
            {
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
            }

            return message;
        }

        public async Task<MessageBase> Voice(UserChat userChat, TL.User me, TL.MessageBase input, Document document, string? business_bot_un, S3ItemInfo s3info)
        {
            var message = await getBase(userChat, me, input, business_bot_un);
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

        public async Task<MessageBase> Sticker(UserChat userChat, TL.User me, TL.MessageBase input, Document document, string? business_bot_un)
        {
            var message = await getBase(userChat, me, input, business_bot_un);

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
