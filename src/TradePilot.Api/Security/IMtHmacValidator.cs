namespace TradePilot.Api.Security;

public interface IMtHmacValidator
{
    Task<HmacValidationResult> ValidateAsync(HttpRequest request, CancellationToken cancellationToken);
}
