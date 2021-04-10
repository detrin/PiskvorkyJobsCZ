using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;

namespace PiskvorkyJobsCZ
{
    public class Game
    {
        public static string UserId { get; set; }
        public static string UserToken { get; set; }
        public int BoardSize = 61;
        public int BoardStartPos = -30;
        public EngineHandler engineHandler { get; set; }
        public GameTokenValidator gameTokenValidator { get; set; }
        public Dictionary<string, Move> MoveCache = new Dictionary<string, Move>();
        public static string BaseUrl = "https://piskvorky.jobs.cz";
        public bool Debug = false;
        public bool Verbose = false;
        public int CatchDelay = 5000;

        protected static HttpClient Client { get; set; } //new HttpClient();
        protected static IConfigurationRoot ConfigurationRoot { get; set; }
        protected Serilog.Core.Logger log;

        public Game(IConfigurationRoot configurationRoot, HttpClient client)
        {
            ConfigurationRoot = configurationRoot;
            UserToken = configurationRoot.GetSection("userToken").Get<string>();
            UserId = configurationRoot.GetSection("userId").Get<string>();
            if (Debug)
                Console.WriteLine($"[{UserToken}]");

            Client = client;

            engineHandler = new EngineHandler(configurationRoot);

            log = new LoggerConfiguration()
                .WriteTo.File("logExceptions.txt")
                .CreateLogger();

            gameTokenValidator = new GameTokenValidator(configurationRoot, Client);
        }

        public int CheckPlayer(string playerId)
        {
            int value = 0;
            if (playerId == null)
            {
                value = 0;
            }
            else if (playerId == UserId)
            {
                value = 1;
            }
            else
            {
                value = 2;
            }
            return value;
        }

        public Board PrepareBoard(GameStateResponse gameState)
        {
            Board board = new Board(BoardSize, BoardSize, BoardStartPos, BoardStartPos);
            int playerNum = 1;
            foreach (Move move in gameState.coordinates)
            {
                if (move.playerId == UserId)
                {
                    playerNum = 1;
                }
                else
                {
                    playerNum = 2;
                }

                board.Move(playerNum, move.x, move.y, relative: true);
                if (Debug)
                    Console.WriteLine($"{move.x} {move.y}");

            }
            return board;
        }

        public async Task<Stream> PostMethod(string url, Dictionary<string, object> payload)
        {
            string stringPayload = JsonSerializer.Serialize(payload);
            StringContent httpContent = new StringContent(stringPayload, Encoding.UTF8, "application/json");
            HttpResponseMessage httpResponse = await Client.PostAsync(BaseUrl + url, httpContent); //.GetAwaiter().GetResult()

            // httpResponse.EnsureSuccessStatusCode();
            if (httpResponse.Content != null)
            {
                if (Debug)
                {
                    string resultString = await httpResponse.Content.ReadAsStringAsync();
                    Console.WriteLine(resultString);
                }

                Stream resultStream = await httpResponse.Content.ReadAsStreamAsync();
                return resultStream;
            }
            return null;
        }

        public async Task<string> Connect()
        {
            var payload = new Dictionary<string, object> {
                {"userToken", UserToken}
            };
            Stream resultStream = await PostMethod("/api/v1/connect", payload);

            ConnectResponse response = await JsonSerializer.DeserializeAsync<ConnectResponse>(resultStream);

            
            if (Verbose)
                Console.WriteLine($"gameToken: {response.gameToken}");
            return response.gameToken;
        }

        public async Task<string> ConnectSafe()
        {
            bool success = true;
            string gameToken = null;
            try
            {
                gameToken = await Connect();
                success = true;
            }
            catch (Exception e)
            {
                if (e.ToString().Contains("System.Text.Json.JsonReaderException"))
                    Console.WriteLine("Failed web API request.");
                log.Information(e.ToString());
                success = false;
            }
            while (!success)
            {
                await Task.Delay(CatchDelay);
                try
                {
                    gameToken = await Connect();
                    success = true;
                }
                catch (Exception e)
                {
                    if (e.ToString().Contains("System.Text.Json.JsonReaderException"))
                        Console.WriteLine("Failed web API request.");
                    log.Information(e.ToString());
                    success = false;
                }
            }

            return gameToken;
        }

        public async Task<GameStateResponse> CheckStatus(string gameToken)
        {
            var payload = new Dictionary<string, object> {
                {"userToken", UserToken},
                {"gameToken", gameToken}
            };
            Stream resultStream = await PostMethod("/api/v1/checkStatus", payload);

            GameStateResponse response = await JsonSerializer.DeserializeAsync<GameStateResponse>(resultStream);

            return response;
        }

