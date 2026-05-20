using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Service.Infrastructure.Persistence;

namespace Service.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure( IServiceCollection services, IConfiguration configuration)
        { var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Connection string 'DefaultConnection' not found in appsettings.json");

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlServer(connectionString);
            });

           

            return services;
        }
    }
}