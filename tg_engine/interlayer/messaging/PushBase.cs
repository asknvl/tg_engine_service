using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tg_engine.interlayer.messaging
{
    public class PushBase
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string id { get; set; }        
        public string description {  get; set; }    
        public string text { get; set; }
        public MediaInfo media { get; set; } = null;        
    }
}
