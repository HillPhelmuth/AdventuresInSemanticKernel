using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace AgentToolComponents.Models
{
    public static class ServiceCollectionExtentions
    {
        public static IServiceCollection AddAskUserService(this IServiceCollection services)
        {
            return services.AddScoped<AskUserService>().AddScoped<DownloadFileService>();
        }
    }
}
