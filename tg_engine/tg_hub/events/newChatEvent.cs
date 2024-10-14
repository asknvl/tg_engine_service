using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tg_engine.database.postgre.dtos;
using tg_engine.database.postgre.models;

namespace tg_engine.tg_hub.events
{
    public class newChatEvent : EventBase
    {
        [JsonIgnore]
        public override string path => "events/new-chat";

        public newChatEvent(UserChat userChat, Guid source_id, string source_name) : base(userChat)
        {
            data = new chatData()
            {
                id = userChat.chat.id,
                chat_type = userChat.chat.chat_type,
                account_id = userChat.chat.account_id,
                telegram_user_id = userChat.chat.telegram_user_id,
                is_deleted = userChat.chat.is_deleted,
                unread_count = userChat.chat.unread_count,
                unread_mark = userChat.chat.unread_mark,
                top_message = userChat.chat.top_message,
                top_message_text = userChat.chat.top_message_text,
                top_message_date = userChat.chat.top_message_date,
                read_inbox_max_id = userChat.chat.read_inbox_max_id,
                read_outbox_max_id = userChat.chat.read_outbox_max_id,
                unread_inbox_count = userChat.chat.unread_inbox_count,
                unread_inbox_mark = userChat.chat.unread_inbox_mark,
                unread_outbox_count = userChat.chat.unread_outbox_count,
                unread_outbox_mark = userChat.chat.unread_outbox_mark,
                is_ai_active = userChat.chat.is_ai_active,                

                user = new userData()
                {
                    id = userChat.user.id,
                    telegram_id = userChat.user.telegram_id,
                    firstname = userChat.user.firstname,    
                    lastname = userChat.user.lastname,
                    username = userChat.user.username
                },

                source = new sourceData()
                {                    
                    id = source_id,
                    source_name = source_name
                }

            };
        }      
    }

    class chatData
    {
        public Guid id { get; set; }
        public Guid account_id { get; set; }
        public Guid telegram_user_id { get; set; }
        public bool is_deleted { get; set; }
        public string chat_type { get; set; }
        public int? unread_count { get; set; } = 0;
        public bool? unread_mark { get; set; }
        public int? top_message { get; set; }
        public string? top_message_text { get; set; }
        public DateTime? top_message_date { get; set; }
        public int? read_inbox_max_id { get; set; }
        public int? read_outbox_max_id { get; set; }
        public int? unread_inbox_count { get; set; }
        public bool? unread_inbox_mark { get; set; }
        public int? unread_outbox_count { get; set; }
        public bool? unread_outbox_mark { get; set; }
        public bool is_ai_active { get; set; }

        public userData user { get; set; } 
        public sourceData source { get; set; }
    }

    class userData
    {
        public Guid id { get; set; }
        public long telegram_id { get; set; }       
        public string? firstname { get; set; }
        public string? lastname { get; set; }
        public string? username { get; set; }

    }

    class sourceData 
    {
        public Guid id { get; set; }            
        public string source_name { get; set; }

    }
}
