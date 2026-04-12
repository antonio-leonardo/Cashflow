using Cashflow.Shared.Storage.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cashflow.Shared.Storage.AzureBlob
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAzureBlobReportArtifactStore(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var section = configuration.GetSection("AzureBlob");
            var options = new AzureBlobStorageOptions
            {
                ConnectionString = section["ConnectionString"],
                AccountName = section["AccountName"],
                ContainerName = section["ContainerName"] ?? "reports",
                UseManagedIdentity = bool.TryParse(section["UseManagedIdentity"], out var useManagedIdentity)
                    && useManagedIdentity
            };

            services.AddSingleton(options);
            services.AddSingleton<IReportArtifactStore, AzureBlobReportArtifactStore>();
            return services;
        }
    }
}
