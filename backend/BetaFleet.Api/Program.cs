using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using BetaFleet.Api.Data;
using BetaFleet.Api.Domain;
using BetaFleet.Api.Endpoints;
using BetaFleet.Api.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDb>(o => o.UseNpgsql(
    builder.Configuration.GetConnectionString("Postgres"),
    npgsql => npgsql.EnableRetryOnFailure(5)));
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.AddJwtAuth();
// Login is limited per client IP to slow down password guessing (on top of per-account lockout).
// 30/min leaves room for a whole office behind one NAT signing in at 08:00.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear(); o.KnownProxies.Clear(); // only reachable through the nginx container on the compose network
});
builder.Services.AddHttpClient("sap", c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.Configure<SapOptions>(builder.Configuration.GetSection("Sap"));
builder.Services.AddHostedService<SapBridgeWorker>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5181"])
    .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    await db.Database.MigrateAsync();
    if (app.Configuration.GetValue("Seed:SampleData", true)) await Seed.RunAsync(db);
    await UserSeed.RunAsync(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>(), app.Configuration, app.Logger);
}

app.UseForwardedHeaders();
app.UseDomainErrors();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapOpenApi();
app.MapScalarApiReference("/docs");
app.MapGet("/health", async (AppDb db) => await db.Database.CanConnectAsync() ? Results.Ok(new { status = "ok" }) : Results.StatusCode(503));

// Every /api route needs a signed-in user unless it opts out (only /api/auth/login does).
var api = app.MapGroup("/api").RequireAuthorization().AddEndpointFilter(AuthSetup.ScopeFilter);
api.MapAuth();
api.MapJobs();
api.MapClearing();
api.MapTransit();
api.MapFinance();
api.MapFleet();
api.MapPortal();

app.Run();
