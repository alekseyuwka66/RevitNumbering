using RevitNumbering.Core.Model;
using RevitNumbering.Core.Writing;
using static RevitNumbering.Tests.Writing.WritingTestData;

namespace RevitNumbering.Tests.Writing
{
    /// <summary>Admission, precheck, attempts and refusals.</summary>
    public class WriteSessionTests
    {
        private const string Detail204 = "";

        /// <summary>Plan 201…206 after the precheck with 204 deleted in central (state after the precheck).</summary>
        private static WriteSession PrecheckedSession()
        {
            var session = new WriteSession(Plan(201, 202, 203, 204, 205, 206));
            session.Precheck(new[]
            {
                GoodFacts(201), GoodFacts(202), GoodFacts(203), DeletedInCentral(204), GoodFacts(205), GoodFacts(206)
            });
            return session;
        }

        private static WritePlan OneItemPlan()
        {
            return Plan(201);
        }

        [Fact]
        public void Admit_SameDocumentAndGeneration()
        {
            Assert.Null(WriteSession.Admit(OneItemPlan(), DocumentA, 8, false));
        }

        [Fact]
        public void Admit_AnotherDocument()
        {
            Assert.Equal(WriteOutcome.DocumentMismatch, WriteSession.Admit(OneItemPlan(), DocumentB, 8, false));
        }

        [Fact]
        public void Admit_NoActiveDocument()
        {
            Assert.Equal(WriteOutcome.DocumentMismatch, WriteSession.Admit(OneItemPlan(), null, 8, false));
        }

        [Fact]
        public void Admit_ModelChanged()
        {
            Assert.Equal(WriteOutcome.Stale, WriteSession.Admit(OneItemPlan(), DocumentA, 9, false));
        }

        [Fact]
        public void Admit_EmptyPlan()
        {
            Assert.Equal(WriteOutcome.NothingToWrite, WriteSession.Admit(Plan(), DocumentA, 8, false));
        }

        [Fact]
        public void Admit_RuleOrder()
        {
            Assert.Equal(WriteOutcome.Cancelled, WriteSession.Admit(OneItemPlan(), DocumentB, 8, true));
            Assert.Equal(WriteOutcome.DocumentMismatch, WriteSession.Admit(OneItemPlan(), DocumentB, 9, false));
        }

        [Fact]
        public void Rejected_ReportIsEmpty()
        {
            WriteReport report = WriteSession.Rejected(WriteOutcome.Stale);

            Assert.Equal(WriteOutcome.Stale, report.Outcome);
            Assert.Empty(report.Written);
            Assert.Empty(report.Failures);
            Assert.Equal(0, report.Attempts);
        }

        [Fact]
        public void Precheck_RefusedElementLeavesTheAttempt()
        {
            WriteSession session = PrecheckedSession();

            Assert.Equal(new long[] { 201, 202, 203, 205, 206 }, Ids(session.CurrentAttempt));
            Assert.Equal(1, session.AttemptNumber);

            WriteReport report = session.Fail();
            AssertFailure(204, WriteFailReason.Deleted, Detail204, Assert.Single(report.Failures));
        }

        [Fact]
        public void ExampleOfSection39()
        {
            WriteSession session = PrecheckedSession();

            bool retry = session.RolledBack(new[] { RolledBackFailure(205, "W") }, new string[0]);

            Assert.True(retry);
            Assert.Equal(new long[] { 201, 202, 203, 206 }, Ids(session.CurrentAttempt));
            Assert.Equal(2, session.AttemptNumber);

            var written = session.CurrentAttempt.Select(item => new WrittenValue(item.Id, item.NewValue)).ToArray();
            WriteReport report = session.Commit(written);

            Assert.Equal(WriteOutcome.Committed, report.Outcome);
            Assert.Equal(new long[] { 201, 202, 203, 206 }, report.Written.Select(value => value.Id));
            Assert.Equal(2, report.Failures.Count);
            AssertFailure(204, WriteFailReason.Deleted, "", report.Failures[0]);
            AssertFailure(205, WriteFailReason.RolledBackByWarning, "W", report.Failures[1]);
            Assert.Equal(2, report.Attempts);
        }

