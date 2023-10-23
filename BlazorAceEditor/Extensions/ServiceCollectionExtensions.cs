using Microsoft.Extensions.DependencyInjection;

namespace BlazorAceEditor.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddBlazorAceEditor(this IServiceCollection services)
        {
            return services.AddScoped<AceEditorJsInterop>();
        }
    }
}
