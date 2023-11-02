using iCode.Log;
using iLoggerUsageExamples.loggabaleObjects;
using iLoggerUsageExamples.nameSapceTargetedObjects;
using Microsoft.Extensions.Configuration;

namespace iLoggerUsageExamples
{
    namespace nameSapceTargetedObjects
    {
        class TargetedNumericLoggableObject
        {
            int numericValue;

            public void UpdateAndLogValue(int newValue)
            {
                numericValue = newValue;
                this.Info($"New value assigned: {numericValue}");
            }

            public bool IsOdd()
            {
                return numericValue % 2 == 1;
            }
        }


        class TargetedStringLoggableObject
        {
            string StringValue;

            public void UpdateAndLogValue(string newValue)
            {
                StringValue = newValue;
                this.Info($"New value assigned: {StringValue}");
            }



            public bool IsCapitalLetter()
            {
                if (string.IsNullOrEmpty(StringValue)) return false; // Retourne false si la chaîne est vide ou null

                return StringValue.All(c => !Char.IsLetter(c) || Char.IsUpper(c));
            }

        }
    }


    namespace loggabaleObjects
    {
        class NumericLoggableObject
        {
            int numericValue;

            public void UpdateAndLogValue(int newValue)
            {
                numericValue = newValue;
                this.Info($"New value assigned: {numericValue}");
            }

            public bool IsOdd()
            {
                return numericValue % 2 == 1;
            }
        }

        class StringLoggableObject
        {
            string  StringValue;

            public void UpdateAndLogValue(string newValue)
            {
                StringValue = newValue;
                this.Info($"New value assigned: {StringValue}");
            }

            public bool isCapitalLetter()
            {
                if (string.IsNullOrEmpty(StringValue)) return false; // Retourne false si la chaîne est vide ou null

                return StringValue.All(c => !Char.IsLetter(c) || Char.IsUpper(c));
            }
        }
    }

    internal class UsageExamplesProgram
    {
        static void Main(string[] args)
        {
 

            Config loggerConfiguration = iLogger.Filters;

            loggerConfiguration.IncludeTimestamp = true;
            loggerConfiguration.IncludeInstanceName = true;
            loggerConfiguration.IncludeTaskId = true;
            loggerConfiguration.IncludeThreadId = true;

            LogIntoKafkEvent();

            DisplayVariableChangeTracking();
            DisplayInstancesTargettingExamples();
            DisplayNamespacesTargettingExamples();

            
        }
        static void LogIntoKafkEvent()
        {

            
            Config loggerConfiguration = iLogger.Filters;
            loggerConfiguration.ResetFilters();
            iLogger.WriteLine("** LogIntoKafkEvent usage example,please create your azure hub event with kafka enabled ", LogLevel.Warn);
            iLogger.WriteLine("           Start kafka server and then Enter event hub namespace...", LogLevel.Warn);
            string? eventHubNamespace = Console.ReadLine();
            iLogger.WriteLine("           enter kafka topic / event hub name ...", LogLevel.Warn);
            string? topic = Console.ReadLine();
            iLogger.WriteLine("           enter connexion string ...", LogLevel.Warn);
            string? connexionstring = Console.ReadLine();
            iLogger.WriteLine("           enter log to put into kafka ...", LogLevel.Warn);
            string? log = Console.ReadLine();


            var builder = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            var settings = builder.Build();

            // default values
            if (String.IsNullOrEmpty(eventHubNamespace))
                eventHubNamespace = settings["kafka_eventHubNamespace"];
            if (string.IsNullOrEmpty(connexionstring))
                connexionstring = settings["kafka_connexionstring"];
            if (string.IsNullOrEmpty(topic))
                topic = settings["kafka_topic"];


            if (String.IsNullOrEmpty(log))
                log = "**Hello world !! Log Into Kafk Event Test ";

            iLogger.ResetLoggers();
            iLogger.AddKafkaEventHubLogger(eventHubNamespace, connexionstring, topic);

            iLogger.WriteLine(log); // here is the loggin operation to kafka

            iLogger.AddColoredConsoleWriter();
            iLogger.AddFileLogger(settings["fileLogger_Path"]);
            Console.WriteLine("Press any key to continue...", LogLevel.Warn);
            Console.ReadKey();
            Console.Clear();

        }
        static void DisplayVariableChangeTracking()
        {
            Config loggerConfiguration = iLogger.Filters;
            loggerConfiguration.ResetFilters();


            iLogger.WriteLine("** Track all variable changes.", LogLevel.Warn);

            Loggable<string> stringValue = "this one";
            stringValue = "First";
            stringValue += "Second";

            iLogger.WriteLine("Press any key to continue...");
            Console.ReadKey();
            Console.Clear();

        }

