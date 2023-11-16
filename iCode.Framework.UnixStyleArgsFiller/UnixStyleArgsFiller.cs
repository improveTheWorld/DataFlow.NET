using iCode.Extentions.NewObjectsParsing;
using iCode.Extentions.StreamReaderExtentions;
using iCode.Extentions.IEnumerableExtentions;
using System.Reflection;
using System;
using System.Linq;

namespace iCode.Framework
{
       
    public class UnixStyleArgsFiller
    {
        //                          
        //                    ____________________
        // CSV conf_File     |CSVConfigFileLoader.|      List<CSV_ParameterConfig>
        //             ----->|        Load        |------
        //                   |____________________|      |
        //                                               |
        //                                               |   List<CSV_ParameterConfig> CommandLineParameterConfigs
        //                                         ______v_____________
        //                     string[] args      | CommandLineParser. |       1. bool Ok |Ok +help Message | Ko + error message| 
        //                                  ----->|     Parse          |------>2. args updated with "--paramter" instead of "-shortName" | "--longName"
        //                                        |____________________|
        //       


        public readonly IEnumerable<ArgDescription> CommandLineParameterConfigs;
        readonly string MessageHeader;
        public static bool CheckMendatoriesAndFillDefaultsFromCsv(string[] args, out string[] filledArgs,  string ArgDescriptionFile, int nbrLinesToSkip , string appDescription = "")
        {
           return new UnixStyleArgsFiller(ArgDescriptionFile, nbrLinesToSkip, appDescription)._checkAndFill(args, out filledArgs, Console.Out);
        }
        bool _checkAndFill(string[] CSVFormatArgs, out string[] UnixStyleArgs, TextWriter outPut)
        {
            if (CSVFormatArgs.ToString().Contains("--help"))
            {
                outPut.WriteLine(MessageHeader);
                outPut.WriteLine(GenerateHelpMessage());
                UnixStyleArgs = null;
                return false;
            }

            List<string> missedOptionList = UpdateParametersAndCheckMissedOptions(CSVFormatArgs, out UnixStyleArgs);

            if (missedOptionList != null && missedOptionList.Count > 0)
            {
                outPut.WriteLine(MessageHeader);
                outPut.WriteLine(GenerateErrorsMessage(missedOptionList));
                outPut.WriteLine(GenerateHelpMessage());
                return false;
            }
            else
            {
                return true;
            }
        }

        public UnixStyleArgsFiller(string ArgsDescriptionFile, int nbrLinesToSkip, string appDescription ="") 
            : this(new StreamReader(ArgsDescriptionFile), nbrLinesToSkip, appDescription) { }


        public UnixStyleArgsFiller(StreamReader ArgsFile, int nbrLinesToSkip, string appDescription ="")
        : this(ArgsFile.AsLines().Skip(nbrLinesToSkip).Select(CVS_line => CVS_line.AsObject<ArgDescription>(";")), appDescription){ }


        public UnixStyleArgsFiller(IEnumerable<ArgDescription> ArgsDescription, string appDescription="")
        {
            CommandLineParameterConfigs = ArgsDescription;
            var app = Assembly.GetExecutingAssembly().GetName();
            MessageHeader = $"\n{app.Name} \r\nCopyright(C)  {app.Version}\r\n\n";
        }

        int GetIndexInArgs(ArgDescription parameter, string[] args)
        {

            for (int index = 0; index < args.Length; index++)
            {
                string arg = args[index];
                if (arg == $"-{parameter.ShortName}" || arg == $"--{parameter.LongName}")
                {
                    return index;
                }

            }
            return -1;
        }


        // 1. Parse command arg
        // 2. check command args against Command line parameters Config and return True if ok,
        // 3. else Generate error message/ help message if args == --help/-h
        public List<string> UpdateParametersAndCheckMissedOptions(string[] args, out string[] newArgs)
        {
            List<string> newArgsList = new List<string>(args);
            List<string> missedList = new List<string>();
            foreach (var parameter in CommandLineParameterConfigs)
            {
                int index = GetIndexInArgs(parameter, args);

                if (index != -1) //if used in params or  not required 
                {
                    newArgsList[index] =$"--{parameter.Parameter}";
                }
                else if(!parameter.IsRequired)
                {
                    newArgsList.Add($"--{parameter.Parameter}");
                    newArgsList.Add(parameter.DefaultValue);
                }
                else // if Required but not n used params
                {
                    missedList.Add(parameter.Parameter);
                }
            }
            newArgs = newArgsList.ToArray();
            return missedList;
        }

        // 1. Generate help message ( for --help case ) 
        public string GenerateHelpMessage()
        {
            string output = "";
            foreach (var parameterConf in CommandLineParameterConfigs)
            {
                string line = $"\n\n  -{parameterConf.ShortName}, --{parameterConf.LongName}\t(Default: {parameterConf.DefaultValue}) {parameterConf.HelpText}";
                output += line;
            }
            output += $"\n\n  --help\tDisplay this help screen";
            return output;
        }

        // 1. Generate Error message when options are missed 
        public string GenerateErrorsMessage(List<string> missedOptionList)
        {
            string output = "ERROR(S):\r";
            foreach (var missedOption in missedOptionList)
            {
                string line = $"\n\n  required option '{missedOption}' is missing";
                output += line;
            }
            return output;
        }
    }
}
