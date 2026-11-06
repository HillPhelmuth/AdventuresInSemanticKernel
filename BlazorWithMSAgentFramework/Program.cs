using Auth0.AspNetCore.Authentication;
using AgentFrameworkLibrary;
using AgentToolComponents.Models;
using BlazorAceEditor.Extensions;
using BlazorWithMSAgentFramework;
using ChatComponents;
using Microsoft.AspNetCore.SignalR;
using Radzen;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAuth0WebAppAuthentication(options =>
{
    options.Domain = builder.Configuration["Auth0:Domain"]!;
    options.ClientId = builder.Configuration["Auth0:ClientId"]!;
});
builder.Services.AddRazorPages();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.Configure<HubOptions>(options => options.MaximumReceiveMessageSize = 2_000_000);
builder.Services.AddHttpClient("public-web", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("AdventuresInMSAgentFramework/1.0");
}).ConfigurePrimaryHttpMessageHandler(ApiTools.CreatePublicHandler);
builder.Services.AddAgentFrameworkServices();
builder.Services.AddChat();
builder.Services.AddRadzenComponents();
builder.Services.AddBlazorAceEditor();
builder.Services.AddAskUserService();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddApplicationInsightsTelemetry();
var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
