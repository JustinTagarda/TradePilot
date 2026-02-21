using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TradePilot.Web.Services;
using TradePilot.Web;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["Api:BaseUrl"];
if (string.IsNullOrWhiteSpace(apiBaseUrl))
{
    apiBaseUrl = builder.HostEnvironment.BaseAddress;
}

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(apiBaseUrl, UriKind.Absolute)
});
builder.Services.AddScoped<IMtApiClient, MtApiClient>();

await builder.Build().RunAsync();
