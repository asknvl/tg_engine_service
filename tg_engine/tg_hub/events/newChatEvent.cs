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
        public override string path => "events/new-chat";

        public newChatEvent(UserChat userChat)
        {
            account_id = userChat.chat.account_id;
            chat_id = userChat.chat.id;
            telegram_id = "" + userChat.user.telegram_id;

            data = new chatData() {
                firstname = userChat.user.firstname,
                lastname = userChat.user.lastname,
                username = userChat.user.username
            };
        }      
    }

    class chatData
    {
        public string? firstname { get; set; }
        public string? lastname { get; set; }
        public string? username { get; set; }
    }
}
