using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PmcAsync;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<Rate>
builder.Services.AddHttpClient<LocClient>(HttpDefaults);


using IHost host = builder.Build();

await host.RunAsync();



void HttpDefaults(HttpClient client)
{
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.DefaultRequestHeaders.Add("User-Agent", "Paul Mellon Centre Linked Art Client");
}