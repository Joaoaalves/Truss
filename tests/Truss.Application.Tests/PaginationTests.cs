using Truss.Application;
using Xunit;

namespace Truss.Application.Tests
{
    public class PaginationTests
    {
        [Fact]
        public void PageRequest_ComputesTheSkip()
        {
            Assert.Equal(0, new PageRequest(1, 20).Skip);
            Assert.Equal(40, new PageRequest(3, 20).Skip);
        }

        [Theory]
        [InlineData(0, 10, 0)]
        [InlineData(1, 10, 1)]
        [InlineData(10, 10, 1)]
        [InlineData(11, 10, 2)]
        [InlineData(21, 10, 3)]
        public void PageResult_ComputesTheTotalPages(int totalCount, int size, int expectedPages)
        {
            var result = new PageResult<int>([], 1, size, totalCount);

            Assert.Equal(expectedPages, result.TotalPages);
        }

        [Fact]
        public void PageResult_KnowsItsNeighbors()
        {
            var first = new PageResult<int>([1, 2], 1, 2, 5);
            Assert.False(first.HasPreviousPage);
            Assert.True(first.HasNextPage);

            var last = new PageResult<int>([5], 3, 2, 5);
            Assert.True(last.HasPreviousPage);
            Assert.False(last.HasNextPage);
        }

        [Fact]
        public void PageResult_WithZeroSize_HasNoPages()
        {
            var result = new PageResult<int>([], 1, 0, 5);

            Assert.Equal(0, result.TotalPages);
            Assert.False(result.HasNextPage);
        }
    }
}
