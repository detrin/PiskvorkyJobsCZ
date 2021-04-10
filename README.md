# PiskvorkyJobsCZ (bot: [hermanda](https://piskvorky.jobs.cz/detail-hrace/410b3182-afbd-4aff-8a43-2e92ea34ab97/))
This app represents a bot that can play gomoku on piskvorky.jobs.cz with other bots via provided web API. Game tokens have to be fetched and left to be matured till another player is assigned to the bot. Then game tokens are distributed via a queue in MongoDB database for distributing the workload. The logic of the gomoku engine is not provided for obvious reasons, but it uses alpha-beta pruning.

On the following diagram we can see how the system is supposed to be set up. Game token fetcher is executed by `dotnet run distributed_fetcher`. A worker is executed by `dotnet run distributed_worker`.
![Connection diagram](https://i.imgur.com/iRXxUDR.png) 
MongoDB database have to have three collections:
- GameTokens - for saving all created games in case of cleanup
- MaturingQueue - for saving game tokens of games where the partner wasn't assigned to our bot yet
- WaitingQueue - the game is ready and it is our move, game token will be taken by worker
 
App can run in local mode with `dotnet run local`, where game tokens are stored in Queue object, but the database is needed for backuping all fetched tokens in case of cleanup.
 
App also has a cleanup mode that can be started by `dotnet run cleanup`, where all fetched game tokens will be checked and if the winner is known, the entry will be deleted. If it is the move of our bot, the game will be finished.
