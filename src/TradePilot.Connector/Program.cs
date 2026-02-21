using Microsoft.Extensions.Options;
using TradePilot.Connector.Configuration;
using TradePilot.Connector.Forwarding;
using TradePilot.Connector.Security;
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
builder.Services.Configure<ConnectorOptions>(builder.Configuration.GetSection(ConnectorOptions.SectionName));
builder.Services.Configure<InboundHmacValidationOptions>(builder.Configuration.GetSection(InboundHmacValidationOptions.SectionName));
builder.Services.Configure<OutboundHmacOptions>(builder.Configuration.GetSection(OutboundHmacOptions.SectionName));
builder.Services.AddSingleton<IInboundSourceSecretProvider, ConfigurationInboundSourceSecretProvider>();
builder.Services.AddSingleton<IInboundNonceReplayGuard, MemoryInboundNonceReplayGuard>();
builder.Services.AddSingleton<IInboundHmacValidator, InboundHmacValidator>();
builder.Services.AddSingleton<IOutboundSourceSecretProvider, ConfigurationOutboundSourceSecretProvider>();
builder.Services.AddHttpClient(CloudSnapshotForwarder.HttpClientName, (serviceProvider, client) =>
{
    var connectorOptions = serviceProvider.GetRequiredService<IOptionsMonitor<ConnectorOptions>>().CurrentValue;
    if (Uri.TryCreate(connectorOptions.CloudApiBaseUrl, UriKind.Absolute, out var baseUri))
    {
        client.BaseAddress = baseUri;
    }
});
builder.Services.AddSingleton<ICloudSnapshotForwarder, CloudSnapshotForwarder>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseWhen(
    context => HttpMethods.IsPost(context.Request.Method)
        && context.Request.Path.Equals("/ingest/snapshot", StringComparison.OrdinalIgnoreCase),
    branch =>
    {
        branch.Use(async (context, next) =>
        {
            var validator = context.RequestServices.GetRequiredService<IInboundHmacValidator>();
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

            context.Items[ConnectorHttpContextItemKeys.AuthenticatedSourceId] = validationResult.SourceId!;
            await next();
        });
    });

app.MapGet("/health", (IOptions<ConnectorOptions> options) => Results.Ok(new
{
    status = "ok",
    service = "TradePilot.Connector",
    sourceId = options.Value.SourceId,
    cloudApiBaseUrl = options.Value.CloudApiBaseUrl,
    timestampUtc = DateTime.UtcNow
}))
.WithName("GetHealth")
.WithOpenApi();

app.MapPost("/ingest/snapshot", async (
    HttpContext context,
    MtSnapshot snapshot,
    ICloudSnapshotForwarder forwarder,
    CancellationToken cancellationToken) =>
{
    var authenticatedSourceId = context.Items[ConnectorHttpContextItemKeys.AuthenticatedSourceId] as string;

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

    var result = await forwarder.ForwardAsync(snapshot, cancellationToken);
    if (!result.Success)
    {
        return Results.Problem(
            title: "Snapshot forward failed",
            detail: result.Error,
            statusCode: result.StatusCode);
    }

    return Results.Accepted($"/ingest/snapshot/{snapshot.SourceId}");
})
.WithName("IngestSnapshot")
.WithOpenApi();

app.Run();
