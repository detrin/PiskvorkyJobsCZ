using System;
using System.Net;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace PiskvorkyJobsCZ
{
    public class EngineHandler
    {
        private static HttpClient Client = new HttpClient();
        private EngineResponse engineResult = new EngineResponse();
        protected static IConfigurationRoot ConfigurationRoot { get; set; }
        private string EngineUrl { get; set; }
        private string EnginePath { get; set; }
        private string UseSettings { get; set; }

        public EngineHandler(IConfigurationRoot configurationRoot)
        {
            ConfigurationRoot = configurationRoot;

            var configEngine = configurationRoot.GetSection("Engine").Get<Dictionary<string, string>>();
            EngineUrl = configEngine["url"];
            EnginePath = configEngine["path"];
            UseSettings = configEngine["useSettings"];
        }

        public async Task<EngineResponse> GetResponse(string path)
        {
            var httpResponse = await Client.GetAsync(path, HttpCompletionOption.ResponseHeadersRead);

            httpResponse.EnsureSuccessStatusCode(); // throws if not 200-299
            if (httpResponse.Content is object)
            {
                Stream contentStream = await httpResponse.Content.ReadAsStreamAsync();

                try
                {
                    return await JsonSerializer.DeserializeAsync<EngineResponse>(contentStream);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
            else
            {
                Console.WriteLine("HTTP Response was invalid and cannot be deserialised.");
            }

            return null;
        }

        private Process SpawnEngineProcess(string board, int player, int threadNum = 12, int depth = 10)
        {           
            ProcessStartInfo startInfo = new ProcessStartInfo(EnginePath, $" -s {board} -p {player} -t {threadNum} -d {depth}")
            {
                RedirectStandardOutput = true
            };

            return Process.Start(startInfo);
        }

        private async Task<EngineResponse> EngineCommand(string board, int player, int threadNum = 12, int depth = 10)
        {
            using (var process = SpawnEngineProcess(board, player, threadNum, depth))
            {
                await process.WaitForExitAsync();
                if (process.ExitCode > 0)
                    throw new Exception($"Engine exited with code {process.ExitCode}");

                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                return JsonSerializer.Deserialize<EngineResponse>(output);
            }
        }

        public EngineResponse GetMove(string board, int player, int threadNum = 12, int depth = 10)
        {
            EngineResponse engineResult = new EngineResponse();
            if (UseSettings == "url")
            {
                string urlPath = $"{EngineUrl}/move?s={board}&p={player}&t={threadNum}&d={depth}";
                var urlTask = GetResponse(urlPath);
                engineResult = urlTask.GetAwaiter().GetResult();
            }
            else if (UseSettings == "path")
            {
                throw new NotImplementedException();
            }

            return engineResult;
        }

        public async Task<EngineResponse> GetMoveAsync(string board, int player, int threadNum = 12, int depth = 10)
        {
            EngineResponse engineResult = new EngineResponse();
            if (UseSettings == "url")
            {
                string urlPath = $"{EngineUrl}/move?s={board}&p={player}&t={threadNum}&d={depth}";
                engineResult = await GetResponse(urlPath);
            }
            else if (UseSettings == "path")
            {
                engineResult = await EngineCommand(board, player, threadNum, depth);
            }

            return engineResult;
        }
    }
}