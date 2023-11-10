using Microsoft.Extensions.DependencyInjection;

namespace ChatComponents
{
    public static class ServiceCollectionExtentions
    {
        public static IServiceCollection AddChat(this IServiceCollection services)
        {
            return services.AddScoped<ChatStateCollection>().AddTransient<AppJsInterop>();
        }
    }
}
