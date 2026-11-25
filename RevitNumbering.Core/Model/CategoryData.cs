namespace RevitNumbering.Core.Model
{
    public sealed class LevelInfo
    {
        public long Id { get; set; }

        public string Name { get; set; }

        public double ElevationMm { get; set; }
    }

    /// <summary>All elements of one category of a document and the levels of the document.</summary>
    public sealed class CategoryData
    {
        public List<NumberingItem> Items { get; set; } = new List<NumberingItem>();

        public List<LevelInfo> Levels { get; set; } = new List<LevelInfo>();
    }
}
