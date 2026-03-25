using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Shiron.BeatDash.API.Data;
using Shiron.BeatDash.API.Endpoints;
using Shiron.BeatDash.API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddDbContext<BeatDashDbContext>(options =>
    options.UseSqlite("Data Source=beatdash.db"));

builder.Services.AddScoped<ISqliteService, SqliteService>();
builder.Services.AddScoped<IQueryService, QueryService>();
builder.Services.AddSingleton<IEventStorageService, EventStorageService>();

builder.Services.AddSingleton<WebSocketClientService>();
builder.Services.AddHostedService<WebSocketClientService>(sp => sp.GetRequiredService<WebSocketClientService>());
builder.Services.AddHostedService<EventStorageServiceHostedService>();

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference(options => {
    options.Title = "BeatDash API";
    options.Theme = ScalarTheme.Purple;
});

app.UseHttpsRedirection();

var api = app.MapGroup("/api");

api.MapGroup("/status").MapStatusApi();
api.MapGroup("/maps").MapMapsApi();
api.MapGroup("/sessions").MapPlaySessionsApi();
api.MapGroup("/livedata").MapLiveDataApi();
api.MapGroup("/analytics").MapAnalyticsApi();

using (var scope = app.Services.CreateScope()) {
    var sqliteService = scope.ServiceProvider.GetRequiredService<ISqliteService>();
    await sqliteService.InitializeAsync();
}

app.Run();
