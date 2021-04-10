using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Bson;
using Serilog;

namespace PiskvorkyJobsCZ
{

    public class GameDatabase : Game
    {
        private MongoCRUD db { get; set; }
        public int OptimalQueueLength = 500;
        public int MaximalQueueLength = 600;

        public GameDatabase(IConfigurationRoot configurationRoot, HttpClient client, bool verbose = false, bool debug = false)
            : base(configurationRoot, client)
        {
            Verbose = verbose;
            Debug = debug;

            var configDatabase = configurationRoot.GetSection("Database").Get<Dictionary<string, string>>();
            string useSecret = configDatabase["useSecret"];
            db = new MongoCRUD(configDatabase[useSecret], "PiskvorkyJobsCZ");
        }

        public async Task AddToMaturingQueue(string gameToken)
        {
            GameStateResponse response = await CheckStatusSafe(gameToken);

            GameState gameState = new GameState
            {
                GameToken = gameToken, // new Guid(gameToken),
                PlayerCrossId = response.playerCrossId,
                PlayerCircleId = response.playerCircleId,
                ActualPlayerId = response.actualPlayerId,
                WinnerId = response.winnerId
            };

            GameState checkResponse = db.LoadRecordsById<GameState>("MaturingQueue", gameToken);
            if (checkResponse == null)
            {
                db.InsertRecord<GameState>("MaturingQueue", gameState);
            }
            else
            {
                db.UpsertRecord<GameState>("MaturingQueue", gameToken, gameState);
            }

        }

        public async Task FetchTokens()
        {
            string gameToken = null;
            while (true)
            {
                long currentQueueLength = await db.RecordsLengthAsync<GameState>("MaturingQueue");
                if (OptimalQueueLength <= currentQueueLength && currentQueueLength < MaximalQueueLength)
                {
                    gameToken = await ConnectSafe();
                    await AddToMaturingQueue(gameToken);
                    await gameTokenValidator.UpdateTokenAsync(gameToken);
                    Console.WriteLine($"TokenFetcher: \tcurrentQueueLength: {currentQueueLength + 1}");
                    await Task.Delay(100);
                }
                else if (currentQueueLength < OptimalQueueLength)
                {
                    gameToken = await ConnectSafe();
                    await AddToMaturingQueue(gameToken);
                    await gameTokenValidator.UpdateTokenAsync(gameToken);
                    Console.WriteLine($"TokenFetcher: \tcurrentQueueLength: {currentQueueLength + 1}");
                }
                else
                {
                    await Task.Delay(100);
                }
            }
        }

        public async Task WaitForMaturedTokens()
        {
            List<GameState> maturingGameStates = new List<GameState>();
            while (true)
            {
                maturingGameStates = await db.LoadRecordsAsync<GameState>("MaturingQueue");
                foreach (GameState gs in maturingGameStates)
                {
                    GameStateResponse response = await CheckStatusSafe(gs.GameToken);
                    GameState gs_new = new GameState
                    {
                        GameToken = gs.GameToken, // new Guid(gameToken),
                        PlayerCrossId = response.playerCrossId,
                        PlayerCircleId = response.playerCircleId,
                        ActualPlayerId = response.actualPlayerId,
                        WinnerId = response.winnerId
                    };

                    if (gs_new.PlayerCrossId != null & gs_new.PlayerCircleId != null && gs_new.ActualPlayerId == UserId)
                    {
                        db.DeleteRecord<GameState>("MaturingQueue", gs.GameToken);
                        GameState gsWaiting = db.LoadRecordsById<GameState>("WaitingQueue", gs.GameToken);
                        if (gsWaiting == null)
                        {
                            db.InsertRecord<GameState>("WaitingQueue", gs_new);
                        }
                        else
                        {
                            db.UpsertRecord<GameState>("WaitingQueue", gs.GameToken, gs_new);
                        }
                        Console.WriteLine($"TokenMaturer: \tPassed token: {gs.GameToken}");
                    }
                    await Task.Delay(100);
                }
                await Task.Delay(500);
            }
        }

        public async Task Run(int workerNum = 0)
        {
            GameStateResponse gameState = new GameStateResponse();
            string gameToken = null;
            bool waitingSuccessfull = true;
            while (true)
            {
                long currentQueueLength = await db.RecordsLengthAsync<GameState>("WaitingQueue");
                if (currentQueueLength == 0)
                {
                    Console.WriteLine($"WorkerNum: {workerNum} \tWaiting for game.");
                    await Task.Delay(500);
                }
                else
                {
                    var gameStateWrapper = await db.LoadRecordAsync<GameState>("WaitingQueue");
                    db.DeleteRecord<GameState>("WaitingQueue", gameStateWrapper.GameToken);
                    gameToken = gameStateWrapper.GameToken;
                    gameState = await CheckStatusSafe(gameToken);

                    if (gameState.winnerId == null && CheckPlayer(gameState.actualPlayerId) == 2)
                    {
                        Console.WriteLine($"WorkerNum: {workerNum} \tWaiting for other player, skipping.");
                        continue;
                    }

                    if (gameState.winnerId == null)
                    {
                        Console.WriteLine($"WorkerNum: {workerNum} \tStarting a new game.");
                        Console.WriteLine($"WorkerNum: {workerNum} \tgameToken: {gameToken}");

                        waitingSuccessfull = true;
                        while (gameState.winnerId == null && waitingSuccessfull)
                        {
                            (gameState, waitingSuccessfull) = await WaitTillGameIsReady(gameToken);
                            if (Verbose)
                                Console.WriteLine($"gameToken: {gameToken}");
                            if (waitingSuccessfull)
                                gameState = await PlayMove(gameToken, gameState);
                            await Task.Delay(333);
                        }
                    }
                    Console.WriteLine($"WorkerNum: {workerNum} \tFinishing game, WinningPlayer: {CheckPlayer(gameState.winnerId)}");
                }
            }
        }
    }
}