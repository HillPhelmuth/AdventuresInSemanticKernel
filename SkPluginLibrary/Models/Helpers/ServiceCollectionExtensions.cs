using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using SkPluginLibrary.Abstractions;
using SkPluginLibrary.Services;
using Microsoft.SemanticKernel;
using SkPluginLibrary.Agents;
using SkPluginLibrary.Agents.Examples;

namespace SkPluginLibrary.Models.Helpers
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSkPluginLibraryServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<ICoreKernelExecution, CoreKernelService>();
            services.AddScoped<IMemoryConnectors, CoreKernelService>();
            services.AddScoped<ISemanticKernelSamples, CoreKernelService>();
            services.AddScoped<ITokenization, CoreKernelService>();
            services.AddScoped<ICustomNativePlugins, CoreKernelService>();
            services.AddScoped<ICustomCombinations, CoreKernelService>();
            services.AddScoped<IChatWithSk, CoreKernelService>();
            services.AddScoped<CrawlService>();
            services.AddScoped<BingWebSearchService>();
            services.AddSingleton<ScriptService>();
            services.AddScoped<CompilerService>();
            services.AddScoped<HdbscanService>();
            services.AddTransient<AdventureStoryAgents>();
            services.AddTransient<AssistantAgentService>();
            return services;
        }
    }
}
