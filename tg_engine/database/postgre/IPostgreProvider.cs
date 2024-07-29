using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tg_engine.database.postgre.dtos;
using tg_engine.database.postgre.models;
using tg_engine.dm;

namespace tg_engine.database.postgre
{
    public interface IPostgreProvider
    {
        Task<List<account>> GetAccountsAsync();
        Task<List<channel_account>> GetChannelsAccounts();
        Task<List<DMStartupSettings>> GetStatupData();
        Task<UserChat> CreateUserAndChat(Guid account_id, telegram_user new_user);
        Task<UserChat?> GetUserChat(Guid account_id, long telegram_id);        
        Task UpdateUnreadCount(Guid chat_id, int? unread_count = null, int? read_inbox_max_id = null, int? read_outbox_max_id = null);
        Task UpdateTopMessage(Guid chat_id, int top_message, bool? add_unread = null);
    }
}
