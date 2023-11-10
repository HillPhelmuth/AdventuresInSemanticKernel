using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using SkPluginLibrary.Abstractions;
using SkPluginLibrary.Services;

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
            services.AddScoped<StorageService>();
            services.AddAzureClients(clientBuilder =>
            {
                clientBuilder.AddBlobServiceClient(configuration["AzureStorage:ConnectionString:blob"], preferMsi: true);
            });
            return services;
        }
    }
}
