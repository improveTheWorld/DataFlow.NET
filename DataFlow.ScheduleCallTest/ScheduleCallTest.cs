using Xunit;
using DataFlow.Framework;
using System.Threading;
using DataFlow.Log;


namespace DataFlow.Tests
{

    public class ScheduleCallTest
    {
        const int tabSize = 5;
        const int MaxTest = 10000;

        int[] sharedTab = new int[tabSize];
        int[] sharedTabScreenShot = new int[tabSize];
        void updateIndexCasenSharedTab(int index)
        {
            for (int i = 0; i <= MaxTest; i++)
            {
                sharedTab[index] = i;
            }
        }
        void ActionToCall0()
        {
            updateIndexCasenSharedTab(0);
        }

        void ActionToCall1()
        {
            updateIndexCasenSharedTab(1);
        }
        void ActionToCall2()
        {
            updateIndexCasenSharedTab(2);
        }

        void ActionToCall3()
        {
            updateIndexCasenSharedTab(3);
        }
        void ActionToCall4()
        {
            updateIndexCasenSharedTab(4);
        }


        void takeScreen()
        {
            for (int i = 0; i < tabSize; i++)
            {
                sharedTabScreenShot[i] = sharedTab[i];
            }
        }

        bool checkSharedEvoluated()
        {
            for (int i = 0; i < 1; i++)
            {
                if (sharedTabScreenShot[i] != MaxTest && sharedTabScreenShot[i] == sharedTab[i])
                {
                    return false;
                }
            }

            return true;
        }

        [Fact]
        public void ScheduleCallAsynchFunction()
        {
            ScheduleCall.Schedule(ActionToCall0, 10);
            ScheduleCall.Schedule(ActionToCall1, 10);
            ScheduleCall.Schedule(ActionToCall2, 10);
            ScheduleCall.Schedule(ActionToCall3, 10);
            ScheduleCall.Schedule(ActionToCall4, 10);

            Thread.Sleep(100);

            for (int i = 0; i < 10; i++)
            {
                takeScreen();
                Thread.Sleep(40);
                Assert.True(checkSharedEvoluated());
            }

        }

        int sharedData = 0;

        void updateShared()
        {
            sharedData += 10;
        }

        [Fact]
        public void ScheduleCall_Schedule()
        {
            sharedData = 0;
            iLogger.AddFileLogger(@"C:\CodeSource\DataFlow\src\DataFlow.ScheduleCallTest\iCodeTestsLog\ScheduleCall_Schedule.txt");

            iLogger.Filters.IncludeTimestamp = true;  
            ScheduleCall.Schedule(updateShared, false, 10, 20,  1030, 30).WatchByLogger();
            ScheduleCall.Start();
            Thread.Sleep(100);
            Assert.Equal(30,sharedData);

            Thread.Sleep(1000);
            Assert.Equal(40,sharedData);

        }

        [Fact]
        public void ScheduleCall_ScheduleAgainWhileFirstSchedule()
        {
            iLogger.AddFileLogger(@"C:\CodeSource\DataFlow\src\DataFlow.ScheduleCallTest\iCodeTestsLog\ScheduleCall_ScheduleAgainWhileFirstSchedule.txt"); 

            ScheduleCall.Schedule(updateShared,false, 10, 400).WatchByLogger();
            ScheduleCall.Start();

            Thread.Sleep(100);
            Assert.Equal(10, sharedData);

            ScheduleCall.Schedule(updateShared, 200, 400);

            Thread.Sleep(230);
            Assert.Equal(20, sharedData);

            Thread.Sleep(100);
            Assert.Equal(30, sharedData);

            Thread.Sleep(100);
            Assert.Equal(40, sharedData);

        }
    }
}
