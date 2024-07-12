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
        IPostgreProvider postgreProvider;
        List<UserChat> userChats = new();
        #endregion

        public ChatsProvider(IPostgreProvider postgreProvider) { 
            this.postgreProvider = postgreProvider;
        }

        public async Task<UserChat> CollectUserChat(Guid account_id, telegram_user user)
        {
            bool res = true;
            var userChat = userChats.FirstOrDefault(uc => uc.chat.account_id == account_id && uc.user.telegram_id == user.telegram_id);
            if (userChat == null)
            {              
                res = false;
                userChat = await postgreProvider.CreateUserAndChat(account_id, user);
                userChats.Add(userChat);
            }
            return userChat;
        }
    }
}
