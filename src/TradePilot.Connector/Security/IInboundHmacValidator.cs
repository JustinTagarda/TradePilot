namespace TradePilot.Connector.Security;

public interface IInboundHmacValidator
{
    Task<InboundHmacValidationResult> ValidateAsync(HttpRequest request, CancellationToken cancellationToken);
}
