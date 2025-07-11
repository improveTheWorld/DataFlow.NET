﻿using DataFlow.Log;
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
            iLogger.Out("** LogIntoKafkEvent usage example,please create your azure hub event with kafka enabled ", LogLevel.Warn);
            iLogger.Out("           Start kafka server and then Enter event hub namespace...", LogLevel.Warn);
            string? eventHubNamespace = Console.ReadLine();
            iLogger.Out("           enter kafka topic / event hub name ...", LogLevel.Warn);
            string? topic = Console.ReadLine();
            iLogger.Out("           enter connexion string ...", LogLevel.Warn);
            string? connexionstring = Console.ReadLine();
            iLogger.Out("           enter log to put into kafka ...", LogLevel.Warn);
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

            iLogger.Out(log); // here is the loggin operation to kafka

            iLogger.getColoredConsoleWriter();
            iLogger.AddFileLogger(settings["fileLogger_Path"]);
            Console.WriteLine("Press any key to continue...", LogLevel.Warn);
            Console.ReadKey();
            Console.Clear();

        }
        static void DisplayVariableChangeTracking()
        {
            Config loggerConfiguration = iLogger.Filters;
            loggerConfiguration.ResetFilters();


            iLogger.Out("** Track all variable changes.", LogLevel.Warn);

            Loggable<string> stringValue = "this one";
            stringValue = "First";
            stringValue += "Second";

            iLogger.Out("Press any key to continue...");
            Console.ReadKey();
            Console.Clear();

        }

        static void DisplayStatusLogExamples()
        {
            //TODO : imagine having a imput that displays some variables values : tables/ strings / object .. with a semi graphical display :
            //                                           ___ ___ ___ ___    
            //exemple a tables can be displayed as this | 5 | 3 | 1 | 3 |
            //                                          |___|___|___|___| 
            // a class like this : _____________
            //                    |name :"Bilel"|
            //                    |-------------|
            //                    |Age  : 43    |
            //                    |-------------|
            //                    |size: 1.80   |
            //                    |_____________|
            //
            // and that when a value is updated, the dispay is updated ( using the x,y cursor moving caracteristic )
            // This will be a step further to the displying differential execution in a GUI environnement in the future ( with a possibility of zoom in , zoom out , forward playing , back playing)
            // reflexion direction : we need to extend the object with a LogStatus function, and then to monitor variables updates 
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

            iLogger.Out("** Target a specific namespace for logging.", LogLevel.Warn);


            loggerConfiguration.WatchedNameSpaces.Watch(new NameSpaceComparer("iLoggerUsageExamples.nameSapceTargetedObjects"));      
            firstNumericObject.WatchByLogger("NumericLoggableObject");
            firstNumericObject.UpdateAndLogValue(5);
            firstStringObject.WatchByLogger("StringLoggableObject");
            firstStringObject.UpdateAndLogValue("new value");
            targetedNumericLoggableObject.WatchByLogger("targetedNumericLoggableObject");
            targetedNumericLoggableObject.UpdateAndLogValue(6);
            targetedStringLoggableObject.WatchByLogger("targetedStringLoggableObject");
            targetedStringLoggableObject.UpdateAndLogValue("new value");


            iLogger.Out("=> Multiple object were updated. Only those belonging to  iLoggerUsageExamples.nameSapceTargetedObjects namespace generates a log!", LogLevel.Warn);
  
            iLogger.Out("Press any key to continue...");

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

            iLogger.Out(secondStringObject.GetType().ToString());
            iLogger.Out(firstNumericObject.GetType().ToString());

            // ************************ FIRST Example : Instance targeting ******************************
            iLogger.Out("** First use case: Target a specific instance for logging.", LogLevel.Warn);

            firstNumericObject.WatchByLogger("FirstObject");
            firstNumericObject.UpdateAndLogValue(5);
            secondNumericObject.nameForLog("SecondObject");
            secondNumericObject.UpdateAndLogValue(6);

            iLogger.Out("=> Only the watched instance, firstObject, generates a log!", LogLevel.Warn);
            iLogger.Out("=> Observe the ability to give instances different names for logging.", LogLevel.Warn);
            iLogger.Out("Press any key to continue...");

            Console.ReadKey();
            Console.Clear();

            // ************************ SECOND Example : All instances targeting ******************************
            iLogger.Out("** Second use case: Target all possible instances without prior declaration.", LogLevel.Warn);

            loggerConfiguration.WatchedInstances.WatchAll();
            firstNumericObject.UpdateAndLogValue(5);
            secondNumericObject.UpdateAndLogValue(6);

            iLogger.Out("=> Both firstObject and secondObject generate logs!", LogLevel.Warn);
            iLogger.Out("Press any key to continue...", LogLevel.Warn);

            Console.ReadKey();
            Console.Clear();

            // ************************ Third Example : target based on instance characteristics  ******************************
            iLogger.Out("** Third use case: Log only when numericValue is odd using RequesterValidation.", LogLevel.Warn);

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

            iLogger.Out("=> Both firstObject and secondObject were updated, but only firstObject generated a log as it has an odd value!", LogLevel.Warn);
            iLogger.Out("Press any key to continue...", LogLevel.Warn);

            Console.ReadKey();
            Console.Clear();

            // ************************ Fourth Example : target based on instance type ******************************
            iLogger.Out("** Fourth use case: Log based on instance type.", LogLevel.Warn);

            loggerConfiguration.RequesterAcceptanceCriterias.SetCriteria((x) =>
            {
                return x is NumericLoggableObject;
            });

            firstNumericObject.UpdateAndLogValue(5);
            firstStringObject.UpdateAndLogValue("new String Value");
            secondNumericObject.UpdateAndLogValue(6);


            iLogger.Out("=> Both Numeric and String objects were updated but logs were generated just by Numeric ones!", LogLevel.Warn);
            iLogger.Out("Press any key to continue...", LogLevel.Warn);

            Console.ReadKey();
            Console.Clear();
            // ************************ Fiveth Example : Multiple acceptance criterias  ******************************
            iLogger.Out("** Fiveth use case: Add new acceptance criterias : Log String objects that are In Capital letters", LogLevel.Warn);

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

            iLogger.Out("=> Both firstObject and secondObject values were updated, but logs were generated based on type!", LogLevel.Warn);
            iLogger.Out("Press any key to continue...", LogLevel.Warn);

            Console.ReadKey();
            Console.Clear();

        }
    }
}
