using logger;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tg_engine.database.postgre;
using tg_engine.database.postgre.dtos;
using tg_engine.database.postgre.models;
using TL;

namespace tg_engine.interlayer.chats
{
    public class ChatsProvider : IChatsProvider
    {
        #region vars
        ILogger logger;
        IPostgreProvider postgreProvider;
        List<UserChat> userChats = new();
        #endregion

        public ChatsProvider(IPostgreProvider postgreProvider, ILogger logger)
        {
            this.logger = logger;
            this.postgreProvider = postgreProvider;
        }

        public async Task<UserChat> CollectUserChat(Guid account_id, Guid source_id, telegram_user user, long access_hash, bool is_min, string type)
        {

            if (access_hash == 0)
                throw new Exception($"User {user.telegram_id} not found");

            var userChat = userChats.FirstOrDefault(uc => uc.chat.account_id == account_id && uc.user.telegram_id == user.telegram_id);
            if (userChat == null)
            {
                userChat = await postgreProvider.CreateUserAndChat(account_id, source_id, user, access_hash, type);
                userChats.Add(userChat);
            }
            else
            {
                userChat.is_new = false;

                if (userChat.access_hash != access_hash)
                {
                    if (!is_min)
                    {
                        await postgreProvider.CreateOrUpdateAccessHash(account_id, user.telegram_id, access_hash);                        
                        logger.warn("chatsProvider", $"access_hash change OK: {userChat.access_hash}->{access_hash}, {userChat.user}");
                        userChat.access_hash = access_hash;
                    }
                    else
                    {
                        logger.warn("chatsProvider", $"access_hash change IGNORED: {userChat.access_hash}->{access_hash}, {userChat.user}");
                    }
                }

                //bool needUpdate = false;
                //if (userChat.user.firstname != user.firstname)
                //{
                //    userChat.user.firstname = user.firstname;
                //    userChat.user.lastname = user.lastname;
                //    userChat.user.username = user.username;
                //    needUpdate = true;
                //}

                if (userChat.chat.chat_type != type)
                {
                    userChat.chat.chat_type = type;
                    await postgreProvider.UpdateChatType(userChat.chat.id, type);
                    logger.inf("CHTPRVDR", $"{userChat.chat.telegram_user_id} type={type}"); //отписка от сервисного чата - меняем тип на просто чат и наоборот
                }
            }
            return userChat;
        }

        public async Task<UserChat?> GetUserChat(Guid account_id, Guid telegram_user_id)
        {
            var userChat = userChats.FirstOrDefault(us => us.chat.account_id == account_id && us.user.id == telegram_user_id);
            if (userChat == null)
            {
                userChat = await postgreProvider.GetUserChat(account_id, telegram_user_id);
                if (userChat != null)
                    userChats.Add(userChat);
            }
            else
                userChat.is_new = false;
            
            return userChat;
        }

        public async Task<UserChat?> GetUserChat(Guid account_id, long telegram_id)
        {
            var userChat = userChats.FirstOrDefault(us => us.chat.account_id == account_id && us.user.telegram_id == telegram_id);
            if (userChat == null)
            {
                userChat = await postgreProvider.GetUserChat(account_id, telegram_id);
                if (userChat != null)
                    userChats.Add(userChat);
            }
            return userChat;
        }

    }
}
