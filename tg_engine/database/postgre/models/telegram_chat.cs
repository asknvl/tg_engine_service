﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tg_engine.database.postgre.models
{
    public class telegram_chat
    {
        [Key]
        public Guid id { get; set; }
        public Guid account_id { get; set; }
        public Guid telegram_user_id { get; set; }
        public string chat_type { get; set; }
        public bool? unread_mark { get; set; }
        public int? top_message { get; set; }
        public int? read_inbox_max_id { get; set; }
        public int? read_outbox_max_id { get; set; }
        public int? unread_count { get; set; } = 0;
    }
}
