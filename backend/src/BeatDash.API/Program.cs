using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Shiron.BeatDash.API.Data;
using Shiron.BeatDash.API.Endpoints;
using Shiron.BeatDash.API.Services;

Env.TraversePath().Load();
var builder = WebApplication.CreateBuilder(args);

var noGameMode = args.Contains("--no-game");

builder.Services.AddOpenApi();
builder.Services.AddRequestDecompression();

builder.Services.AddDbContext<BeatDashDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Default")
        ?? builder.Configuration.GetSection("BEATDASH_ConnectionStrings")["Default"]
        ?? throw new InvalidOperationException("Database connection string not configured")
    ));

builder.Services.AddHttpClient<DatabaseService>();
builder.Services.AddScoped<IDatabaseService, DatabaseService>();
builder.Services.AddScoped<IQueryService, QueryService>();

if (!noGameMode) {
    builder.Services.AddSingleton<IEventStorageService, EventStorageService>();
    builder.Services.AddSingleton<WebSocketClientService>();
    builder.Services.AddHostedService<WebSocketClientService>(sp => sp.GetRequiredService<WebSocketClientService>());
    builder.Services.AddHostedService<EventStorageServiceHostedService>();
}

var app = builder.Build();
using (var scope = app.Services.CreateAsyncScope()) {
    var context = scope.ServiceProvider.GetRequiredService<BeatDashDbContext>();
    await context.Database.MigrateAsync();
    context.SeedMockData();
}

app.MapOpenApi();
app.MapScalarApiReference(options => {
    options.Title = "BeatDash API";
    options.Theme = ScalarTheme.Purple;
});

app.UseRequestDecompression();
app.UseHttpsRedirection();

var api = app.MapGroup("/api");
api.MapGroup("/status").MapStatusApi();
api.MapGroup("/maps").MapMapsApi();
api.MapGroup("/sessions").MapPlaySessionsApi();
api.MapGroup("/livedata").MapLiveDataApi();
api.MapGroup("/analytics").MapAnalyticsApi();
app.MapGroup("/recordings").MapRecordingEndpoints();

using (var scope = app.Services.CreateScope()) {
    var databaseService = scope.ServiceProvider.GetRequiredService<IDatabaseService>();
    await databaseService.InitializeAsync();
}

app.Run();
