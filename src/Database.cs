using System;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Text;
using Serilog;

namespace PiskvorkyJobsCZ
{
    public class GameTokenValidator
    {
        private IConfigurationRoot ConfigurationRoot { get; set; }
        private MongoCRUD db { get; set; }
        private string UserToken { get; set; }
        private string UserId { get; set; }
        private Guid UserGuid { get; set; }
        private HttpClient Client { get; set; }
        private string BaseUrl = "https://piskvorky.jobs.cz";
        private Serilog.Core.Logger log;
        private int CatchDelay = 2000;
        private int MaxConcurrentRequests = 50;

        public GameTokenValidator(IConfigurationRoot configurationRoot, HttpClient client)
        {
            ConfigurationRoot = configurationRoot;
            Client = client;

            var configDatabase = configurationRoot.GetSection("Database").Get<Dictionary<string, string>>();
            string useSecret = configDatabase["useSecret"];
            db = new MongoCRUD(configDatabase[useSecret], "PiskvorkyJobsCZ");

            UserToken = configurationRoot.GetSection("userToken").Get<string>();
            UserId = configurationRoot.GetSection("userId").Get<string>();
            UserGuid = new Guid(UserId);

            log = new LoggerConfiguration()
                .WriteTo.File("logExceptions.txt")
                .CreateLogger();

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

        public async Task<Stream> PostMethod(string url, Dictionary<string, object> payload)
        {
            string stringPayload = JsonSerializer.Serialize(payload);
            StringContent httpContent = new StringContent(stringPayload, Encoding.UTF8, "application/json");
            HttpResponseMessage httpResponse = await Client.PostAsync(BaseUrl + url, httpContent); //.GetAwaiter().GetResult()

            // httpResponse.EnsureSuccessStatusCode();
            if (httpResponse.Content != null)
            {

                Stream resultStream = await httpResponse.Content.ReadAsStreamAsync();
                return resultStream;
            }
            return null;
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

        public void AddToken(string gameToken, GameStateResponse gameStateResponse)
        {
            GameState gameState = new GameState
            {
                GameToken = gameToken, // new Guid(gameToken),
                PlayerCrossId = gameStateResponse.playerCrossId,
                PlayerCircleId = gameStateResponse.playerCircleId,
                ActualPlayerId = gameStateResponse.actualPlayerId,
                WinnerId = gameStateResponse.winnerId
            };
            

            GameState response = db.LoadRecordsById<GameState>("GameTokens", gameToken);

            if (response == null)
            {
                db.InsertRecord<GameState>("GameTokens", gameState);
            }
            else
            {
                db.UpsertRecord<GameState>("GameTokens", gameToken, gameState);
            }
        }

        public void UpdateToken(string gameToken)
        {
            GameStateResponse response = CheckStatusSafe(gameToken).GetAwaiter().GetResult();
            AddToken(gameToken, response);
        }

        public async Task UpdateTokenAsync(string gameToken)
        {
            GameStateResponse response = await CheckStatusSafe(gameToken);
            AddToken(gameToken, response);
        }

        public void UpdateAllTokens()
        {
            List<GameState> gameStates = db.LoadRecords<GameState>("GameTokens");
            int gameStateNum = 0;
            foreach (GameState gameState in gameStates)
            {
                UpdateToken(gameState.GameToken); 
                gameStateNum++;
            }
        }

        public void UpdateAllTokensAsync()
        {
            List<GameState> gameStates = db.LoadRecords<GameState>("GameTokens");
            int gameStateNum = 0;
            int gameStateInd = 0;
            while (gameStateNum < gameStates.Count)
            {
                gameStateInd = 0;
                List<Task> gameUpdateTasks = new List<Task>();
                while (gameStateNum < gameStates.Count && gameStateInd < MaxConcurrentRequests)
                {
                    gameUpdateTasks.Add(UpdateTokenAsync(gameStates[gameStateNum].GameToken));
                    gameStateInd++;
                    gameStateNum++;
                }
                Task.WaitAll(gameUpdateTasks.ToArray());
            }
        }

        public void RemoveFinishedGames()
        {
            List<GameState> gameStates = db.LoadRecords<GameState>("GameTokens");

            foreach (GameState gameState in gameStates)
            {
                if (gameState.WinnerId != null)
                {
                    db.DeleteRecord<GameState>("GameTokens", gameState.GameToken);
                }
            }
        }

        public List<string> FetchTokensMyTurn()
        {
            List<GameState> gameStates = db.LoadRecords<GameState>("GameTokens");
            List<string> fetchedTokens = new List<string>();

            foreach (GameState gameState in gameStates)
            {
                if (CheckPlayer(gameState.ActualPlayerId) == 1 &&
                    CheckPlayer(gameState.PlayerCrossId) * CheckPlayer(gameState.PlayerCircleId) != 0)
                {
                    fetchedTokens.Add(gameState.GameToken);
                }
            }

            return fetchedTokens;
        }

    }
}