        [Fact]
        public void FailureWithoutElement()
        {
            WriteSession session = PrecheckedSession();

            bool retry = session.RolledBack(new WriteFailure[0], new[] { "E1", "E1", "E2" });
            WriteReport report = session.Fail();

            Assert.False(retry);
            Assert.Equal(WriteOutcome.Failed, report.Outcome);
            Assert.Empty(report.Written);
            Assert.Equal(2, report.Failures.Count);
            AssertFailure(204, WriteFailReason.Deleted, "", report.Failures[0]);
            AssertFailure(0, WriteFailReason.RolledBackByWarning, "E1; E2", report.Failures[1]);
            Assert.Equal(1, report.Attempts);
        }

        [Fact]
        public void FailureWithoutElementAndText()
        {
            WriteSession session = PrecheckedSession();

            Assert.False(session.RolledBack(new WriteFailure[0], new string[0]));
            WriteReport report = session.Fail();

            AssertFailure(0, WriteFailReason.RolledBackByWarning, "Revit отменил запись без указания элементов",
                report.Failures[report.Failures.Count - 1]);
        }

        [Fact]
        public void ForeignIdIsDropped()
        {
            WriteSession session = PrecheckedSession();

            bool retry = session.RolledBack(new[] { RolledBackFailure(999, "W") }, new[] { "E" });
            WriteReport report = session.Fail();

            Assert.False(retry);
            Assert.DoesNotContain(report.Failures, failure => failure.Id == 999);
            AssertFailure(0, WriteFailReason.RolledBackByWarning, "E", report.Failures[report.Failures.Count - 1]);
        }

        [Fact]
        public void AttemptLimit()
        {
            var session = new WriteSession(Plan(301, 302, 303, 304, 305, 306, 307));
            session.Precheck(new long[] { 301, 302, 303, 304, 305, 306, 307 }.Select(GoodFacts).ToArray());

            var results = new List<bool>();
            for (long id = 301; id <= 305; id++)
            {
                results.Add(session.RolledBack(new[] { RolledBackFailure(id, "W") }, new string[0]));
            }

            WriteReport report = session.Fail();

            Assert.Equal(new[] { true, true, true, true, false }, results);
            Assert.Equal(WriteOutcome.Failed, report.Outcome);
            Assert.Equal(6, report.Failures.Count);
            for (int i = 0; i < 5; i++)
            {
                AssertFailure(301 + i, WriteFailReason.RolledBackByWarning, "W", report.Failures[i]);
            }

            AssertFailure(0, WriteFailReason.RolledBackByWarning, "превышено число попыток записи: 5",
                report.Failures[5]);
            Assert.Equal(5, report.Attempts);
        }

        [Fact]
        public void AllItemsFailed_NoZeroIdFailure()
        {
            var session = new WriteSession(Plan(401));
            session.Precheck(new[] { GoodFacts(401) });

            bool retry = session.RolledBack(new[] { RolledBackFailure(401, "W") }, new string[0]);
            WriteReport report = session.Fail();

            Assert.False(retry);
            AssertFailure(401, WriteFailReason.RolledBackByWarning, "W", Assert.Single(report.Failures));
            Assert.Equal(1, report.Attempts);
        }

        [Fact]
        public void RefusedBeforeTransaction()
        {
            var session = new WriteSession(Plan(201));
            session.Precheck(new[]
            {
                new ElementWriteFacts(201, false, false, null, CentralState.NotWorkshared, false, null)
            });

            Assert.Empty(session.CurrentAttempt);

            WriteReport report = session.Fail();

            Assert.Equal(WriteOutcome.Failed, report.Outcome);
            Assert.Empty(report.Written);
            AssertFailure(201, WriteFailReason.Deleted, "", Assert.Single(report.Failures));
            Assert.Equal(0, report.Attempts);
        }

        [Fact]
        public void SetRefused_RetryWithoutElement()
        {
            WriteSession session = PrecheckedSession();

            bool retry = session.RolledBack(new[] { new WriteFailure(202, WriteFailReason.NotUpToDate, "") },
                new string[0]);

            Assert.True(retry);
            Assert.Equal(new long[] { 201, 203, 205, 206 }, Ids(session.CurrentAttempt));
        }

        [Fact]
        public void Precheck_WrongFactCount_Throws()
        {
            var session = new WriteSession(Plan(201, 202));

            Assert.Throws<ArgumentException>(() => session.Precheck(new[] { GoodFacts(201) }));
        }

        [Fact]
        public void FinishedSession_RejectsFurtherCalls()
        {
            WriteSession session = PrecheckedSession();
            session.Fail();

            Assert.Throws<InvalidOperationException>(() => session.Commit(new WrittenValue[0]));
            Assert.Throws<InvalidOperationException>(() => session.RolledBack(null, null));
        }
    }
}
