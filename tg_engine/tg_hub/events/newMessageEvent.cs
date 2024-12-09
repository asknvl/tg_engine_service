﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tg_engine.database.postgre.dtos;
using IL = tg_engine.interlayer.messaging;

namespace tg_engine.tg_hub.events
{
    public class newMessageEvent : EventBase
    {
        [JsonIgnore]
        public override string path => "events/new-message";
        public newMessageEvent(UserChat userChat, IL.MessageBase message) : base(userChat) {

            data = new messageData()
            {
                id = message.id,
                chat_id = message.chat_id,
                direction = message.direction,                
                telegram_id = message.telegram_id,
                telegram_message_id = message.telegram_message_id,
                text = message.text,
                screen_text = message.screen_text,
                date = message.date,
                edited_date = message.edited_date,
                reply_to_message_id = message.reply_to_message_id,
                media = message.media,
                reactions = message.reactions,
                is_business_bot_reply = message.is_business_bot_reply,
                business_bot_username = message.business_bot_username,
                operator_id = message.operator_id,
                operator_letters = message.operator_letters,
                created_at = message.created_at,
                updated_at = message.updated_at
            };
        }
    }

    class messageData()
    {
        public Guid chat_id { get; set; }
        public string direction { get; set; }
        public string id { get; set; }    
        public long telegram_id { get; set; }
        public int telegram_message_id { get; set; }
        public string? text { get; set; }
        public string? screen_text { get; set; }
        public DateTime? date { get; set; }
        public DateTime? edited_date { get; set; }
        public int? reply_to_message_id { get; set; }
        public IL.MediaInfo? media { get; set; }
        public List<IL.Reaction>? reactions { get; set; }
        public bool is_business_bot_reply { get; set; }
        public string? business_bot_username { get; set; }
        public string? operator_id { get; set; }
        public string? operator_letters { get; set; }   
        public DateTime created_at { get; set; }
        public DateTime? updated_at { get; set; }
    }
}
