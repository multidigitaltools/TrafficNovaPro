using Microsoft.Extensions.DependencyInjection;
using TrafficNova.Core.Interfaces;
using TrafficNova.Core.Services;

namespace TrafficNova.Core;

public static class ServiceRegistration
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        // IStatsService, ICampaignService, IProxyService registered in App.xaml.cs with real DB-backed services
        return services;
    }
}
