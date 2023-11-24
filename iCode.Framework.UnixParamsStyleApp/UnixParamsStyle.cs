using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace iCode.Framework
{
    public static class UnixParamsStyle
    {
        public static CONFIG? GetConfig<CONFIG>(string[] args, string CSV_ArgsFile, out ServiceProvider? serviceProvider) 
        {
            if (!UnixStyleArgsFiller.CheckMendatoriesAndFillDefaultsFromCsv(args, out args, CSV_ArgsFile, 1))
            {
                serviceProvider = null;
                return default;
            }
            else
            {
                IConfiguration configuration = new ConfigurationBuilder()
                    .AddCommandLine(args)
                    .Build();
                CONFIG config = configuration.Get<CONFIG>();
                IServiceCollection collection = new ServiceCollection();
                collection.AddSingleton(typeof(CONFIG),config);
                serviceProvider = collection.BuildServiceProvider();
                return config;
            }
        }
    }       
}
