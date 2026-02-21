using Serilog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradePilot.Api.Persistence;
using TradePilot.Api.Security;
using TradePilot.Api.Snapshots;
using TradePilot.Shared.Models;
using TradePilot.Shared.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = TradePilotJson.Default.PropertyNamingPolicy;
    options.SerializerOptions.DictionaryKeyPolicy = TradePilotJson.Default.DictionaryKeyPolicy;
    options.SerializerOptions.DefaultIgnoreCondition = TradePilotJson.Default.DefaultIgnoreCondition;
    options.SerializerOptions.WriteIndented = TradePilotJson.Default.WriteIndented;
});
builder.Services.Configure<PersistenceOptions>(builder.Configuration.GetSection(PersistenceOptions.SectionName));
builder.Services.AddDbContextFactory<TradePilotDbContext>((serviceProvider, dbContextOptions) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<PersistenceOptions>>().Value;
    dbContextOptions.UseSqlite(options.ConnectionString);
});
builder.Services.Configure<HmacValidationOptions>(builder.Configuration.GetSection(HmacValidationOptions.SectionName));
builder.Services.AddSingleton<ISourceSecretProvider, ConfigurationSourceSecretProvider>();
builder.Services.AddSingleton<INonceReplayGuard, MemoryNonceReplayGuard>();
builder.Services.AddSingleton<IMtHmacValidator, MtHmacValidator>();
builder.Services.AddSingleton<ISnapshotStore, InMemorySnapshotStore>();
builder.Services.AddSingleton<ISnapshotHistoryStore, SqliteSnapshotHistoryStore>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseSwagger();
app.UseSwaggerUI();

app.UseWhen(
    context => HttpMethods.IsPost(context.Request.Method)
        && context.Request.Path.Equals("/v1/mt/snapshots", StringComparison.OrdinalIgnoreCase),
    branch =>
    {
        branch.Use(async (context, next) =>
        {
            var validator = context.RequestServices.GetRequiredService<IMtHmacValidator>();
            var validationResult = await validator.ValidateAsync(context.Request, context.RequestAborted);

            if (!validationResult.IsValid)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "invalid_hmac",
                    detail = validationResult.Error
                }, cancellationToken: context.RequestAborted);
                return;
            }

            context.Items[HmacHttpContextItemKeys.AuthenticatedSourceId] = validationResult.SourceId!;
            await next();
        });
    });

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "TradePilot.Api",
    timestampUtc = DateTime.UtcNow
}))
.WithName("GetHealth")
.WithOpenApi();

var mtGroup = app.MapGroup("/v1/mt");

mtGroup.MapPost("/snapshots", async (
    HttpContext context,
    MtSnapshot snapshot,
    ISnapshotStore snapshotStore,
    ISnapshotHistoryStore snapshotHistoryStore,
    CancellationToken cancellationToken) =>
{
    var authenticatedSourceId = context.Items[HmacHttpContextItemKeys.AuthenticatedSourceId] as string;

    if (string.IsNullOrWhiteSpace(snapshot.SourceId))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["sourceId"] = ["sourceId is required."]
        });
    }

    if (string.IsNullOrWhiteSpace(authenticatedSourceId)
        || !string.Equals(snapshot.SourceId, authenticatedSourceId, StringComparison.OrdinalIgnoreCase))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["sourceId"] = ["sourceId does not match authenticated source."]
        });
    }

    snapshotStore.Upsert(snapshot);
    await snapshotHistoryStore.PersistAsync(snapshot, cancellationToken);
    return Results.Accepted($"/v1/mt/sources/{snapshot.SourceId}/latest");
})
.WithName("IngestSnapshot")
.WithOpenApi();

mtGroup.MapGet("/sources", (ISnapshotStore snapshotStore) =>
{
    return Results.Ok(snapshotStore.GetSources());
})
.WithName("GetSources")
.WithOpenApi();

mtGroup.MapGet("/sources/{sourceId}/latest", (string sourceId, ISnapshotStore snapshotStore) =>
{
    if (!snapshotStore.TryGetLatest(sourceId, out var snapshot))
    {
        return Results.NotFound();
    }

    return Results.Ok(snapshot);
})
.WithName("GetLatestSnapshotBySource")
.WithOpenApi();

mtGroup.MapGet("/sources/{sourceId}/history", async (
    string sourceId,
    int? take,
    ISnapshotHistoryStore snapshotHistoryStore,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(sourceId))
    {
        return Results.BadRequest(new { error = "sourceId is required." });
    }

    var history = await snapshotHistoryStore.GetHistoryAsync(sourceId, take, cancellationToken);
    return Results.Ok(history);
})
.WithName("GetSnapshotHistoryBySource")
.WithOpenApi();

await EnsurePersistenceDatabaseCreatedAsync(app);
app.Run();

static async Task EnsurePersistenceDatabaseCreatedAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var options = scope.ServiceProvider.GetRequiredService<IOptions<PersistenceOptions>>().Value;
    if (!options.Enabled)
    {
        return;
    }

    await using var dbContext = await scope.ServiceProvider
        .GetRequiredService<IDbContextFactory<TradePilotDbContext>>()
        .CreateDbContextAsync();
    await dbContext.Database.EnsureCreatedAsync();
}

public partial class Program
{
}
