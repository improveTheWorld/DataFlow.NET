using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace iCode.Framework.UnixParamsStyle
{
    public static class UnixParamsStyle
    {
        public static CONFIG? GetConfig<CONFIG>(string[] args, string ArgsDescriptionFile, out ServiceProvider? serviceProvider) 
        {
            if (!UnixStyleArgsFiller.CheckMendatoriesAndFillDefaultsFromCsv(args, out args, ArgsDescriptionFile, true))
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
