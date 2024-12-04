using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tg_engine.interlayer.messaging
{
    public class MediaInfo
    {
        public string type { get; set; }
        public string? file_name { get; set; }
        public string? extension { get; set; }
        public long? length { get; set; }
        public double? duration { get; set; }
        public int? width { get; set; }
        public int? height { get; set; }
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
