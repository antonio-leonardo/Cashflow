using Cashflow.Shared.Storage.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Cashflow.Shared.Storage.Local
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddLocalReportArtifactStore(
            this IServiceCollection services,
            Action<LocalStorageOptions>? configure = null)
        {
            var options = new LocalStorageOptions();
            configure?.Invoke(options);

            services.AddSingleton(options);
            services.AddSingleton<IReportArtifactStore, LocalReportArtifactStore>();
            return services;
        }
    }
}
