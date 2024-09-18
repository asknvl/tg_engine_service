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
        public required Guid account_id { get; set; }
        public required Guid chat_id { get; set; }        
        public required string chat_type { get; set; }
        public string direction { get; set; }
        public long telegram_id { get; set; }
        public int telegram_message_id { get; set; }        
        public string? text { get; set; }
        public DateTime date { get; set; }
        public DateTime? edited_date { get; set; } = null;
        public bool is_read { get; set; }
        public DateTime? read_date { get; set; } = null;
        public bool is_deleted { get; set; }
        public DateTime? deleted_date { get; set; } = null;
        public int? reply_to_message_id { get; set; }
        public MediaInfo? media { get; set; }
        public DateTime created_at { get; set; } = DateTime.UtcNow;
        public DateTime? updated_at { get; set; } = null;
        public bool is_business_bot_reply { get; set; } = false;
        public string? business_bot_username { get; set; } = null;
        public string? operator_id { get; set; } = null;
    }

    public class MediaInfo
    {        
        public string type { get; set; }  
        public string? file_name { get; set; }
        public string? extension { get; set; }
        public long? length { get; set; }
        public double? duration { get; set; }        
        public int? width { get; set;}
        public int? height { get; set;}   
        public string storage_id { get; set; }
        public string storage_url { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is MediaInfo other)
            {
                return type == other.type &&
                       file_name == other.file_name &&
                       extension == other.extension &&
                       length == other.length &&
                       duration == other.duration &&
                       width == other.width &&
                       height == other.height;
                       
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(type,
                                    file_name,
                                    extension,
                                    length,
                                    duration);
        }
    }

    public static class MediaTypes
    {
        public const string image = "image";
        public const string circle = "circle";
        public const string photo = "photo";
        public const string video = "video";
        public const string voice = "voice";
        public const string sticker = "sticker";
    }   
}
