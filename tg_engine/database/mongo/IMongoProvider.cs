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
        Task<bool> CheckMessageExists(int message_id);
        Task<List<MessageBase>> GetMessages(Guid chat_id);
        Task<List<MessageBase>> MarkMessagesDeleted(int[] ids);
        Task<(int,int)> MarkMessagesRead(Guid chat_id, string direction, int max_message_id);
    }
}
