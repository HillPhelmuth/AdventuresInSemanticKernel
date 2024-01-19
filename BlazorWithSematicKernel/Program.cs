using Auth0.AspNetCore.Authentication;
using BlazorAceEditor.Extensions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging.ApplicationInsights;
using SkPluginLibrary.Abstractions;
using SkPluginLibrary.Services;
using SkPluginLibrary.Models.Helpers;
using SkPluginComponents.Models;
using BlazorWithSematicKernel;

var builder = WebApplication.CreateBuilder(args);
IConfiguration configuration = builder.Configuration;
TestConfiguration.Initialize(configuration);
var services = builder.Services;
services.AddAuth0WebAppAuthentication(options =>
{
    options.Domain = configuration["Auth0:Domain"]!;
    options.ClientId = configuration["Auth0:ClientId"]!;
    
});
services.AddRazorPages();
//services.AddServerSideBlazor();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.Configure<HubOptions>(options =>
{
    options.MaximumReceiveMessageSize = null;
});
services.AddSkPluginLibraryServices(configuration);
services.AddHttpClient();

services.AddBlazorAceEditor();

services.AddChat();
services.AddRadzenComponents();
services.AddAskUserService();

var stringEventWriter = new StringEventWriter();
services.AddScoped<StringEventWriter>();
var appInsightsConnectionString = configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"] ?? configuration["ApplicationInsights:ConnectionString"];
services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = appInsightsConnectionString;
    options.EnableDebugLogger = true;
});


builder.Logging.AddFilter<ApplicationInsightsLoggerProvider>("Default", LogLevel.Debug);
services.AddLogging(config =>
{
    config.AddProvider(new StringEventWriterLoggerProvider(stringEventWriter));    
    config.Services.AddSingleton<ILoggerProvider, CustomApplicationInsightsLoggerProvider>();
    config.AddFilter<CustomApplicationInsightsLoggerProvider>("Default", LogLevel.Information);
});
services.AddCascadingAuthenticationState();
services.AddSingleton<ActivityLogging>();

var app = builder.Build();


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.Run();
