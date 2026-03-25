using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Shiron.BeatDash.API.Data;
using Shiron.BeatDash.API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddDbContext<BeatDashDbContext>(options =>
    options.UseSqlite("Data Source=beatdash.db"));

builder.Services.AddScoped<ISqliteService, SqliteService>();
builder.Services.AddSingleton<IEventStorageService, EventStorageService>();

builder.Services.AddSingleton<WebSocketClientService>();
builder.Services.AddHostedService<WebSocketClientService>(sp => sp.GetRequiredService<WebSocketClientService>());
builder.Services.AddHostedService<EventStorageServiceHostedService>();

var app = builder.Build();

if (app.Environment.IsDevelopment()) {
    app.MapOpenApi();
    app.MapScalarApiReference(options => {
        options.Title = "BeatDash API";
        options.Theme = ScalarTheme.Purple;
    });
}

app.UseHttpsRedirection();

using (var scope = app.Services.CreateScope()) {
    var sqliteService = scope.ServiceProvider.GetRequiredService<ISqliteService>();
    await sqliteService.InitializeAsync();
}

app.Run();
