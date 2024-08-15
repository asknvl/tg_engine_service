using Microsoft.EntityFrameworkCore;
using tg_engine.config;
using tg_engine.database.postgre.dtos;
using tg_engine.database.postgre.models;
using tg_engine.dm;

namespace tg_engine.database.postgre
{
    public class PostgreProvider : IPostgreProvider
    {
        #region vars
        private readonly DbContextOptions<PostgreDbContext> dbContextOptions;
        object lockObj = new object();
        #endregion

        public PostgreProvider(settings_db settings)
        {
            var optionsBuilder = new DbContextOptionsBuilder<PostgreDbContext>();
            optionsBuilder.UseNpgsql($"Host={settings.host};Username={settings.user};Password={settings.password};Database={settings.db_name};Pooling=true;");
            dbContextOptions = optionsBuilder.Options;
        }

        public async Task<List<account>> GetAccountsAsync()
        {
            using (var context = new PostgreDbContext(dbContextOptions))
            {
                return await context.accounts.ToListAsync();
            }
        }

        public async Task<List<channel_account>> GetChannelsAccounts()
        {
            using (var context = new PostgreDbContext(dbContextOptions))
            {
                return await context.channels_accounts.ToListAsync();
            }
        }

        public async Task<List<DMStartupSettings>> GetStatupData()
        {
            using (var context = new PostgreDbContext(dbContextOptions))
            {
                var query = from account in context.accounts
                            join channelAccount in context.channels_accounts on account.id equals channelAccount.account_id
                            join channel in context.channels on channelAccount.channel_id equals channel.id
                            join source in context.sources on channel.id equals source.channel_id
                            select new
                            {
                                source_name = source.source_name,
                                source_id = source.id,
                                account = account
                            };

                List<DMStartupSettings> res = new();

                foreach (var q in query)
                {
                    res.Add(new DMStartupSettings()
                    {
                        source_name = q.source_name,
                        source_id = q.source_id,
                        account = q.account
                    });
                }

                return res;
            }
        }

        public async Task<UserChat> CreateUserAndChat(Guid account_id, Guid source_id, telegram_user new_user)
        {
            UserChat res = new UserChat();

            try
            {

                using (var context = new PostgreDbContext(dbContextOptions))
                {

                    var foundChat = (from chat in context.telegram_chats
                                     join user in context.telegram_users
                                     on chat.telegram_user_id equals user.id
                                     where chat.account_id == account_id && user.telegram_id == new_user.telegram_id
                                     select chat).SingleOrDefault();

                    if (foundChat == null)
                    {

                        Guid telegram_user_id;

                        var foundUser = context.telegram_users.SingleOrDefault(u => u.telegram_id == new_user.telegram_id);
                        if (foundUser == null)
                        {
                            context.telegram_users.Add(new_user);
                            await context.SaveChangesAsync();
                            telegram_user_id = new_user.id;
                        }
                        else                        
                            telegram_user_id = foundUser.id;

                        var new_chat = new telegram_chat()
                        {
                            account_id = account_id,
                            source_id = source_id,
                            telegram_user_id = telegram_user_id,
                            chat_type = "private",
                            unread_count = 0,
                            unread_mark = false
                        };

                        context.telegram_chats.Add(new_chat);
                        await context.SaveChangesAsync();

                        res.is_new = true;
                        res.chat = new_chat;
                        res.user = new_user;

                    }
                    else
                    {
                        res.chat = foundChat;

                        var foundUser = context.telegram_users.SingleOrDefault(u => u.id == foundChat.telegram_user_id);
                        if (foundUser.access_hash == null && new_user.access_hash != null)
                        {
                            foundUser.access_hash = new_user.access_hash;
                            await context.SaveChangesAsync();
                        }

                        res.user = foundUser;
                    }

                }
            } catch (Exception ex)
            {

            }

            return res;
        }

        public async Task<UserChat?> GetUserChat(Guid account_id, long telegram_id)
        {

            UserChat? res = null;

            using (var context = new PostgreDbContext(dbContextOptions))
            {
                try
                {
                    var foundChat = await (from chat in context.telegram_chats
                                           join user in context.telegram_users
                                           on chat.telegram_user_id equals user.id
                                           where chat.account_id == account_id && user.telegram_id == telegram_id
                                           select chat).SingleOrDefaultAsync();

                    if (foundChat == null)
                        throw new KeyNotFoundException($"Chat account_id={account_id} telegram_id={telegram_id} not found");

                    var foundUser = await context.telegram_users.SingleOrDefaultAsync(u => u.id == foundChat.telegram_user_id);
                    if (foundUser == null)
                        throw new KeyNotFoundException($"Uset telegram_user_id={foundChat.telegram_user_id} not found");

                    res = new UserChat();
                    res.chat = foundChat;
                    res.user = foundUser;

                }
                catch (Exception ex)
                {
                    throw new Exception("GetUserChat error", ex);
                }

            }

            return res;
        }

