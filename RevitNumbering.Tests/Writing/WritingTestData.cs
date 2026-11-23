using RevitNumbering.Core.Model;
using RevitNumbering.Core.Writing;

namespace RevitNumbering.Tests.Writing
{
    /// <summary>
    /// Builders and assertions shared by the write tests.
    /// </summary>
    internal static class WritingTestData
    {
        public static readonly DocumentKey DocumentA = new DocumentKey("A");
        public static readonly DocumentKey DocumentB = new DocumentKey("B");

        public static readonly CategoryInfo Doors =
            new CategoryInfo("Doors", "Двери", "Д-{Уровень}-{N:000}", 500, ConflictScope.Document, false);

        public static readonly CategoryInfo Rooms =
            new CategoryInfo("Rooms", "Помещения", "{Уровень}{N:00}", 3000, ConflictScope.Phase, true);

        /// <summary>Item with distinct current and new values built from the id.</summary>
        public static WriteItem Item(long id)
        {
            return new WriteItem(id, "old-" + id, "new-" + id);
        }

        public static WriteItem[] Items(params long[] ids)
        {
            return ids.Select(Item).ToArray();
        }

        public static WritePlan Plan(params long[] ids)
        {
            return new WritePlan(DocumentA, 8, Doors.Key, Doors.DisplayName, Items(ids));
        }

        /// <summary>Facts that allow writing the item built by <see cref="Item"/>.</summary>
        public static ElementWriteFacts GoodFacts(long id)
        {
            return new ElementWriteFacts(id, true, false, null, CentralState.CurrentWithCentral, true, "old-" + id);
        }

        public static ElementWriteFacts DeletedInCentral(long id)
        {
            return new ElementWriteFacts(id, true, false, null, CentralState.DeletedInCentral, true, "old-" + id);
        }

        public static WriteFailure RolledBackFailure(long id, string detail)
        {
            return new WriteFailure(id, WriteFailReason.RolledBackByWarning, detail);
        }

        public static ElementData Element(long id, string currentValue, string phaseKey = null)
        {
            return new ElementData(id, "name", "L1", "Этаж 1", currentValue, 0, 0, phaseKey, false, false, null,
                true, 0, false);
        }

        public static long[] Ids(IEnumerable<WriteItem> items)
        {
            return items.Select(item => item.Id).ToArray();
        }

        public static void AssertFailure(long id, WriteFailReason reason, string detail, WriteFailure actual)
        {
            Assert.NotNull(actual);
            Assert.Equal(id, actual.Id);
            Assert.Equal(reason, actual.Reason);
            Assert.Equal(detail, actual.Detail);
        }
    }
}
