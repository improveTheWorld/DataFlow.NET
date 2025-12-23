using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DataFlow.Extensions;

namespace DataFlow.Framework
{

    public static class UnixStyleArgs
    {
        public static IEnumerable<string>? CheckAgains(this IEnumerable<ArgRequirement> requirements, string[] inputArgs, out List<string>? unixStyleArgs)
        {
            IEnumerable<string> missedArgs;

            if (inputArgs.Contains("--help") || inputArgs.Contains("-h"))
            {
                unixStyleArgs = null;

                return requirements.GenerateHelpMessage().Prepend(appNameAndVersionr());
            }
            else if (!(missedArgs = requirements.Parse(inputArgs, out unixStyleArgs)).IsNullOrEmpty())
            {
                return GenerateErrorsMessage(missedArgs).Prepend(appNameAndVersionr()).Union(requirements.GenerateHelpMessage()); ;
            }
            else
            {
                // Success case: unixStyleArgs is already populated by Parse
                return null;
            }
        }

        public static ServiceProvider ConfigureApp<TConfig>(string[] unixStyleArgs)
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddCommandLine(unixStyleArgs.ToArray())
                .Build();
            IServiceCollection collection = new ServiceCollection();
            collection.AddSingleton(typeof(TConfig), configuration.Get<TConfig>()!);
            return collection.BuildServiceProvider();
        }



        // 1. Parse command arg
        // 2. check command args against Command line parameters Config and return True if ok,
        // 3. else Generate error message/ help message if args == --help/-h        
        // 1. Generate help message ( for --help case ) 
        static IEnumerable<string> Parse(this IEnumerable<ArgRequirement> requirements, string[] inputArgs, out List<string> completedArgs)
        {
            completedArgs = new(inputArgs);
            List<string> missedArgs = new();


            foreach (var arg in requirements)
            {
                int index = Array.FindIndex(inputArgs, x => x == $"-{arg.ShortName}" || x == $"--{arg.LongName}");
                if (index != -1)
                {
                    if (arg.IsFlag)
                    {
                        completedArgs[index] = $"--{arg.ArgName}=true";
                    }
                    else
                    {
                        completedArgs[index] = $"--{arg.ArgName}";  // Modify the element directly in the array
                    }
                }
                else if (arg.IsMandatory) // means mandatory and not in input args !
                {
                    missedArgs.Add(arg.ArgName);
                }
                else
                {
                    if (arg.IsFlag)
                    {
                        completedArgs.Add($"--{arg.ArgName}=false");
                    }
                    else
                    {
                        completedArgs.Add($"--{arg.ArgName}");
                        completedArgs.Add(arg.DefaultValue);
                    }
                }

            }
            return missedArgs;
        }

        static IEnumerable<string> GenerateHelpMessage(this IEnumerable<ArgRequirement> requirements)
        {
            return requirements.Select(x => $"  -{x.ShortName}, --{x.LongName}\t(Default: {x.DefaultValue}) {x.Description}")
                                    .Append("  --help\tDisplay this help screen");
        }

        static string appNameAndVersionr()
        {
            var app = Assembly.GetExecutingAssembly().GetName();
            return $"\n{app.Name} \r\nCopyright(C)  {app.Version}\r\n\n";
        }

        // 1. Generate Error message when options are missed 
        static IEnumerable<string> GenerateErrorsMessage(IEnumerable<string> missedOptions)
        {
            return missedOptions.Select(x => $"  required option '{x}' is missing")
                                       .Prepend("ERROR(S):");
        }
    }

}