        public async Task<GameStateResponse> CheckStatusSafe(string gameToken)
        {
            bool success = true;
            GameStateResponse response = new GameStateResponse();
            try
            {
                response = await CheckStatus(gameToken);
                success = true;
            }
            catch (Exception e)
            {
                if (e.ToString().Contains("System.Text.Json.JsonReaderException"))
                    Console.WriteLine("Failed web API request.");
                log.Information(e.ToString());
                success = false;
            }
            while (!success)
            {
                await Task.Delay(CatchDelay);
                try
                {
                    response = await CheckStatus(gameToken);
                    success = true;
                }
                catch (Exception e)
                {
                    if (e.ToString().Contains("System.Text.Json.JsonReaderException"))
                        Console.WriteLine("Failed web API request.");
                    log.Information(e.ToString());
                    success = false;
                }
            }
            return response;
        }

        public async Task<GameStateResponse> SendMove(string gameToken, int moveX, int moveY)
        {
            var payload = new Dictionary<string, object> {
                    {"userToken", UserToken},
                    {"gameToken", gameToken},
                    {"positionX", moveX+BoardStartPos},
                    {"positionY", moveY+BoardStartPos}
                };
            Stream resultStream = await PostMethod("/api/v1/play", payload);

            GameStateResponse response = await JsonSerializer.DeserializeAsync<GameStateResponse>(resultStream);

            return response;
        }

        public async Task<GameStateResponse> SendMoveSafe(string gameToken, int moveX, int moveY)
        {
            bool success = true;
            GameStateResponse response = new GameStateResponse();
            try
            {
                response = await SendMove(gameToken, moveX, moveY);
                success = true;
            }
            catch (Exception e)
            {
                if (e.ToString().Contains("System.Text.Json.JsonReaderException"))
                    Console.WriteLine("Failed web API request.");
                log.Information(e.ToString());
                success = false;
            }
            while (!success)
            {
                await Task.Delay(CatchDelay);
                try
                {
                    response = await SendMove(gameToken, moveX, moveY);
                    success = true;
                }
                catch (Exception e)
                {
                    if (e.ToString().Contains("System.Text.Json.JsonReaderException"))
                        Console.WriteLine("Failed web API request.");
                    log.Information(e.ToString());
                    success = false;
                }
            }
            return response;
        }

        public async Task<(GameStateResponse, bool)> WaitTillGameIsReady(string gameToken)
        {
            GameStateResponse gs = await CheckStatusSafe(gameToken);
            int pauseCount = 0;
            if (gs.winnerId == null)
            {
                
                while (!(gs.actualPlayerId == UserId && gs.playerCrossId != null && gs.playerCircleId != null) && pauseCount < 15 && gs.winnerId == null)
                {
                    if (pauseCount < 10)
                        await Task.Delay(250);
                    else
                        await Task.Delay(4000);
                    gs = await CheckStatusSafe(gameToken);
                    if (Verbose)
                        Console.Write(".");
                    pauseCount++;
                }
                if (Verbose)
                    Console.WriteLine();
                PrepareBoard(gs);
            }
            return (gs, pauseCount < 15);
        }

        public async Task<(Board, GameStateResponse)> Resume(string gameToken)
        {
            Board board = new Board(BoardSize, BoardSize, BoardStartPos, BoardStartPos);
            GameStateResponse gameState = await CheckStatusSafe(gameToken);
            board = PrepareBoard(gameState);

            return (board, gameState);
        }

        public async Task<GameStateResponse> PlayMove(string gameToken, GameStateResponse gs)
        {
            if (gs.winnerId == null && gs.actualPlayerId == UserId)
            {
                Board board = PrepareBoard(gs);
                int moveX = 0; int moveY = 0;

                if (board.BoardIsEmpty)
                {
                    moveX = -BoardStartPos; moveY = -BoardStartPos;
                }
                else
                {
                    string boardString = board.BoardToString();
                    if (!MoveCache.ContainsKey(boardString))
                    {

                        EngineResponse result = await engineHandler.GetMoveAsync(
                                boardString, CheckPlayer(gs.actualPlayerId), threadNum: 1
                            );
                        
                        moveX = Int32.Parse(result.result["move_c"]);
                        moveY = Int32.Parse(result.result["move_r"]);
                        MoveCache[boardString] = new Move
                        {
                            playerId = null,
                            x = moveX,
                            y = moveY
                        };
                    }
                    else
                    {
                        Move move = MoveCache[boardString];
                        moveX = move.x;
                        moveY = move.y;
                    }

                    if (Debug)
                        Console.WriteLine($"{moveX} {moveY}");

                }
                gs = await SendMoveSafe(gameToken, moveX, moveY);

                if (Verbose)
                    Console.WriteLine($"{CheckPlayer(gs.actualPlayerId)} {moveX} {moveY}");

                if (Verbose)
                    board.PrintBoard(offset: 20);
            }
            return gs;
        }
    }
}