using Xunit;
using DataFlow.Log;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace DataFlow.Logger.Tests
{
    public class LoggerTests : IDisposable
    {
        public LoggerTests()
        {
            // Reset logger state before each test
            iLogger.Filters.ResetFilters();
            iLogger.ResetLoggers();
            iLogger.BufferEnabled = false; // Start with sync mode for predictable tests
        }

        public void Dispose()
        {
            iLogger.ResetLoggers();
        }

        [Fact]
        public void BasicLogging_WritesToTarget()
        {
            // Arrange
            var mockTarget = new MockLoggerTarget();
            iLogger.AddLogger(mockTarget);

            // Act
            iLogger.Info("Test Message");

            // Assert
            Assert.Contains(mockTarget.Logs, log => log.Contains("Test Message"));
        }

        [Fact]
        public void Filtering_RespectsLogLevel()
        {
            // Arrange
            var mockTarget = new MockLoggerTarget();
            iLogger.AddLogger(mockTarget);
            iLogger.MaxAuthorizedLogLevel = LogLevel.Info;

            // Act
            iLogger.Info("Info Message");
            iLogger.Debug("Debug Message"); // Should be filtered out

            // Assert
            Assert.Contains(mockTarget.Logs, log => log.Contains("Info Message"));
            Assert.DoesNotContain(mockTarget.Logs, log => log.Contains("Debug Message"));
        }

        [Fact]
        public async Task Loop_Crashes_OnException()
        {
            // This test is designed to FAIL (crash the process or hang) before the fix.
            // We expect the unhandled exception in the async void Loop to be problematic.

            // Arrange
            iLogger.BufferEnabled = true; // Enable background loop
            var crashingTarget = new CrashingLoggerTarget();
            iLogger.AddLogger(crashingTarget);

            // Act
            iLogger.Info("This will crash the logger loop");

            // Wait a bit for the loop to process
            await Task.Delay(500);

            // Assert
            // If the loop crashed, subsequent logs won't be processed.
            var safeTarget = new MockLoggerTarget();
            iLogger.AddLogger(safeTarget);

            iLogger.Info("Post-crash message");
            await Task.Delay(500);

            // If the fix is working, this should pass. If not, it will likely fail 
            // because the loop is dead.
            Assert.Contains(safeTarget.Logs, log => log.Contains("Post-crash message"));
        }
    }

    public class MockLoggerTarget : ILoggerTarget
    {
        public List<string> Logs { get; } = new List<string>();

        public void Log(string message, LogLevel logging)
        {
            Logs.Add(message);
        }
    }

    public class CrashingLoggerTarget : ILoggerTarget
    {
        public void Log(string message, LogLevel logging)
        {
            throw new Exception("Intentional Crash!");
        }
    }
}
