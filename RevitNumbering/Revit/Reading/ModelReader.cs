using System.Diagnostics;
using Autodesk.Revit.DB;
using RevitNumbering.Core.Documents;
using RevitNumbering.Core.Domain.Categories;
using RevitNumbering.Core.Domain.Elements;
using RevitNumbering.Core.Gateway;
using RevitNumbering.Core.Logging;
using RevitNumbering.Core.Reading;
using RevitNumbering.Revit.Categories;
using RevitNumbering.Revit.Common;
using RevitNumbering.Revit.Documents;

namespace RevitNumbering.Revit.Reading
{
    /// <summary>
    /// Reads a category of the whole document in slices (07 §3.4). Does not know the categories:
    /// it finds the category by key and calls its own Read. API context only.
    /// </summary>
    internal sealed class ModelReader : ICategorySliceReader
    {
        private readonly RevitActiveDocumentService _documents;
        private readonly CategoryRegistry<IRevitCategory> _categories;
        private readonly CategoryCollector _collector;
        private readonly ILog _log;

        public ModelReader(RevitActiveDocumentService documents, CategoryRegistry<IRevitCategory> categories,
            CategoryCollector collector, ILog log)
        {
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _categories = categories ?? throw new ArgumentNullException(nameof(categories));
            _collector = collector ?? throw new ArgumentNullException(nameof(collector));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public LoadAttempt Begin(DocumentKey document, string categoryKey)
        {
            Stopwatch watch = Stopwatch.StartNew();
            Document doc = RequireDocument(document);
            long generation = _documents.GetGeneration(document);

            IRevitCategory category = _categories.Find(categoryKey)
                ?? throw new ArgumentException("Unknown category: " + categoryKey, nameof(categoryKey));

            long[] ids = _collector.CollectIds(doc, category).Select(RevitIds.ToLong).OrderBy(id => id).ToArray();
            List<Level> levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().ToList();
            IReadOnlyList<LevelData> levelData = ReadLevels(levels);

            IEnumerable<long> known = ids
                .Concat(levels.Select(level => RevitIds.ToLong(level.Id)))
                .Concat(doc.Phases.Cast<Phase>().Select(phase => RevitIds.ToLong(phase.Id)));
            _documents.SetSnapshotScope(document, category.BuiltInCategory, known);

            var attempt = new LoadAttempt(document, categoryKey, generation, ids, levelData);
            ReadSlice(doc, category, attempt, watch, atLeastOne: false);
            return attempt;
        }

        public bool ReadNext(LoadAttempt attempt, bool ignoreGenerationChange)
        {
            if (attempt == null)
            {
                throw new ArgumentNullException(nameof(attempt));
            }

            Stopwatch watch = Stopwatch.StartNew();
            Document doc = RequireDocument(attempt.Document);
            if (_documents.GetGeneration(attempt.Document) != attempt.Generation)
            {
                if (!ignoreGenerationChange)
                {
                    return false;
                }

                attempt.GenerationChanged = true;
            }

            IRevitCategory category = _categories.Find(attempt.CategoryKey)
                ?? throw new ArgumentException("Unknown category: " + attempt.CategoryKey, nameof(attempt));
            ReadSlice(doc, category, attempt, watch, atLeastOne: true);
            return true;
        }

        /// <summary>The document of the key, only while it is the active project; otherwise the load is cancelled.</summary>
        private Document RequireDocument(DocumentKey document)
        {
            Document doc = _documents.GetActiveDocument(document);
            if (doc == null)
            {
                throw new OperationCanceledException("The document of the load is closed or not active.");
            }

            if (doc.IsFamilyDocument)
            {
                throw new InvalidOperationException("Family documents are not supported.");
            }

            return doc;
        }

        private void ReadSlice(Document doc, IRevitCategory category, LoadAttempt attempt, Stopwatch watch,
            bool atLeastOne)
        {
            var context = new ReadContext(doc);
            bool readAny = false;
            while (!attempt.IsComplete
                && ((atLeastOne && !readAny) || watch.ElapsedMilliseconds < GatewayParameters.LoadSliceMs))
            {
                long id = attempt.Ids[attempt.NextIndex];
                NumberableElement element = ReadOne(doc, category, context, id);
                if (element != null)
                {
                    attempt.Elements.Add(element);
                }

                attempt.NextIndex++;
                readAny = true;
            }
        }

        private NumberableElement ReadOne(Document doc, IRevitCategory category, ReadContext context, long id)
        {
            try
            {
                Element element = doc.GetElement(RevitIds.ToElementId(id));
                if (element == null || !element.IsValidObject || _collector.IsExcluded(element, category))
                {
                    return null;
                }

                return category.Read(element, context);
            }
            catch (Autodesk.Revit.Exceptions.InvalidObjectException)
            {
                // Deleted between slices: skipped.
                return null;
            }
        }

        /// <summary>07 §3.6, AA-73: by elevation, then by id; text parameters by name, the first one wins.</summary>
        private IReadOnlyList<LevelData> ReadLevels(IEnumerable<Level> levels)
        {
            var result = new List<LevelData>();
            foreach (Level level in levels
                .OrderBy(l => RevitUnits.ToMm(l.ProjectElevation))
                .ThenBy(l => RevitIds.ToLong(l.Id)))
            {
                var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (Parameter parameter in level.GetOrderedParameters())
                {
                    if (parameter.StorageType != StorageType.String)
                    {
                        continue;
                    }

                    string name = parameter.Definition?.Name ?? string.Empty;
                    if (parameters.ContainsKey(name))
                    {
                        _log.Info("Повтор имени параметра уровня: " + RevitIds.ToKey(level.Id) + ", " + name);
                        continue;
                    }

                    parameters.Add(name, parameter.AsString() ?? string.Empty);
                }

                result.Add(new LevelData(RevitIds.ToKey(level.Id), level.Name, RevitUnits.ToMm(level.ProjectElevation),
                    parameters));
            }

            return result;
        }
    }
}
