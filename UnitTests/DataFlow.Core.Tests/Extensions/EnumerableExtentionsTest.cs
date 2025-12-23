using Xunit;
using System.Linq;
using DataFlow.Extensions;

namespace DataFlow.Tests
{
    public class EnumerableExtentionsTest
    {
        [Fact]
        public void CombineOrderedWithIntEnums()
        {
            int[] ordered1 = { 1, 5, 6, 8, 10 };
            int[] ordered2 = { 0, 1, 1, 2, 7, 9, 10, 11 };

            int[] ordered3 = ordered1.MergeOrdered<int>(ordered2, (x, y) => x < y).ToArray();
            int[] expectedResult = { 0, 1, 1, 1, 2, 5, 6, 7, 8, 9, 10, 10, 11 };
            Assert.Equal(expectedResult, ordered3);
        }

        [Fact]
        public void CombineOrderedWithIntEnumsParamsEmpty()
        {
            int[] ordered1 = {};
            int[] ordered2 = {};

            int[] ordered3 = ordered1.MergeOrdered<int>(ordered2, (x, y) => x < y).ToArray();
            int[] expectedResult = { };
            Assert.Equal(expectedResult, ordered3);
        }

        [Fact]
        public void CombineOrderedWithIntEnumsSecondtParamEmpty()
        {
            int[] ordered1 = { 1, 5, 6, 8, 10 };
            int[] ordered2 = { };

            int[] ordered3 = ordered1.MergeOrdered<int>(ordered2, (x, y) => x < y).ToArray();
            int[] expectedResult = { 1, 5, 6, 8, 10 };
            Assert.Equal(expectedResult, ordered3);
        }

        [Fact]
        public void CombineOrderedWithIntEnumsFirstParamEmpty()
        {
            int[] ordered1 = { };
            int[] ordered2 = { 1, 5, 6, 8, 10 };

            int[] ordered3 = ordered1.MergeOrdered<int>(ordered2, (x, y) => x < y).ToArray();
            int[] expectedResult = { 1, 5, 6, 8, 10 };
            Assert.Equal(expectedResult, ordered3);
        }

        [Fact]
        public void CombineOrderedWithIntEnumsParamsNull()
        {
            int[] ordered1 = null;
            int[] ordered2 = null;

            int[] ordered3 = ordered1.MergeOrdered<int>(ordered2, (x, y) => x < y).ToArray();
            int[] expectedResult = { };
            Assert.Equal(expectedResult, ordered3);
        }

        [Fact]
        public void CombineOrderedWithIntEnumsFirstParamNull()
        {
            int[] ordered1 = null;
            int[] ordered2 = { 1, 5, 6, 8, 10 };

            int[] ordered3 = ordered1.MergeOrdered<int>(ordered2, (x, y) => x < y).ToArray();
            int[] expectedResult = { 1, 5, 6, 8, 10 };
            Assert.Equal(expectedResult, ordered3);
        }

        [Fact]
        public void CombineOrderedWithIntEnumsSecondParamNull()
        {
            int[] ordered1 = { 1, 5, 6, 8, 10 };
            int[] ordered2 = null;

            int[] ordered3 = ordered1.MergeOrdered<int>(ordered2, (x, y) => x < y).ToArray();
            int[] expectedResult = { 1, 5, 6, 8, 10 };
            Assert.Equal(expectedResult, ordered3);
        }

        [Fact]
        public void CombineOrderedWithIntEnumsonElementEnum()
        {
            int[] ordered1 = { 1 };
            int[] ordered2 = { 5};

            int[] ordered3 = ordered1.MergeOrdered<int>(ordered2, (x, y) => x < y).ToArray();
            int[] expectedResult = {1,5 };
            Assert.Equal(expectedResult, ordered3);

            //////////////////////////////////// 
            

            ordered1[0] = 0;
            ordered2[0] = 0;

            int[] ordered4 = ordered1.MergeOrdered<int>(ordered2, (x, y) => x < y).ToArray();
            int[] expectedResult2 = { 0, 0 };
            Assert.Equal(expectedResult2, ordered4);
        }


    }
}