        static void DisplayNamespacesTargettingExamples()
        {


            // ************************ Namespace Targetting ******************************
            Config loggerConfiguration = iLogger.Filters;
            loggerConfiguration.ResetFilters();

            NumericLoggableObject firstNumericObject = new();
            StringLoggableObject firstStringObject = new();
            TargetedNumericLoggableObject targetedNumericLoggableObject = new();
            TargetedStringLoggableObject targetedStringLoggableObject = new();

            iLogger.WriteLine("** Target a specific namespace for logging.", LogLevel.Warn);


            loggerConfiguration.WatchedNameSpaces.Watch(new NameSpaceComparer("iLoggerUsageExamples.nameSapceTargetedObjects"));      
            firstNumericObject.WatchByLogger("NumericLoggableObject");
            firstNumericObject.UpdateAndLogValue(5);
            firstStringObject.WatchByLogger("StringLoggableObject");
            firstStringObject.UpdateAndLogValue("new value");
            targetedNumericLoggableObject.WatchByLogger("targetedNumericLoggableObject");
            targetedNumericLoggableObject.UpdateAndLogValue(6);
            targetedStringLoggableObject.WatchByLogger("targetedStringLoggableObject");
            targetedStringLoggableObject.UpdateAndLogValue("new value");


            iLogger.WriteLine("=> Multiple object were updated. Only those belonging to  iLoggerUsageExamples.nameSapceTargetedObjects namespace generates a log!", LogLevel.Warn);
  
            iLogger.WriteLine("Press any key to continue...");

            Console.ReadKey();
            Console.Clear();
        }
        static void DisplayInstancesTargettingExamples()
        {
            Config loggerConfiguration = iLogger.Filters;

            NumericLoggableObject firstNumericObject = new();
            NumericLoggableObject secondNumericObject = new();
            StringLoggableObject firstStringObject = new();
            StringLoggableObject secondStringObject = new();

            iLogger.WriteLine(secondStringObject.GetType().ToString());
            iLogger.WriteLine(firstNumericObject.GetType().ToString());

            // ************************ FIRST Example : Instance targeting ******************************
            iLogger.WriteLine("** First use case: Target a specific instance for logging.", LogLevel.Warn);

            firstNumericObject.WatchByLogger("FirstObject");
            firstNumericObject.UpdateAndLogValue(5);
            secondNumericObject.nameForLog("SecondObject");
            secondNumericObject.UpdateAndLogValue(6);

            iLogger.WriteLine("=> Only the watched instance, firstObject, generates a log!", LogLevel.Warn);
            iLogger.WriteLine("=> Observe the ability to give instances different names for logging.", LogLevel.Warn);
            iLogger.WriteLine("Press any key to continue...");

            Console.ReadKey();
            Console.Clear();

            // ************************ SECOND Example : All instances targeting ******************************
            iLogger.WriteLine("** Second use case: Target all possible instances without prior declaration.", LogLevel.Warn);

            loggerConfiguration.WatchedInstances.WatchAll();
            firstNumericObject.UpdateAndLogValue(5);
            secondNumericObject.UpdateAndLogValue(6);

            iLogger.WriteLine("=> Both firstObject and secondObject generate logs!", LogLevel.Warn);
            iLogger.WriteLine("Press any key to continue...", LogLevel.Warn);

            Console.ReadKey();
            Console.Clear();

            // ************************ Third Example : target based on instance characteristics  ******************************
            iLogger.WriteLine("** Third use case: Log only when numericValue is odd using RequesterValidation.", LogLevel.Warn);

            loggerConfiguration.RequesterAcceptanceCriterias.SetCriteria((x) =>
            {
                if (x is NumericLoggableObject loggableObject)
                {
                    return loggableObject.IsOdd();
                }
                return false;
            });

            firstNumericObject.UpdateAndLogValue(5);
            secondNumericObject.UpdateAndLogValue(6);

            iLogger.WriteLine("=> Both firstObject and secondObject were updated, but only firstObject generated a log as it has an odd value!", LogLevel.Warn);
            iLogger.WriteLine("Press any key to continue...", LogLevel.Warn);

            Console.ReadKey();
            Console.Clear();

            // ************************ Fourth Example : target based on instance type ******************************
            iLogger.WriteLine("** Fourth use case: Log based on instance type.", LogLevel.Warn);

            loggerConfiguration.RequesterAcceptanceCriterias.SetCriteria((x) =>
            {
                return x is NumericLoggableObject;
            });

            firstNumericObject.UpdateAndLogValue(5);
            firstStringObject.UpdateAndLogValue("new String Value");
            secondNumericObject.UpdateAndLogValue(6);


            iLogger.WriteLine("=> Both Numeric and String objects were updated but logs were generated just by Numeric ones!", LogLevel.Warn);
            iLogger.WriteLine("Press any key to continue...", LogLevel.Warn);

            Console.ReadKey();
            Console.Clear();
            // ************************ Fiveth Example : Multiple acceptance criterias  ******************************
            iLogger.WriteLine("** Fiveth use case: Add new acceptance criterias : Log String objects that are In Capital letters", LogLevel.Warn);

            loggerConfiguration.RequesterAcceptanceCriterias.AddCriteria((x) =>
            {

                if (x is StringLoggableObject loggableObject)
                {
                    return loggableObject.isCapitalLetter();
                }
                return false;
            });

            firstNumericObject.UpdateAndLogValue(5);
            secondNumericObject.UpdateAndLogValue(6);
            firstStringObject.UpdateAndLogValue("new String Value");
            secondStringObject.UpdateAndLogValue("HELLO WORLD!");

            iLogger.WriteLine("=> Both firstObject and secondObject values were updated, but logs were generated based on type!", LogLevel.Warn);
            iLogger.WriteLine("Press any key to continue...", LogLevel.Warn);

            Console.ReadKey();
            Console.Clear();

        }
    }
}
