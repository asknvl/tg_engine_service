using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using tg_engine.config;
using tg_engine.database.mongo;
using tg_engine.database.postgre;
using tg_engine.database.postgre.models;
using TL;

namespace mongo_modifier
{
    public class Program
    {
        
        static void Main(string[] args)
        {
            var vars = variables.getInstance();
            IPostgreProvider postgre = new PostgreProvider(vars.tg_engine_variables.accounts_settings_db);
            IMongoProvider mongo = new MongoProvider(vars.tg_engine_variables.messaging_settings_db);


            using (var context = new PostgreDbContext(postgre.DbContextOptions))
            {
                var c = context.telegram_chats.Select(c => new { c.id, c.account_id, c.chat_type }).ToList();

                foreach (var item in c)
                {
                    mongo.SetAccountToChatMessages(item.id, item.account_id);
                    Console.WriteLine($"{1} of {c.Count} done ({item.id}) ({item.account_id})");
                }
            }

        }
    }
}
