using RevitNumbering.Core.Writing;

namespace RevitNumbering.Tests.Writing
{
    /// <summary>Retry decision after a rollback.</summary>
    public class WriteRetryPolicyTests
    {
        private static long[] Next(long[] attempted, long[] failed, int attemptNumber)
        {
            return WritingTestData.Ids(
                WriteRetryPolicy.NextAttempt(WritingTestData.Items(attempted), failed, attemptNumber));
        }

        [Fact]
        public void FailedItemIsExcluded()
        {
            Assert.Equal(new long[] { 101, 103, 104 }, Next(new long[] { 101, 102, 103, 104 }, new long[] { 102 }, 1));
        }

        [Fact]
        public void OrderIsKept()
        {
            Assert.Equal(new long[] { 105, 103 }, Next(new long[] { 105, 101, 103 }, new long[] { 101 }, 2));
        }

        [Fact]
        public void SeveralFailures()
        {
            Assert.Equal(new long[] { 102 }, Next(new long[] { 101, 102, 103 }, new long[] { 101, 103 }, 3));
        }

        [Fact]
        public void NoFailedItems_NothingToRetry()
        {
            Assert.Empty(Next(new long[] { 101, 102 }, new long[0], 1));
        }

        [Fact]
        public void EmptyIntersection_NothingToRetry()
        {
            Assert.Empty(Next(new long[] { 101, 102 }, new long[] { 999 }, 1));
        }

        [Fact]
        public void FourthAttempt_StillRetries()
        {
            Assert.Equal(new long[] { 102, 103 }, Next(new long[] { 101, 102, 103 }, new long[] { 101 }, 4));
        }

        [Fact]
        public void AttemptLimit_NothingToRetry()
        {
            Assert.Equal(5, WritingParameters.WriteMaxAttempts);
            Assert.Empty(Next(new long[] { 101, 102, 103 }, new long[] { 101 }, 5));
        }

        [Fact]
        public void AllFailed_NothingToRetry()
        {
            Assert.Empty(Next(new long[] { 101, 102 }, new long[] { 101, 102 }, 1));
        }

        [Fact]
        public void OneFailurePerAttempt()
        {
            var expected = new[]
            {
                new long[] { 2, 3, 4, 5, 6 },
                new long[] { 3, 4, 5, 6 },
                new long[] { 4, 5, 6 },
                new long[] { 5, 6 },
                new long[0]
            };

            IReadOnlyList<WriteItem> attempt = WritingTestData.Items(1, 2, 3, 4, 5, 6);
            for (int k = 1; k <= 5; k++)
            {
                IReadOnlyList<WriteItem> next = WriteRetryPolicy.NextAttempt(attempt, new long[] { k }, k);

                Assert.Equal(expected[k - 1], WritingTestData.Ids(next));
                if (next.Count > 0)
                {
                    attempt = next;
                }
            }
        }

        [Fact]
        public void InvalidAttemptNumber_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => WriteRetryPolicy.NextAttempt(WritingTestData.Items(101), new long[] { 101 }, 0));
        }
    }
}
