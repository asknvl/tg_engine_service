using Amazon.S3.Model;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tg_engine.config;
using tg_engine.interlayer.chats;
using tg_engine.interlayer.messaging;


namespace tg_engine.database.mongo
{
    public class MongoProvider : IMongoProvider
    {
        #region vars
        MongoClient client;
        IMongoCollection<MessageBase> messages;
        #endregion

        #region public
        public MongoProvider(settings_db settings) {

            var connectionString = $"mongodb://{settings.user}:{settings.password}@{settings.host}";
            client = new MongoClient(connectionString);
            var database = client.GetDatabase(settings.db_name);

            //messages = database.GetCollection<MessageBase>("messages_test");
            messages = database.GetCollection<MessageBase>("messages");
        }

        public async Task SaveMessage(MessageBase message)
        {
            await messages.InsertOneAsync(message);         
        }

        public async Task<(MessageBase, string?)> UpdateMessage(MessageBase message)
        {
            var filter = Builders<MessageBase>.Filter.Eq("chat_id", message.chat_id) &
                         Builders<MessageBase>.Filter.Eq("telegram_message_id", message.telegram_message_id);


            var currentMessage = await messages.Find(filter).FirstOrDefaultAsync();
            string? storage_id = currentMessage?.media?.storage_id;

            var update = Builders<MessageBase>.Update
                       .Set(m => m.text, message.text)
                       .Set(m => m.media, message.media)
                       .Set(m => m.edited_date, DateTime.UtcNow)
                       .Set(m => m.updated_at, DateTime.UtcNow)
                       .Set(m => m.is_deleted, message.is_deleted);

            var options = new FindOneAndUpdateOptions<MessageBase>
            {
                ReturnDocument = ReturnDocument.After
            };

            var res = await messages.FindOneAndUpdateAsync(filter, update, options);
            
            if (res == null)
            {
                await SaveMessage(message);
                res = message;
            }

            return (res, storage_id);            
        }

        public async Task<List<MessageBase>> GetMessages(Guid chat_id)
        {
            var filter = Builders<MessageBase>.Filter.Eq("chat_id", chat_id);
            var result = await messages.FindAsync(filter);
            return await result.ToListAsync();
        }

        public async Task<bool> CheckMessageExists(int telegram_message_id)
        {
            var filter = Builders<MessageBase>.Filter.Eq("telegram_message_id", telegram_message_id);
            using (var cursor = await messages.FindAsync(filter))
            {
                return await cursor.AnyAsync();
            }
        }
        public async Task<bool> CheckMessageExists(Guid chat_id, int message_id)
        {
            var filter = Builders<MessageBase>.Filter.Eq("chat_id", chat_id) &
                         Builders<MessageBase>.Filter.Eq("telegram_message_id", message_id);

            var document = await messages.Find(filter)
                                         .Limit(1)
                                         .FirstOrDefaultAsync();
            return document != null;

        }

        public async Task<List<MessageBase>> MarkMessagesDeletedUser(Guid account_id, int[] ids)
        {
            FilterDefinition<MessageBase> filter;

            filter = Builders<MessageBase>.Filter.Eq("account_id", account_id) &
                     Builders<MessageBase>.Filter.Eq("chat_type", ChatTypes.user) &                     
                     Builders<MessageBase>.Filter.In(m => m.telegram_message_id, ids);
                     

            var cursor = await messages.FindAsync(filter);
            var found = await cursor.ToListAsync();
            
            if (found.Count > 0)
            {
                var update = Builders<MessageBase>.Update
                    .Set(m => m.is_deleted, true)
                    .Set(m => m.deleted_date, DateTime.UtcNow)
                    .Set(m => m.updated_at, DateTime.UtcNow);

                await messages.UpdateManyAsync(filter, update);
            }
            return found;
        }

        public async Task<List<MessageBase>> MarkMessagesDeletedChannel(Guid account_id, int[] ids, long channel_id)
        {
            FilterDefinition<MessageBase> filter;

            filter = Builders<MessageBase>.Filter.Eq("account_id", account_id) &
                     Builders<MessageBase>.Filter.Eq("telegram_id", channel_id) &
                     Builders<MessageBase>.Filter.In(m => m.telegram_message_id, ids);


            var cursor = await messages.FindAsync(filter);
            var found = await cursor.ToListAsync();

            if (found.Count > 0)
            {
                var update = Builders<MessageBase>.Update
                    .Set(m => m.is_deleted, true)
                    .Set(m => m.deleted_date, DateTime.UtcNow)
                    .Set(m => m.updated_at, DateTime.UtcNow);

                await messages.UpdateManyAsync(filter, update);
            }
            return found;
        }

        /// <summary>
        /// Помечает сообщения в монго как прочитанные 
        /// </summary>
        /// <param name="chat_id"></param>
        /// <param name="direction">Входящие или исходящие</param>
        /// <param name="max_message_id"></param>
        /// <returns>Вохвращает количество непрочитанных и айди последнего прочитанного</returns>
        public async Task<(int, int)> MarkMessagesRead(Guid chat_id, string direction, int max_message_id)
        {
            int maxId = 0;
            int unreadCount = 0;

            FilterDefinition<MessageBase> filter;

            if (max_message_id > 0)
            {
                filter = Builders<MessageBase>.Filter.Eq("chat_id", chat_id) &
                             Builders<MessageBase>.Filter.Eq("direction", direction) &
                             Builders<MessageBase>.Filter.Eq("is_read", false) &
                             Builders<MessageBase>.Filter.Lte("telegram_message_id", max_message_id);
            }
            else
            {

                filter = Builders<MessageBase>.Filter.Eq("chat_id", chat_id) &
                             Builders<MessageBase>.Filter.Eq("direction", direction) &
                             Builders<MessageBase>.Filter.Eq("is_read", false);
            }

            var cursor = await messages.FindAsync(filter);
            var found = await cursor.ToListAsync();

            if (found.Count > 0)
            {
                maxId = found.Max(m => m.telegram_message_id);

                var update = Builders<MessageBase>.Update
                    .Set(m => m.is_read, true)
                    .Set(m => m.read_date, DateTime.UtcNow)
                    .Set(m => m.updated_at, DateTime.UtcNow);

                await messages.UpdateManyAsync(filter, update);
            }

            filter = Builders<MessageBase>.Filter.Eq("chat_id", chat_id) &
                         Builders<MessageBase>.Filter.Eq("direction", direction) &
                         Builders<MessageBase>.Filter.Eq("is_read", false);

            unreadCount = (int)await messages.CountDocumentsAsync(filter);
            //maxId = max_message_id;

            return (unreadCount, maxId);
        }

        #region сервисное
        public void SetAccountToChatMessages(Guid chat_id, Guid account_id)
        {
            var filter = Builders<MessageBase>.Filter.Eq("chat_id", chat_id);
            var update = Builders<MessageBase>.Update
                    .Set(m => m.account_id, account_id);                   

            messages.UpdateMany(filter, update);
        }

        public void SetChatTypeToChatMessages(Guid chat_id, string chat_type)
        {
            var filter = Builders<MessageBase>.Filter.Eq("chat_id", chat_id);
            var update = Builders<MessageBase>.Update
                    .Set(m => m.chat_type, chat_type);

            messages.UpdateMany(filter, update);
        }
        #endregion
        #endregion
    }
}
