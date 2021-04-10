using System;
using System.Collections.Generic;

namespace PiskvorkyJobsCZ
{
    public class GameStateResponse
    {
        public int statusCode { get; set; }
        public string playerCrossId { get; set; }
        public string playerCircleId { get; set; }
        public string actualPlayerId { get; set; }
        public string winnerId { get; set; }
        public List<Move> coordinates { get; set; }
    }

    public class ConnectResponse
    {
        public int statusCode { get; set; }
        public string gameToken { get; set; }
        public string gameId { get; set; }
    }

    public class Move 
    {
        public string playerId { get; set; }
        public int x { get; set; }
        public int y { get; set; }
    }
}
