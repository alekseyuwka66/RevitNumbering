using RevitNumbering.Core.Common;

namespace RevitNumbering.Core.Domain.Elements
{
    public sealed class LevelData
    {
        public LevelData(string key, string name, double elevationMm,
            IReadOnlyDictionary<string, string> textParameters)
        {
            Key = key;
            Name = name ?? string.Empty;
            ElevationMm = elevationMm;
            TextParameters = DtoCollections.CopyDictionary(textParameters);
        }

        public string Key { get; }

        public string Name { get; }

        public double ElevationMm { get; }

        /// <summary>Text values of level parameters by name.</summary>
        public IReadOnlyDictionary<string, string> TextParameters { get; }
    }
}
