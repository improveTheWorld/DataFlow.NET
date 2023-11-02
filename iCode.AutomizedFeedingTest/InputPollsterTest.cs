
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;


namespace iCode.Tests
{
    internal class DataGeneratorThread
    {
        public int data;

        private readonly ITestOutputHelper _output;

        public DataGeneratorThread(ITestOutputHelper testOutput)
        {

            _output = testOutput;
            data = 0;
        }

        public void generate()
        {
            new Task(() =>
            {
                while (data < 20)
                {
                    data++;
                    _output.WriteLine($"Data updated to {data}");
                    Thread.Sleep(1000);
                }

            }); 
        }


       
    }

    public class InputPollsterTest
    {
        private readonly ITestOutputHelper _output;
        public InputPollsterTest(ITestOutputHelper testOutput)
        {
            _output = testOutput;
        }

        [Fact]
        public void TestPolling()
        {
            var dataGen = new DataGeneratorThread(_output);
            dataGen.generate();
            for (int i = 0;i<10; i++)
            {
                _output.WriteLine($"Polling value {dataGen.data}");
                Thread.Sleep(500);
            }
            
        }

    }
}
