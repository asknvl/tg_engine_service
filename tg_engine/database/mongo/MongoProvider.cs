using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tg_engine.config;
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


            //var connectionString = $"mongodb://{username}:{password}@{host}:{port}/{databaseName}?authSource={databaseName}&authMechanism=SCRAM-SHA-256";

        }

        public async Task SaveMessage(MessageBase message)
        {
            await messages.InsertOneAsync(message);
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

        public async Task<List<MessageBase>> MarkMessagesDeleted(int[] ids)
        {
            var filter = Builders<MessageBase>.Filter.In(m => m.telegram_message_id, ids);

            var cursor = await messages.FindAsync(filter);
            var found = await cursor.ToListAsync();
            
            if (found.Count > 0)
            {
                var update = Builders<MessageBase>.Update
                    .Set(m => m.is_deleted, true)
                    .Set(m => m.updated_at, DateTime.UtcNow);

                await messages.UpdateManyAsync(filter, update);
            }
            return found;
        }

        public async Task<(int, int)> MarkMessagesRead(Guid chat_id, string direction, int max_message_id)
        {
            int maxId = 0;
            int unreadCount = 0;

            var filter = Builders<MessageBase>.Filter.Eq("chat_id", chat_id) &
                         Builders<MessageBase>.Filter.Eq("direction", direction) &
                         Builders<MessageBase>.Filter.Eq("is_read", false) &
                         Builders<MessageBase>.Filter.Lte("telegram_message_id", max_message_id);

            var cursor = await messages.FindAsync(filter);
            var found = await cursor.ToListAsync();

            if (found.Count > 0)
            {
                var update = Builders<MessageBase>.Update
                    .Set(m => m.is_read, true)
                    .Set(m => m.read_date, DateTime.UtcNow)
                    .Set(m => m.updated_at, DateTime.UtcNow);

                await messages.UpdateManyAsync(filter, update);
            }

            //сколько входящих, непрочитанных
            filter = Builders<MessageBase>.Filter.Eq("chat_id", chat_id) &
                         Builders<MessageBase>.Filter.Eq("direction", "in") &
                         Builders<MessageBase>.Filter.Eq("is_read", false);

            unreadCount = (int)await messages.CountDocumentsAsync(filter);
            maxId = max_message_id;

            return (unreadCount, max_message_id);
        }
        #endregion
    }
}
