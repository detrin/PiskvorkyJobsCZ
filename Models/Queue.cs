using System;
using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.Bson;

namespace PiskvorkyJobsCZ
{
    public class GameState
    {
        [BsonId]
        public string GameToken { get; set; }
        public string PlayerCrossId { get; set; }
        public string PlayerCircleId { get; set; }
        public string ActualPlayerId { get; set; }
        public string WinnerId { get; set; }
    }
}