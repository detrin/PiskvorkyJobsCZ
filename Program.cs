using System;
using System.Collections;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using System.Net.Http;
using System.IO;
using System.Collections.Generic;

namespace PiskvorkyJobsCZ
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: dotnet run (cleanup|local|distributed_fetcher|distributed_worker)");
                Console.WriteLine("  - cleanup: Clean the queue in GameTokens collection.");
                Console.WriteLine("  - local: Run locall app with local queue.");
                Console.WriteLine("  - distributed_fetcher: Run app in distributed mode in game fetcher mode.");
                Console.WriteLine("  - distributed_worker: Run app in distributed mode in game runner mode.");
                return 1;
            }

            string appState = args[0];
            var config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json").Build();
            int workersNum = config.GetSection("workersNum").Get<int>();

            HttpClient Client = new HttpClient();
            Client.Timeout = TimeSpan.FromMinutes(10);

            if (appState == "cleanup")
            {
                var gameTokenValidator = new GameTokenValidator(config, Client);
                gameTokenValidator.UpdateAllTokensAsync();
                gameTokenValidator.RemoveFinishedGames();

                List<string> fetchedTokens = gameTokenValidator.FetchTokensMyTurn();
                GameLocal game = new GameLocal(config, Client, verbose: false, debug: false);
                game.OptimalQueueLength = 0;
                game.MaximalQueueLength = 0;

                foreach (string gameToken in fetchedTokens)
                {
                    game.GameTokenQueue.Enqueue(gameToken);
                }

                List<Task> gameTasks = new List<Task>();
                for (int workerNum = 0; workerNum < workersNum; workerNum++)
                {
                    gameTasks.Add(game.RunWithTokenFeeder(workerNum: workerNum, skipIfNotMyTurn: true));
                }
                Task.WaitAll(gameTasks.ToArray());
                gameTokenValidator.UpdateAllTokensAsync();
                gameTokenValidator.RemoveFinishedGames();

            }
            else if (appState == "local")
            {
                GameLocal game = new GameLocal(config, Client, verbose: false, debug: false);
                List<Task> gameTasks = new List<Task>();
                gameTasks.Add(game.TokenFeeder());
                Thread.Sleep(2000);

                for (int workerNum = 1; workerNum < workersNum + 1; workerNum++)
                {
                    gameTasks.Add(game.RunWithTokenFeeder(workerNum: workerNum));
                    Thread.Sleep(2000);
                }
                Task.WaitAll(gameTasks.ToArray());
            }
            else if (appState == "distributed_fetcher")
            {
                
                GameDatabase game = new GameDatabase(config, Client, verbose: false);
                List<Task> gameTasks = new List<Task>();
                gameTasks.Add(game.FetchTokens());
                gameTasks.Add(game.WaitForMaturedTokens());
                Task.WaitAll(gameTasks.ToArray());
            }
            else if (appState == "distributed_worker")
            {
                GameDatabase game = new GameDatabase(config, Client, verbose: false);
                List<Task> gameTasks = new List<Task>();

                for (int workerNum = 1; workerNum < workersNum + 1; workerNum++)
                {
                    gameTasks.Add(game.Run(workerNum: workerNum));
                }
                Task.WaitAll(gameTasks.ToArray());
            }
            return 0;
        }
    }
}
