using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tg_engine.interlayer.messaging
{
    public class MessageBase
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string id { get; set; }
        public Guid chat_id { get; set; }
        public string direction { get; set; }
        public long? telegram_id { get; set; } = null;
        public int telegram_message_id { get; set; }
        //public long? src_telegram_user_id { get; set; } = null;
        //public long? dst_telegram_user_id { get; set; } = null;        
        public string? text { get; set; }   
        public DateTime date { get; set; }
        public DateTime? edited_date { get; set; } = null;
        public bool is_read { get; set; }
        public int? reply_to_message_id { get; set; }
        public List<MediaInfo>? media { get; set; }
        public DateTime created_at { get; set; } = DateTime.UtcNow;
        public DateTime? updated_at { get; set; } = null;
    }

    public class MediaInfo
    {
        public string type { get; set; }
        public string url { get; set; }
        public string file_id { get; set; }
    }
}
