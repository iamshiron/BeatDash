using DotNetEnv;
using Shiron.BeatDash.API;

Env.TraversePath().Load();
var app = BeatDashHost.Build(BeatDashHost.CreateBuilder(args));
await BeatDashHost.InitializeAsync(app);
app.Run();
