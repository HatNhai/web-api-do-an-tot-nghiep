// "Một sản phẩm từ phòng sharepoint. SIMAX-NhàiVtt"

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Service.Application.Interfaces;
using Service.Application.Services;

namespace Service.Core.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddServicesDependencies(this IServiceCollection services, IConfiguration configuration)
        {
            AddService(services);
            return services;
        }
        private static void AddService(IServiceCollection services)
        {
            services.AddScoped<IPredictService, PredictService>();
        }
    }
}