        public async Task<UserChat?> GetUserChat(Guid account_id, Guid telegram_user_id)
        {

            UserChat? res = null;

            using (var context = new PostgreDbContext(dbContextOptions))
            {
                try
                {
                    var foundChat = await (from chat in context.telegram_chats
                                           join user in context.telegram_users
                                           on chat.telegram_user_id equals user.id
                                           where chat.account_id == account_id && user.id == telegram_user_id
                                           select chat).SingleOrDefaultAsync();

                    if (foundChat == null)
                        throw new KeyNotFoundException($"Chat account_id={account_id} telegram_user_id={telegram_user_id} not found");

                    var foundUser = await context.telegram_users.SingleOrDefaultAsync(u => u.id == foundChat.telegram_user_id);
                    if (foundUser == null)
                        throw new KeyNotFoundException($"Uset telegram_user_id={foundChat.telegram_user_id} not found");

                    res = new UserChat();
                    res.chat = foundChat;
                    res.user = foundUser;

                }
                catch (Exception ex)
                {
                    throw new Exception("GetUserChat error", ex);
                }

            }

            return res;
        }

        public async Task<telegram_chat> UpdateUnreadCount(Guid chat_id, int? unread_count = null, int? read_inbox_max_id = null, int? read_outbox_max_id = null)
        {
            using (var context = new PostgreDbContext(dbContextOptions))
            {
                try
                {
                    var foundChat = await context.telegram_chats.SingleOrDefaultAsync(ch => ch.id == chat_id);
                    if (foundChat == null)
                        throw new KeyNotFoundException($"Chat {chat_id} not found");

                    foundChat.unread_count = unread_count ?? foundChat.unread_count;
                    foundChat.unread_mark = foundChat.unread_count > 0;

                    foundChat.read_inbox_max_id = read_inbox_max_id ?? foundChat.read_inbox_max_id;
                    foundChat.read_outbox_max_id = read_outbox_max_id ?? foundChat.read_outbox_max_id;

                    await context.SaveChangesAsync();

                    return foundChat;

                } catch (Exception ex)
                {
                    throw new Exception($"UpdateUnreadCount error", ex);
                }
            }
        }

        public async Task<telegram_chat> UpdateUnreadCount(Guid chat_id, int? unread_inbox_count = null, int? read_inbox_max_id = null, int? unread_outbox_count = null, int? read_outbox_max_id = null)
        {
            using (var context = new PostgreDbContext(dbContextOptions))
            {
                try
                {
                    var foundChat = await context.telegram_chats.SingleOrDefaultAsync(ch => ch.id == chat_id);
                    if (foundChat == null)
                        throw new KeyNotFoundException($"Chat {chat_id} not found");

                    foundChat.unread_count = unread_inbox_count ?? foundChat.unread_count;
                    foundChat.unread_mark = foundChat.unread_count > 0;

                    foundChat.unread_inbox_count = unread_inbox_count ?? foundChat.unread_inbox_count;
                    foundChat.unread_inbox_mark = foundChat.unread_inbox_count > 0;
                    foundChat.read_inbox_max_id = read_inbox_max_id ?? foundChat.read_inbox_max_id;

                    foundChat.unread_outbox_count = unread_outbox_count ?? foundChat.unread_outbox_count;
                    foundChat.unread_outbox_mark = unread_outbox_count > 0;
                    foundChat.read_outbox_max_id = read_outbox_max_id ?? foundChat.read_outbox_max_id;

                    await context.SaveChangesAsync();

                    return foundChat;

                }
                catch (Exception ex)
                {
                    throw new Exception($"UpdateUnreadCount error", ex);
                }
            }
        }

        public async Task<telegram_chat> UpdateTopMessage(Guid chat_id, int top_message, string? top_message_text, DateTime top_message_date, bool? add_unread = null)
        {
            using (var context = new PostgreDbContext(dbContextOptions))
            {
                try
                {
                    var foundChat = await context.telegram_chats.SingleOrDefaultAsync(ch => ch.id == chat_id);
                    if (foundChat == null)
                        throw new KeyNotFoundException($"Chat {chat_id} not found");

                    foundChat.top_message = top_message;
                    foundChat.top_message_text = top_message_text;
                    foundChat.top_message_date = top_message_date;  

                    foundChat.unread_count = (add_unread == true) ? foundChat.unread_count + 1 : foundChat.unread_count;
                    foundChat.unread_mark = foundChat.unread_count > 0;

                    await context.SaveChangesAsync();

                    return foundChat;

                } catch (Exception ex)
                {
                    throw new Exception("UpdateTopMessage error", ex);
                }
            }
        }

        public async Task<telegram_chat> UpdateTopMessage(Guid chat_id, string direction, int top_message, string? top_message_text, DateTime top_message_date)
        {
            using (var context = new PostgreDbContext(dbContextOptions))
            {
                try
                {
                    var foundChat = await context.telegram_chats.SingleOrDefaultAsync(ch => ch.id == chat_id);
                    if (foundChat == null)
                        throw new KeyNotFoundException($"Chat {chat_id} not found");

                    foundChat.top_message = top_message;
                    foundChat.top_message_text = top_message_text;
                    foundChat.top_message_date = top_message_date;

                    switch (direction)
                    {
                        case "in":
                            foundChat.unread_count = foundChat.unread_count + 1;
                            foundChat.unread_mark = foundChat.unread_count > 0;
                            foundChat.unread_inbox_count = foundChat.unread_inbox_count + 1;
                            foundChat.unread_inbox_mark = foundChat.unread_inbox_count > 0;
                            break;
                        case "out":
                            foundChat.unread_outbox_count = foundChat.unread_outbox_count + 1;
                            foundChat.unread_outbox_mark = foundChat.unread_outbox_count > 0;
                            break;
                        default:
                            break;
                    }

                    await context.SaveChangesAsync();

                    return foundChat;

                }
                catch (Exception ex)
                {
                    throw new Exception("UpdateTopMessage error", ex);
                }
            }
        }

    }        
}

