namespace RevitNumbering.Core.Model
{
    public sealed class LevelInfo
    {
        public long Id { get; set; }

        public string Name { get; set; }

        public double ElevationMm { get; set; }
    }

    public sealed class CategoryData
    {
        public List<NumberingItem> Items { get; set; } = new List<NumberingItem>();

        public List<LevelInfo> Levels { get; set; } = new List<LevelInfo>();

        // Returns null when there is no level, or the level is not in the document.
        public LevelInfo FindLevel(long? levelId)
        {
            return Levels.Find(level => level.Id == levelId);
        }
    }
}
