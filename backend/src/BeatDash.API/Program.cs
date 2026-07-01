using DotNetEnv;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Shiron.BeatDash.API.Endpoints;
using Shiron.BeatDash.API.Seeders;
using Shiron.BeatDash.DB;
using Shiron.BeatDash.DB.Schema;

Env.TraversePath().Load();
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddIdentity<User, IdentityRole<Guid>>(c => {
    if (builder.Environment.IsDevelopment()) {
        c.Password.RequireDigit = false;
        c.Password.RequireLowercase = false;
        c.Password.RequireNonAlphanumeric = false;
        c.Password.RequireUppercase = false;
        c.Password.RequiredLength = 1;
    }

    c.User.RequireUniqueEmail = true;
}).AddEntityFrameworkStores<BeatDashDbContext>();

builder.Services.AddDbContext<BeatDashDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Database connection string not configured")
    ));

builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

var app = builder.Build();
using (var scope = app.Services.CreateScope()) {
    await scope.SeedAdminUser(
        app.Configuration.GetSection("AdminUser")["Email"] ?? throw new InvalidOperationException("Admin user email not configured"),
        app.Configuration.GetSection("AdminUser")["Password"] ?? throw new InvalidOperationException("Admin user password not configured"),
        app.Configuration.GetSection("AdminUser")["Role"] ?? throw new InvalidOperationException("Admin user role not configured")
    );
}

if (builder.Environment.IsDevelopment()) {
    app.MapOpenApi();
    app.MapScalarApiReference(o => {
        o.Title = "BeatDash API";
        o.Theme = ScalarTheme.Purple;
    });
}

app.UseHttpsRedirection();

var api = app.MapGroup("/api");
api.MapGet("/health", () => new { Message = "OK" }).WithTags("Health");
api.MapIdentityEndpoints();

app.Run();
