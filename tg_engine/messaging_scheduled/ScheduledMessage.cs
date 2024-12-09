using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static tg_engine.rest.MessageUpdatesRequestProcessor;

namespace tg_engine.messaging_scheduled
{
    public class ScheduledMessage : messageDto
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string id { get; set; }
    }
}
