using Xunit;
using iCode.Framework.ElementaryBlocks;
using iCode.Framework;
using iCode.Log;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace iCode.Tests
{
    public class AsyncEnumerableTest
    {

        interface IFactory<T>
        {
            public T Create();
        }

        struct Coordinate
        {
            public int X;
            int Y;
            public Coordinate(int x, int y)
            {
                X = x;
                Y = y;
            }

            public override string ToString()
            {
                return $"X:{X}, Y:{Y}";
            }
            public void logtest()
            {
                this.Trace($"X:0, Y:1");
            }
        }

        class MouseCoordFactory : IFactory<Coordinate>
        {
            int Coord = 0;
            public MouseCoordFactory(int coord)
            {
                Coord = coord;
            }
            public Coordinate Create()
            {
                Coord++;
                this.Trace($"MouseCoordinateFactory : Create new with coord = {Coord}");
                return new Coordinate(Coord, Coord);
            }
        }

        class OddCoordFactory : IFactory<Coordinate>
        {
            int Coord = -1;
            public OddCoordFactory(int coord)
            {
                Coord = coord;
            }
            public Coordinate Create()
            {
                Coord += 2;
                this.Trace($"MouseCoordinateFactory : Create new with coord = {Coord}");
                return new Coordinate(Coord, Coord);
            }
        }


        class EvenCoordFactory : IFactory<Coordinate>
        {
            int Coord = 0;

            public EvenCoordFactory(int coord)
            {
                Coord = coord;
            }

            public Coordinate Create()
            {
                Coord += 2;
                this.Trace($"MouseCoordinateFactory : Create new with coord = {Coord}");
                return new Coordinate(Coord, Coord);
            }
        }

        class AsyncEnumerableConsumerr<T>
        {
            public  List<T> savedItems;
            readonly AsyncEnumerable<T> MyAsyncEnumerable;
            public AsyncEnumerableConsumerr(AsyncEnumerable<T> asyncEnumerable)
            {
                MyAsyncEnumerable = asyncEnumerable;
                savedItems = new List<T>();
            }

            public async void start()
            {                
                await foreach (var item in MyAsyncEnumerable)
                {
                    savedItems.Add(item);
                }

                savedItems.Clear();
            }
        }

        [Fact]
        public async void AsyncEnumerator_MultipleSources_Dispose()
        {
            (var oddCoordPublisher, var evenCoordPublisher, var coordPublisher, var everyTic, var evenTic, var oddTic, var var1, var var2, var var3) = setUpDifferentFoctories(30, 30, 30);

            AsyncEnumerable<Coordinate> allItemsAsync = new AsyncEnumerable<Coordinate>().
                                                                ListenTo(coordPublisher).
                                                                ListenTo(evenCoordPublisher).
                                                                ListenTo(oddCoordPublisher);

            var consumer = new AsyncEnumerableConsumerr<Coordinate>(allItemsAsync);


            Task.WaitAll(everyTic, oddTic, evenTic, ScheduleCall.Schedule(consumer.start, 0).Task, Task.Delay(1000));

            Assert.True(consumer.savedItems.Count == 90, $" Count == {consumer.savedItems.Count}");


            // test that  the dispose free the ressources
            allItemsAsync.Dispose();

            await Task.Delay(10);

            Assert.True(consumer.savedItems.Count == 0, $" Count == {consumer.savedItems.Count}");
        }

        (DataPublisher<Coordinate>, DataPublisher<Coordinate>, DataPublisher<Coordinate>, Task?,Task?,Task?, MouseCoordFactory, EvenCoordFactory, OddCoordFactory) setUpDifferentFoctories(int nbrElemInEvryTics, int nbrElemInEvenTics,int nbrElemInOddTics)
        {
            DataPublisher<Coordinate> oddCoordPublisher = new DataPublisher<Coordinate>();
            DataPublisher<Coordinate> evenCoordPublisher = new DataPublisher<Coordinate>();
            DataPublisher<Coordinate> coordPublisher = new DataPublisher<Coordinate>();

            OddCoordFactory oddFactory = new OddCoordFactory(1);
            EvenCoordFactory evenFactory = new EvenCoordFactory(0);
            MouseCoordFactory mousefactory = new MouseCoordFactory(0);

            oddCoordPublisher.WatchByLogger("oddCoordPublisher");
            evenCoordPublisher.WatchByLogger();
            coordPublisher.WatchByLogger();
            oddFactory.WatchByLogger();
            evenFactory.WatchByLogger();
            mousefactory.WatchByLogger();


            var everyTic  = new PeriodicCall(() => coordPublisher.PublishDataAsync(mousefactory.Create()), 1,0, nbrElemInEvryTics).Task;
            var evenTic = new PeriodicCall(() => evenCoordPublisher.PublishDataAsync(evenFactory.Create()), 2,0, nbrElemInEvenTics).Task;
            Thread.Sleep(1);
            var oddTic = new PeriodicCall(() => oddCoordPublisher.PublishDataAsync(oddFactory.Create()), 2, nbrElemInOddTics).Task;

            return (coordPublisher,evenCoordPublisher, oddCoordPublisher,everyTic,evenTic,oddTic, mousefactory, evenFactory, oddFactory);
        }

        [Fact]
        public async void  LoopOnAsyncEnumeratorMultipleSources_FactoryDispose()
        {

            (var oddCoordPublisher, var evenCoordPublisher, var coordPublisher, var everyTic, var evenTic, var oddTic, var fact ,var evenFactory, var oddFactory) = setUpDifferentFoctories(30, 30, 30);



            AsyncEnumerable<Coordinate> allItemsAsync = new AsyncEnumerable<Coordinate>(null,null,
                                                coordPublisher,evenCoordPublisher,oddCoordPublisher);
            
            var consumer = new AsyncEnumerableConsumerr<Coordinate>(allItemsAsync);


            Task.WaitAll(everyTic, oddTic, evenTic, ScheduleCall.Schedule(consumer.start, 0).Task, Task.Delay(1000));

            Assert.True(consumer.savedItems.Count == 90,$" Count == {consumer.savedItems.Count}");

            coordPublisher.Dispose();
            
            oddTic = new PeriodicCall(() => oddCoordPublisher.PublishDataAsync(oddFactory.Create()), 1, 1, 10).Task;
            evenTic = new PeriodicCall(() => evenCoordPublisher.PublishDataAsync(evenFactory.Create()), 1, 1, 10).Task;

            Task.WaitAll( oddTic, evenTic, Task.Delay(1000));

            Assert.True(consumer.savedItems.Count == 110, $" Count == {consumer.savedItems.Count}");

            oddCoordPublisher.Dispose();
            evenTic = new PeriodicCall(() => evenCoordPublisher.PublishDataAsync(evenFactory.Create()), 1, 1, 10).Task;
            Task.WaitAll(evenTic, Task.Delay(1000));

            Assert.True(consumer.savedItems.Count == 120, $" Count == {consumer.savedItems.Count}");

            evenCoordPublisher.Dispose();
            await Task.Delay(10);

            Assert.True(consumer.savedItems.Count == 0, $" Count == {consumer.savedItems.Count}");


            

        }
    }
}