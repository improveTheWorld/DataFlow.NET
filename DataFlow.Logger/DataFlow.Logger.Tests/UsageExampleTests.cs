using Xunit;
using DataFlow.Log;
using DataFlow.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DataFlow.Logger.Tests
{
    public class UsageExampleTests : IDisposable
    {
        private readonly MockLoggerTarget _mockTarget;

        public UsageExampleTests()
        {
            iLogger.Filters.ResetFilters();
            iLogger.ResetLoggers();
            iLogger.BufferEnabled = false; // Sync mode for tests
            iLogger.MaxAuthorizedLogLevel = LogLevel.Trace; // Ensure logs are accepted
            _mockTarget = new MockLoggerTarget();
            iLogger.AddLogger(_mockTarget);
        }

        public void Dispose()
        {
            iLogger.ResetLoggers();
        }

        [Fact]
        public void VariableChangeTracking_LogsChanges()
        {
            // Arrange
            iLogger.Filters.WatchedInstances.WatchAll(); // Watch everything for this test

            // Act
            Loggable<string> stringValue = "Initial"; // Logs assignment
            stringValue = "First"; // Logs assignment
            stringValue += "Second"; // Logs + operator

            // Assert
            // Note: The exact messages depend on the Loggable implementation strings
            Assert.Contains(_mockTarget.Logs, l => l.Contains("Value Changes due to assignment/ implicit conversion: Initial"));
            Assert.Contains(_mockTarget.Logs, l => l.Contains("Value Changes due to assignment/ implicit conversion: First"));
            Assert.Contains(_mockTarget.Logs, l => l.Contains("Value Changes due to '+' operator: FirstSecond"));
        }

        // Helper classes for targeting tests
        class NumericLoggableObject
        {
            int numericValue;
            public void UpdateAndLogValue(int newValue)
            {
                numericValue = newValue;
                this.Info($"New value assigned: {numericValue}");
            }
            public bool IsOdd() => numericValue % 2 == 1;
        }

        class StringLoggableObject
        {
            string StringValue;
            public void UpdateAndLogValue(string newValue)
            {
                StringValue = newValue;
                this.Info($"New value assigned: {StringValue}");
            }
            public bool AreLetterUpper() => !string.IsNullOrEmpty(StringValue) && StringValue.All(c => !char.IsLetter(c) || char.IsUpper(c));
        }

        [Fact]
        public void InstanceTargeting_LogsOnlyWatchedInstances()
        {
            // Arrange
            var firstNumeric = new NumericLoggableObject();
            var secondNumeric = new NumericLoggableObject();

            // Act
            firstNumeric.WatchByLogger("FirstObject");
            firstNumeric.UpdateAndLogValue(5);

            secondNumeric.NameForLog("SecondObject"); // Named but NOT watched
            secondNumeric.UpdateAndLogValue(6);

            // Assert
            Assert.Contains(_mockTarget.Logs, l => l.Contains("New value assigned: 5"));
            Assert.DoesNotContain(_mockTarget.Logs, l => l.Contains("New value assigned: 6"));
        }

        [Fact]
        public void WatchAll_LogsAllInstances()
        {
            // Arrange
            var firstNumeric = new NumericLoggableObject();
            var secondNumeric = new NumericLoggableObject();
            iLogger.Filters.WatchedInstances.WatchAll();

            // Act
            firstNumeric.UpdateAndLogValue(5);
            secondNumeric.UpdateAndLogValue(6);

            // Assert
            Assert.Contains(_mockTarget.Logs, l => l.Contains("New value assigned: 5"));
            Assert.Contains(_mockTarget.Logs, l => l.Contains("New value assigned: 6"));
        }

        [Fact]
        public void RequesterValidation_LogsBasedOnCriteria()
        {
            // Arrange
            var firstNumeric = new NumericLoggableObject(); // Will update to 5 (Odd)
            var secondNumeric = new NumericLoggableObject(); // Will update to 6 (Even)
            iLogger.Filters.WatchedInstances.WatchAll();

            iLogger.Filters.RequesterAcceptanceCriterias.SetCriteria(x =>
            {
                if (x is NumericLoggableObject num) return num.IsOdd();
                return false;
            });

            // Act
            firstNumeric.UpdateAndLogValue(5);
            secondNumeric.UpdateAndLogValue(6);

            // Assert
            Assert.Contains(_mockTarget.Logs, l => l.Contains("New value assigned: 5"));
            Assert.DoesNotContain(_mockTarget.Logs, l => l.Contains("New value assigned: 6"));
        }
    }
}
