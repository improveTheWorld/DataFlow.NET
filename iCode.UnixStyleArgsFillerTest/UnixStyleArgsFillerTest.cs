using System;
using System.Collections.Generic;
using iCode.Framework;
using iCode.Extentions.ObjecExtentions.InvokePrivateMethod;

namespace iCode.Tests
{
    public class UnixStyleArgsFillerTest
    {
            
        UnixStyleArgsFiller UnixStyleArgsFiller_Setup_Test()
        {
            ArgDescription fileDest = new ArgDescription
            {
                Parameter = "FileWriterDestination",
                ShortName = "D",
                LongName = "Dest",
                IsRequired = false,
                DefaultValue = "12",
                HelpText = ""
            };

            ArgDescription maxRandom = new ArgDescription
            {
                Parameter = "MaxRandomInt",
                ShortName = "M",
                LongName = "max",
                IsRequired = false,
                DefaultValue = "12",
                HelpText = "The maximum integer to generate"
            };
            ArgDescription maxcount = new ArgDescription
            {
                Parameter = "RandomIntCount",
                ShortName = "c",
                LongName = "count",
                IsRequired = false,
                DefaultValue = "12",
                HelpText = " the number to multily random integers by"
            };

            ArgDescription mandatory = new ArgDescription
            {
                Parameter = "mandatory",
                ShortName = "m",
                LongName = "mand",
                IsRequired = true,
                DefaultValue = "",
                HelpText = " the number to multiply random integers by"
            };

            var conf = new List<ArgDescription> { fileDest, maxRandom, maxcount, mandatory };
            UnixStyleArgsFiller parser = new UnixStyleArgsFiller(conf);
            return parser;

        }

        void GenerateHelpMessage_Test()
        {
            UnixStyleArgsFiller parser = UnixStyleArgsFiller_Setup_Test();
            Console.WriteLine("**************** GenerateHelpMessage ******************");
            Console.WriteLine(parser.GenerateHelpMessage());

        }

        void GetMissedRequiredOption_Test()
        {
            UnixStyleArgsFiller parser = UnixStyleArgsFiller_Setup_Test();
            string[] argsOk = { "-D", "c:/test/text.txt", "-m" };
            string[] argsMandatoryMissed = { "-D", "c:/test/text.txt", "-c", "10" };
            Console.WriteLine("****************GetMissedRequiredOption : list should be empty********************");
            foreach (var missed in parser.UpdateParametersAndCheckMissedOptions(argsOk,out argsOk))
            {
                Console.WriteLine(missed);
            }
            Console.WriteLine($"Updated command parameter :{string.Join(" ", argsOk)}");

            Console.WriteLine("**************** GetMissedRequiredOption : Missed one argument : mandatory ********************");
            foreach (var missed in parser.UpdateParametersAndCheckMissedOptions(argsMandatoryMissed,out argsMandatoryMissed))
            {
                Console.WriteLine(missed);
            }
            Console.WriteLine($"Updated command parameter :{string.Join(" ", argsMandatoryMissed)}");
        }

        void GenerateErrorsMessage_Test()
        {
            UnixStyleArgsFiller parser = UnixStyleArgsFiller_Setup_Test();
            string[] argsMandatoryMissed = { "-D", "c:/test/text.txt", "-c 10" };
            string errorMessage = parser.GenerateErrorsMessage(parser.UpdateParametersAndCheckMissedOptions(argsMandatoryMissed,out argsMandatoryMissed));
            Console.WriteLine("**************** GenerateErrorsMessage: mandatory is required******************");
            Console.WriteLine(errorMessage);
        }

        void Parse_test()
        {
            UnixStyleArgsFiller parser = UnixStyleArgsFiller_Setup_Test();
            string[] argsOk = { "-D", "c:/test/text.txt", "-m" };
            string[] argsMandatoryMissed = { "-D", "c:/test/text.txt", "-c", "10" };
            Console.WriteLine("****************Parse : Ok**************** ");           ;
            
            Console.WriteLine(parser.iInvoke("_checkAndFill", argsOk, argsOk, Console.Out));
            Console.WriteLine("****************Parse : Error**************** ");

            Console.WriteLine(parser.iInvoke("_checkAndFill",argsMandatoryMissed, argsMandatoryMissed, Console.Out));
        }
    }
}
