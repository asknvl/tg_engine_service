using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tg_engine.interlayer.messaging;

namespace tg_engine.database.mongo
{
    public interface IMongoProvider
    {
        Task SaveMessage(MessageBase message);
        Task<(MessageBase updated, string? storage_id)> UpdateMessage(MessageBase message);
        Task<bool> CheckMessageExists(int message_id);
        Task<List<MessageBase>> GetMessages(Guid chat_id);
        Task<List<MessageBase>> MarkMessagesDeletedUser(Guid account_id, int[] ids);
        Task<List<MessageBase>> MarkMessagesDeletedChannel (Guid account_id, int[] ids, long channel_id);
        Task<(int,int)> MarkMessagesRead(Guid chat_id, string direction, int max_message_id);
        Task<(int, int)> MarkMessagesRead(Guid chat_id, string direction);
        Task<bool> CheckMessageExists(Guid chat_id, int message_id);

        #region сервисные 
        void SetAccountToChatMessages(Guid chat_id, Guid account_id);
        void SetChatTypeToChatMessages(Guid chat_id, string chat_type);
        #endregion
    }
}
