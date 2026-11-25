using RevitNumbering.Core.Model;
using RevitNumbering.Core.Settings;
using RevitNumbering.Core.Templates;

namespace RevitNumbering.Core.Numbering
{
    /// <summary>Input of the calculation.</summary>
    public sealed class NumberingRequest
    {
        /// <summary>All elements of the category in the document.</summary>
        public List<NumberingItem> Items { get; set; }

        public List<LevelInfo> Levels { get; set; }

        /// <summary>Who gets numbers: rows visible after the search, or elements picked in the model.</summary>
        public HashSet<long> Scope { get; set; }

        public CategorySettings Settings { get; set; }

        /// <summary>Filled by the calculation after parsing; null when the template has errors.</summary>
        public Template Template { get; set; }

        /// <summary>null: no level or the level is not found.</summary>
        public LevelInfo FindLevel(long? levelId)
        {
            if (levelId == null)
            {
                return null;
            }

            foreach (LevelInfo level in Levels)
            {
                if (level.Id == levelId.Value)
                {
                    return level;
                }
            }

            return null;
        }
    }
}
