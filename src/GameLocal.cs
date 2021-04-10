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
    public class GameLocal : Game
    {
        public Queue<string> GameTokenQueue { get; set; }
        public int OptimalQueueLength = 500;
        public int MaximalQueueLength = 600;

        public GameLocal(IConfigurationRoot configurationRoot, HttpClient client, bool verbose = false, bool debug = false)
            : base(configurationRoot, client)
        {
            Verbose = verbose;
            Debug = debug;
            GameTokenQueue = new Queue<string>();
        }

        public async Task Run(string gameToken = null, bool infinite = true, bool returnIfNotMyTurn = false, int workerNum = 0)
        {
            bool runFirstTime = true;
            GameStateResponse gameState = new GameStateResponse();
            bool waitingSuccessfull = true;
            while (infinite || runFirstTime)
            {
                runFirstTime = false;
                if (Debug)
                    Console.WriteLine($"1 runFirstTime {runFirstTime} infinite {infinite}");

                if (gameToken == null)
                {
                    Console.WriteLine($"WorkerNum: {workerNum} \tWaiting for game.");
                    gameToken = await ConnectSafe();
                }

                gameState = await CheckStatusSafe(gameToken);
                if (returnIfNotMyTurn && gameState.winnerId == null && CheckPlayer(gameState.actualPlayerId) == 2)
                {
                    Console.WriteLine($"WorkerNum: {workerNum} \tWaiting for other player, resuming.");
                    return;
                }

                if (gameState.winnerId == null)
                {
                    Console.WriteLine($"WorkerNum: {workerNum} \tStarting a new game.");
                    Console.WriteLine($"WorkerNum: {workerNum} \tgameToken: {gameToken}");
                    try
                    {
                        waitingSuccessfull = true;
                        while (gameState.winnerId == null && waitingSuccessfull)
                        {
                            (gameState, waitingSuccessfull) = await WaitTillGameIsReady(gameToken);
                            if (Verbose)
                                Console.WriteLine($"gameToken: {gameToken}");
                            if (waitingSuccessfull)
                                gameState = await PlayMove(gameToken, gameState);
                        }
                    }
                    catch (Exception e)
                    {
                        log.Information(gameToken);
                        log.Information(e.ToString());
                    }
                }
                Console.WriteLine($"WorkerNum: {workerNum} \tFinishing game, WinningPlayer: {CheckPlayer(gameState.winnerId)}");
            }
        }

        public async Task TokenFeeder()
        {
            string gameToken = null;
            while (true)
            {
                if (OptimalQueueLength <= GameTokenQueue.Count && GameTokenQueue.Count < MaximalQueueLength)
                {
                    gameToken = await ConnectSafe();
                    GameTokenQueue.Enqueue(gameToken);
                    Console.WriteLine($"TokenFeeder: \tLength of a queue {GameTokenQueue.Count}.");
                    await gameTokenValidator.UpdateTokenAsync(gameToken);
                    await Task.Delay(200);
                }
                else if (GameTokenQueue.Count < OptimalQueueLength)
                {
                    gameToken = await ConnectSafe();
                    GameTokenQueue.Enqueue(gameToken);
                    Console.WriteLine($"TokenFeeder: \tLength of a queue {GameTokenQueue.Count}.");
                    await gameTokenValidator.UpdateTokenAsync(gameToken);
                    await Task.Delay(100);
                }
                else
                {
                    await Task.Delay(100);
                }
            }
        }

        public async Task<string> WaitForTokenFeeder(int workerNum = 0)
        {
            if (GameTokenQueue.Count < OptimalQueueLength)
            {
                bool isLoaded = false;
                while (!isLoaded)
                {
                    Console.WriteLine($"WorkerNum: {workerNum} \tWaiting for TokenFeeder.");
                    await Task.Delay(1000);
                    isLoaded = GameTokenQueue.Count >= OptimalQueueLength;
                }
            }

            Random waitTimeGenerator = new Random();
            int waitTime = waitTimeGenerator.Next(100, 1000);
            await Task.Delay(waitTime);

            if (GameTokenQueue.Count == 0)
            {
                return "";
            }
            return GameTokenQueue.Dequeue();
        }


        public async Task RunWithTokenFeeder(int workerNum = 0, bool skipIfNotMyTurn = false)
        {
            string gameToken = null;
            GameStateResponse gameState = new GameStateResponse();
            bool waitingSuccessfull = true;
            while (true)
            {
                if (GameTokenQueue.Count == 0)
                {
                    return;
                }
                Console.WriteLine($"WorkerNum: {workerNum} \tWaiting for game.");
                gameToken = await WaitForTokenFeeder(workerNum: workerNum);
                if (gameToken == "")
                {
                    return;
                }
                gameState = await CheckStatusSafe(gameToken);
                if (skipIfNotMyTurn && gameState.winnerId == null && CheckPlayer(gameState.actualPlayerId) == 2)
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