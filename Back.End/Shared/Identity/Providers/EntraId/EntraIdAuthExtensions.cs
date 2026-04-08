using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Web;

namespace Cashflow.Shared.Identity.EntraId
{
    public static class EntraIdAuthExtensions
    {
        public static IServiceCollection AddEntraIdAuthentication(
            this IServiceCollection services,
            IConfiguration configuration,
            bool requireHttpsMetadata = true)
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApi(configuration, configSectionName: "EntraId");

            if (!requireHttpsMetadata)
            {
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.RequireHttpsMetadata = false;
                });
            }

            return services;
        }
    }
}
