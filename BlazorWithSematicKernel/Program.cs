using Auth0.AspNetCore.Authentication;
using BlazorAceEditor.Extensions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.ApplicationInsights;
using SkPluginLibrary.Abstractions;
using SkPluginLibrary.Services;
using CrawlService = SkPluginLibrary.Services.CrawlService;
using WebApplication = Microsoft.AspNetCore.Builder.WebApplication;


var builder = WebApplication.CreateBuilder(args);
TestConfiguration.Initialize(builder.Configuration);
var services = builder.Services;
services.AddAuth0WebAppAuthentication(options =>
{
    options.Domain = builder.Configuration["Auth0:Domain"]!;
    options.ClientId = builder.Configuration["Auth0:ClientId"]!;
    
});
services.AddRazorPages();
services.AddServerSideBlazor();
builder.Services.Configure<HubOptions>(options =>
{
    options.MaximumReceiveMessageSize = null;
});
services.AddScoped<CrawlService>();
services.AddScoped<ICoreKernelExecution, CoreKernelService>();
services.AddScoped<IMemoryConnectors, CoreKernelService>();
services.AddScoped<ISemanticKernelSamples, CoreKernelService>();
services.AddScoped<ITokenization, CoreKernelService>();
services.AddScoped<ICustomNativePlugins, CoreKernelService>();
services.AddScoped<ICustomCombinations, CoreKernelService>();
services.AddScoped<IChatWithSk, CoreKernelService>();

services.AddHttpClient();
services.AddScoped<BingWebSearchService>();
services.AddBlazorAceEditor();
services.AddSingleton<ScriptService>();
services.AddScoped<CompilerService>();
services.AddScoped<TooltipService>();
services.AddChat();
services.AddRadzenComponents();

var stringEventWriter = new StringEventWriter();
services.AddScoped<StringEventWriter>();
var appInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"] ?? builder.Configuration["ApplicationInsights:ConnectionString"];
services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = appInsightsConnectionString;
    options.EnableDebugLogger = true;
});
builder.Logging.AddApplicationInsights(
    configureTelemetryConfiguration: (config) =>
        config.ConnectionString = appInsightsConnectionString,
    configureApplicationInsightsLoggerOptions: (options) => { }
);

builder.Logging.AddFilter<ApplicationInsightsLoggerProvider>("Default", LogLevel.Debug);
services.AddLogging(config =>
{
    config.AddProvider(new StringEventWriterLoggerProvider(stringEventWriter));
    config.AddApplicationInsights();
});
services.AddScoped<HdbscanService>();
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

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
