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
        #endregion
    }
}
