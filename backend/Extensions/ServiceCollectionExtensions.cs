using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
// using QB_SortifyDetectAI.Options;
using QB_SortifyDetectAI.Services;

namespace QB_SortifyDetectAI.Extensions
{
    public static class ServiceCollectionExtensions
    {
        // // 注册 TokenStore 为单例
        // public static IServiceCollection AddQuickBooksTokenStore(this IServiceCollection services, IConfiguration configuration)
        // {
        //     services.AddSingleton<QuickBooksTokenStore>(provider =>
        //     {
        //         var config = provider.GetRequiredService<IConfiguration>();
        //         var store = new QuickBooksTokenStore();
        //         store.Load(config);
        //         return store;
        //     });
        //     return services;
        // }

        // // 注册 QuickBooksService 相关服务
        // public static IServiceCollection AddQuickBooksServices(this IServiceCollection services, IConfiguration configuration)
        // {
        //     services.Configure<QuickBooksOptions>(configuration.GetSection("QuickBooks"));
        //     services.AddHttpClient<IQuickBooksService, QuickBooksService>();
        //     return services;
        // }

        // 可以继续添加其它分组服务注册
        public static IServiceCollection AddExpenseServices(this IServiceCollection services)
        {
            services.AddScoped<IExpenseClassificationService, ExpenseClassificationService>();
            services.AddScoped<IExpenseAnomalyDetectionService, ExpenseAnomalyDetectionService>();
            return services;
        }
    }

}
