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

namespace tg_engine.interlayer.chats
{
    public class ChatsProvider : IChatsProvider
    {
        #region vars
        ILogger logger;
        IPostgreProvider postgreProvider;
        List<UserChat> userChats = new();
        #endregion

        public ChatsProvider(IPostgreProvider postgreProvider, ILogger logger) { 
            this.logger = logger;
            this.postgreProvider = postgreProvider;
        }

        public async Task<UserChat> CollectUserChat(Guid account_id, Guid source_id, telegram_user user)
        {
            var userChat = userChats.FirstOrDefault(uc => uc.chat.account_id == account_id && uc.user.telegram_id == user.telegram_id);
            if (userChat == null)
            {
                userChat = await postgreProvider.CreateUserAndChat(account_id, source_id, user);
                userChats.Add(userChat);
            }
            else
            {
                userChat.is_new = false;

                if (!user.Equals(userChat.user))
                {
                    logger.warn("ChatsProvider", $"needUpdate {userChat.user} to {user}");
                    userChats.Remove(userChat);
                    userChat = await postgreProvider.CreateUserAndChat(account_id, source_id, user);
                    userChats.Add(userChat);
                }

            }
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

        public async Task<UserChat?> GetUserChat(Guid account_id, Guid telegram_user_id)
        {
            var userChat = userChats.FirstOrDefault(us => us.chat.account_id == account_id && us.user.id == telegram_user_id);
            if (userChat == null)
            {
                userChat = await postgreProvider.GetUserChat(account_id, telegram_user_id);
                if (userChat != null)
                    userChats.Add(userChat);
            }
            return userChat;
        }
    }
}
