using System;
using System.Collections.Generic;
using iCode.Framework;
using iCode.Extensions;

namespace iCode.Tests
{
    public class UnixStyleArgsFillerTest
    {
            
        IEnumerable<ArgRequirement> ArgREquirements_Setup()
        {
            yield return new ArgRequirement("FileWriterDestination", "D", "Dest", "12", "Path to the target folder");
            yield return new ArgRequirement("MaxRandomInt", "M", "max",  "12", "The maximum integer to generate");
            yield return new ArgRequirement("RandomIntCount", "c", "count", "12", " the number to multily random integers by");
            yield return new ArgRequirement("mandatory", "m", "mand", "", " the number to multiply random integers by", true);
        }

        void GenerateHelpMessage_Test()
        {
            var parser = ArgREquirements_Setup();
            Console.WriteLine("**************** GenerateHelpMessage ******************");
            // to do How to test a private method?
            //Console.WriteLine(parser.GenerateHelpMessage());

        }

        void GetMissedRequiredOption_Test()
        {
            var parser = ArgREquirements_Setup();
            string[] argsOk = { "-D", "c:/test/text.txt", "-m" };
            string[] argsMandatoryMissed = { "-D", "c:/test/text.txt", "-c", "10" };
            Console.WriteLine("****************GetMissedRequiredOption : list should be empty********************");
            List<string>? unixStyleArgs;
            parser.CheckAgains(argsOk,out unixStyleArgs)?.Display();
            
            Console.WriteLine($"Updated command parameter :{string.Join(" ", argsOk)}");

            Console.WriteLine("**************** GetMissedRequiredOption : Missed one argument : mandatory ********************");

            unixStyleArgs?.Clear();
            parser.CheckAgains(argsMandatoryMissed, out unixStyleArgs)?.Display();
            
            Console.WriteLine($"Updated command parameter :{string.Join(" ", argsMandatoryMissed)}");
        }

        void GenerateErrorsMessage_Test()
        {
            var parser = ArgREquirements_Setup();
            string[] argsMandatoryMissed = { "-D", "c:/test/text.txt", "-c 10" };

            //TOdo: check the private methods!
            //List<string> unixStyleArgs;
            //string errorMessage = parser.GenerateErrorsMessage(parser.CheckAgains(argsMandatoryMissed, unixStyleArgs));
            //Console.WriteLine("**************** GenerateErrorsMessage: mandatory is required******************");
            //Console.WriteLine(errorMessage);
        }

        void Parse_test()
        {
            var parser = ArgREquirements_Setup();
            string[] argsOk = { "-D", "c:/test/text.txt", "-m" };
            string[] argsMandatoryMissed = { "-D", "c:/test/text.txt", "-c", "10" };
            Console.WriteLine("****************Parse : Ok**************** ");           ;
            Console.WriteLine(parser.iInvoke("CheckAgains", argsOk));
            Console.WriteLine("****************Parse : Error**************** ");
            Console.WriteLine(parser.iInvoke("CheckAgains", argsMandatoryMissed));
        }
    }
}
