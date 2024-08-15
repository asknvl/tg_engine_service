using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tg_engine.database.postgre.dtos;
using tg_engine.database.postgre.models;

namespace tg_engine.interlayer.chats
{
    public interface IChatsProvider
    {
        Task<UserChat> CollectUserChat(Guid account_id, Guid source_id, telegram_user user);
        Task<UserChat?> GetUserChat(Guid account_id, long telegram_id);
    }
}
