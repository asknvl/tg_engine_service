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
        Task<UserChat> CreateUserAndChat(Guid account_id, Guid source_id, telegram_user new_user, string type);
        Task UpdateUser(telegram_user user);
        Task UpdateChatType(Guid chat_id, string chat_type);
        Task<UserChat?> GetUserChat(Guid account_id, long telegram_id);
        Task<UserChat?> GetUserChat(Guid account_id, Guid telegram_user_id);
        Task<telegram_chat> UpdateUnreadCount(Guid chat_id, int? unread_count = null, int? read_inbox_max_id = null, int? read_outbox_max_id = null);
        Task<telegram_chat> UpdateUnreadCount(Guid chat_id, int? unread_inbox_count = null, int? read_inbox_max_id = null, int? unread_outbox_count = null, int? read_outbox_max_id = null);
        //Task<telegram_chat> UpdateTopMessage(Guid chat_id, int top_message, string? top_message_text, DateTime top_message_date, bool? add_unread = null);
        Task<telegram_chat> UpdateTopMessage(Guid chat_id, string direction, int top_message, string? top_message_text, DateTime top_message_date, bool igonreUnread = false);
    }
}
