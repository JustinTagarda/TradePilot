using TradePilot.Api.Security;
using TradePilot.Api.Snapshots;
using TradePilot.Shared.Models;
using TradePilot.Shared.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = TradePilotJson.Default.PropertyNamingPolicy;
    options.SerializerOptions.DictionaryKeyPolicy = TradePilotJson.Default.DictionaryKeyPolicy;
    options.SerializerOptions.DefaultIgnoreCondition = TradePilotJson.Default.DefaultIgnoreCondition;
    options.SerializerOptions.WriteIndented = TradePilotJson.Default.WriteIndented;
});
builder.Services.Configure<HmacValidationOptions>(builder.Configuration.GetSection(HmacValidationOptions.SectionName));
builder.Services.AddSingleton<ISourceSecretProvider, ConfigurationSourceSecretProvider>();
builder.Services.AddSingleton<INonceReplayGuard, MemoryNonceReplayGuard>();
builder.Services.AddSingleton<IMtHmacValidator, MtHmacValidator>();
builder.Services.AddSingleton<ISnapshotStore, InMemorySnapshotStore>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

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

mtGroup.MapPost("/snapshots", (HttpContext context, MtSnapshot snapshot, ISnapshotStore snapshotStore) =>
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

app.Run();
