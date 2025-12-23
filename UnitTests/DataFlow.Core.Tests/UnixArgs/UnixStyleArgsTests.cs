using DataFlow.Framework;
using Xunit;

namespace DataFlow.Core.Tests.UnixArgs
{
    public class UnixStyleArgsTests
    {
        [Fact]
        public void Parse_ShouldHandleBooleanFlags_WhenPresent()
        {
            // Arrange
            var requirements = new List<ArgRequirement>
            {
                new ArgRequirement("Verbose", "v", "verbose", "false", "Enable verbose logging", isFlag: true)
            };
            string[] args = new[] { "-v" };

            // Act
            requirements.CheckAgains(args, out List<string>? parsedArgs);

            // Assert
            Assert.NotNull(parsedArgs);
            Assert.Contains("--Verbose=true", parsedArgs);
        }

        [Fact]
        public void Parse_ShouldHandleBooleanFlags_WhenAbsent()
        {
            // Arrange
            var requirements = new List<ArgRequirement>
            {
                new ArgRequirement("Verbose", "v", "verbose", "false", "Enable verbose logging", isFlag: true)
            };
            string[] args = Array.Empty<string>();

            // Act
            requirements.CheckAgains(args, out List<string>? parsedArgs);

            // Assert
            Assert.NotNull(parsedArgs);
            Assert.Contains("--Verbose=false", parsedArgs);
        }

        [Fact]
        public void Parse_ShouldHandleBooleanFlags_LongName()
        {
            // Arrange
            var requirements = new List<ArgRequirement>
            {
                new ArgRequirement("Verbose", "v", "verbose", "false", "Enable verbose logging", isFlag: true)
            };
            string[] args = new[] { "--verbose" };

            // Act
            requirements.CheckAgains(args, out List<string>? parsedArgs);

            // Assert
            Assert.NotNull(parsedArgs);
            Assert.Contains("--Verbose=true", parsedArgs);
        }

        [Fact]
        public void Parse_ShouldHandleStandardArgs_And_Flags()
        {
            // Arrange
            var requirements = new List<ArgRequirement>
            {
                new ArgRequirement("Output", "o", "output", "./logs"),
                new ArgRequirement("Verbose", "v", "verbose", "false", isFlag: true)
            };
            string[] args = new[] { "-o", "./custom", "-v" };

            // Act
            requirements.CheckAgains(args, out List<string>? parsedArgs);

            // Assert
            Assert.NotNull(parsedArgs);
            Assert.Contains("--Output", parsedArgs);
            Assert.Contains("./custom", parsedArgs);
            Assert.Contains("--Verbose=true", parsedArgs);
        }
    }
}
