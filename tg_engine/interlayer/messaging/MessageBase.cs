using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using System.ComponentModel;

namespace tg_engine.interlayer.messaging
{
    public class MessageBase
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string id { get; set; }
        public required Guid account_id { get; set; }
        public required Guid chat_id { get; set; }        
        public required string chat_type { get; set; }
        public string direction { get; set; }
        public long telegram_id { get; set; }
        public int telegram_message_id { get; set; }        
        public string? text { get; set; } = null;
        public string? screen_text { get; set; } = null;
        public List<Reaction> reactions { get; set; } = null;
        public DateTime date { get; set; }
        public DateTime? edited_date { get; set; } = null;
        public bool is_read { get; set; }
        public DateTime? read_date { get; set; } = null;
        public bool is_deleted { get; set; }
        public DateTime? deleted_date { get; set; } = null;
        public int? reply_to_message_id { get; set; } = null;
        public MediaInfo? media { get; set; } = null;
        public DateTime created_at { get; set; } = DateTime.UtcNow;
        public DateTime? updated_at { get; set; } = null;
        public bool is_business_bot_reply { get; set; } = false;
        public string? business_bot_username { get; set; } = null;
        public string? operator_id { get; set; } = null;
        public string? operator_letters { get; set;} = null;
    }

    


    //public class  Reaction 
    //{
    //    public string emoji { get; set; }
    //    public string count { get; set; }   
    //    public string is_my { get; set; }
    //}

    public class ReactionData
    {
        public string initials { get; set; }
        public string direction { get; set; }
    }

    public class Reaction 
    {
        public string emoji { get; set; }
        //public List<string> initials { get; set; } = new(); 
        public List<ReactionData> data  { get; set; } = new();
    }
}
