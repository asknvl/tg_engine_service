using Microsoft.EntityFrameworkCore;
using tg_engine.config;
using tg_engine.database.postgre.dtos;
using tg_engine.database.postgre.models;
using tg_engine.dm;
using tg_engine.interlayer.chats;
using tg_engine.rest.updates;
using TL;

namespace tg_engine.database.postgre
{
    public class PostgreProvider : IPostgreProvider
    {
        #region vars
        public readonly DbContextOptions<PostgreDbContext> dbContextOptions;
        #endregion

        #region properties
        public DbContextOptions<PostgreDbContext> DbContextOptions => dbContextOptions;
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
                                direction_id = source.direction_id,
                                account = account
                            };

                List<DMStartupSettings> res = new();

                foreach (var q in query)
                {
                    res.Add(new DMStartupSettings()
                    {
                        source_name = q.source_name,
                        source_id = q.source_id,
                        direction_id = q.direction_id,
                        account = q.account
                    });
                }

                return res;
            }
        }

      
        public async Task<UserChat> CreateUserAndChat(Guid account_id, Guid source_id, telegram_user new_user, long access_hash, string type)
        {
            UserChat res = new UserChat();

            try
            {

                var foundHash = await CreateOrUpdateAccessHash(account_id, new_user.telegram_id, access_hash);
                res.access_hash = foundHash.access_hash;

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
                            chat_type =  type,
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
                        if (foundChat.source_id == null)
                        {
                            foundChat.source_id = source_id;
                            await context.SaveChangesAsync();
                        }

                        if (!foundChat.chat_type.Equals(type))
                        {
                            foundChat.chat_type = type;
                            await context.SaveChangesAsync();
                        }

                        res.chat = foundChat;

                        var foundUser = await context.telegram_users.SingleOrDefaultAsync(u => u.id == foundChat.telegram_user_id);
                        if (foundUser != null)
                        {                           

                            var nfn = new_user.firstname;
                            var nln = new_user.lastname;
                            var nun = new_user.username;                            

                            var fn = foundUser.firstname;
                            var ln = foundUser.lastname;    
                            var un = foundUser.username;

                            var needUpdate = (!string.IsNullOrEmpty(nfn) && !nfn.Equals(fn)) ||
                                             (!string.IsNullOrEmpty(nln) && !nln.Equals(ln)) ||
                                             (!string.IsNullOrEmpty(nun) && !nun.Equals(un));

                            if (needUpdate)
                            {
                                foundUser.firstname = nfn;
                                foundUser.lastname = nln;
                                foundUser.username = nun;

                                await context.SaveChangesAsync();
                            }

                            res.user = foundUser;
                        }
                    }

                }
            }
            catch (Exception ex)
            {

            }

            return res;
        }

        public async Task<telegram_access_hash> CreateOrUpdateAccessHash(Guid account_id, long telegram_id, long access_hash) {

            telegram_access_hash foundHash;

            using (var context = new PostgreDbContext(dbContextOptions))
            {
                foundHash = await context.access_hashes.SingleOrDefaultAsync(h => h.account_id == account_id && h.telegram_id == telegram_id);
                if (foundHash == null)
                {
                    foundHash = new telegram_access_hash()
                    {
                        account_id = account_id,
                        telegram_id = telegram_id,
                        access_hash = access_hash
                    };

                    context.access_hashes.Add(foundHash);

                    await context.SaveChangesAsync();
                } else
                {                    
                    if (foundHash.access_hash != access_hash)
                    {
                        foundHash.access_hash = access_hash;
                        await context.SaveChangesAsync();
                    }
                }
            }

            return foundHash;
        }

        public async Task<telegram_access_hash?> GetAccessHash(Guid account_id, long telegram_id)
        {
            telegram_access_hash? res = null;

            using (var context = new PostgreDbContext(dbContextOptions))
            {
                res = await context.access_hashes.SingleOrDefaultAsync(h => h.account_id == account_id && h.telegram_id == telegram_id);                
            }
            return res;
        }

        public async Task UpdateChatType(Guid chat_id, string chat_type)
        {
            using (var context = new PostgreDbContext(dbContextOptions))
            {
                var foundChat = await context.telegram_chats.SingleOrDefaultAsync(c => c.id == chat_id);
                if (foundChat != null)
                {
                    foundChat.chat_type = chat_type;
                    await context.SaveChangesAsync();
                }
            }
        }

        public async Task UpdateUser(Guid chat_id, string? fn, string? ln, string? un)
        {
            using (var context = new PostgreDbContext(dbContextOptions))
            {
                var foundChat = await context.telegram_chats.SingleOrDefaultAsync(c => c.id == chat_id);
                if (foundChat != null)
                {
                    var foundUser = await context.telegram_users.SingleOrDefaultAsync(u => u.id == foundChat.telegram_user_id);
                    if (foundUser != null)
                    {
                        foundUser.firstname = fn;
                        foundUser.lastname = ln;
                        foundUser.username = un;

                        await context.SaveChangesAsync();
                    }
                }
            }
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
                        throw new KeyNotFoundException($"User telegram_user_id={foundChat.telegram_user_id} not found");

                    res = new UserChat();
                    res.chat = foundChat;
                    res.user = foundUser;

                    var access_hash = await GetAccessHash(account_id, foundUser.telegram_id);
                    res.access_hash = access_hash.access_hash;
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
                        throw new KeyNotFoundException($"User telegram_user_id={foundChat.telegram_user_id} not found");

                    res = new UserChat();
                    res.chat = foundChat;
                    res.user = foundUser;

                    var access_hash = await GetAccessHash(account_id, foundUser.telegram_id);
                    res.access_hash = access_hash.access_hash;

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

        //public async Task<telegram_chat> UpdateTopMessage(Guid chat_id, int top_message, string? top_message_text, DateTime top_message_date, bool? add_unread = null)
        //{
        //    using (var context = new PostgreDbContext(dbContextOptions))
        //    {
        //        try
        //        {
        //            var foundChat = await context.telegram_chats.SingleOrDefaultAsync(ch => ch.id == chat_id);
        //            if (foundChat == null)
        //                throw new KeyNotFoundException($"Chat {chat_id} not found");

        //            foundChat.top_message = top_message;
        //            foundChat.top_message_text = top_message_text;
        //            foundChat.top_message_date = top_message_date;  

        //            foundChat.unread_count = (add_unread == true) ? foundChat.unread_count + 1 : foundChat.unread_count;
        //            foundChat.unread_mark = foundChat.unread_count > 0;

        //            await context.SaveChangesAsync();

        //            return foundChat;

        //        } catch (Exception ex)
        //        {
        //            throw new Exception("UpdateTopMessage error", ex);
        //        }
        //    }
        //}

        public async Task<telegram_chat> UpdateTopMessage(Guid chat_id, string direction,
                                                          int top_message,
                                                          string? top_message_text,
                                                          DateTime? top_message_date,
                                                          bool igonreUnread = false)
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

                    if (!igonreUnread)
                    {

                        foundChat.unread_inbox_count = foundChat.unread_inbox_count ?? 0;
                        foundChat.unread_outbox_count = foundChat.unread_inbox_count ?? 0;
                        foundChat.unread_inbox_mark = foundChat.unread_inbox_count > 0;
                        foundChat.unread_outbox_mark = foundChat.unread_outbox_count > 0;

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

        public async Task<storage_file_parameter?> GetFileParameters(string hash)
        {
            storage_file_parameter? result = null;

            try
            {
                using (var context = new PostgreDbContext(dbContextOptions))
                {
                    result = await context.storage_file_parameters.SingleOrDefaultAsync(p => p.hash == hash);
                }

            } catch (Exception ex)
            {               
                //TODO логгирование!!!
            }

            return result;
        }

        public async Task CreateFileParameters(storage_file_parameter parameters)
        {
            try
            {
                using (var context = new PostgreDbContext(dbContextOptions))
                {
                    context.storage_file_parameters.Add(parameters);
                    await context.SaveChangesAsync();
                }
            } catch (Exception ex)
            {
                //TODO логгирование!!!
            }
        }

        //public async Task<telegram_chat> SetAIStatus(Guid chat_id, bool status)
        //{
        //    using (var context = new PostgreDbContext(dbContextOptions))
        //    {
        //        try
        //        {
        //            var foundChat = await context.telegram_chats.SingleOrDefaultAsync(ch => ch.id == chat_id);
        //            if (foundChat == null)
        //                throw new KeyNotFoundException($"Chat {chat_id} not found");

        //            foundChat.is_ai_active = status;

        //            await context.SaveChangesAsync();

        //            return foundChat;

        //        }
        //        catch (Exception ex)
        //        {
        //            throw new Exception($"UpdateUnreadCount error", ex);
        //        }
        //    }
        //}

        public async Task<telegram_chat> SetAIStatus(Guid chat_id, bool status)
        {
            using (var context = new PostgreDbContext(dbContextOptions))
            {
                try
                {
                    var foundChat = await context.telegram_chats.SingleOrDefaultAsync(ch => ch.id == chat_id);
                    if (foundChat == null)
                        throw new KeyNotFoundException($"Chat {chat_id} not found");

                    foundChat.is_ai_active = status;
                    foundChat.ai_status = (status) ? (int)AIStatuses.on : (int)AIStatuses.off;

                    if (!status)                    
                        foundChat.ai_deactivate_date = DateTime.UtcNow;
                    else
                        foundChat.ai_processed = true;
                    

                    await context.SaveChangesAsync();

                    return foundChat;

                }
                catch (Exception ex)
                {
                    throw new Exception($"SetAIStatus error", ex);
                }
            }
        }

        public async Task<telegram_chat> SetAIStatus(Guid chat_id, int status)
        {
            using (var context = new PostgreDbContext(dbContextOptions))
            {
                try
                {
                    var foundChat = await context.telegram_chats.SingleOrDefaultAsync(ch => ch.id == chat_id);
                    if (foundChat == null)
                        throw new KeyNotFoundException($"Chat {chat_id} not found");

                    if (Enum.IsDefined(typeof(AIStatuses), status))
                    {
                        foundChat.ai_status = status;

                        switch (status)
                        {
                            case (int)AIStatuses.off:
                            case (int)AIStatuses.done:
                                foundChat.ai_deactivate_date = DateTime.UtcNow;
                                break;
                            case (int)AIStatuses.on:
                                foundChat.ai_processed = true;
                                break;
                        }

                        await context.SaveChangesAsync();
                    }                    

                    return foundChat;

                }
                catch (Exception ex)
                {
                    throw new Exception($"SetAIStatus error", ex);
                }
            }
        }


    }        
}

