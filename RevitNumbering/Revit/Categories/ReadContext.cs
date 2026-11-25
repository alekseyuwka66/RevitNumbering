using Autodesk.Revit.DB;
using RevitNumbering.Core.Domain.Elements;
using RevitNumbering.Revit.Common;

namespace RevitNumbering.Revit.Categories
{
    /// <summary>
    /// Reading of one load attempt: the document and caches shared by all elements of the attempt.
    /// Lives only inside one call in the API context.
    /// </summary>
    public sealed class ReadContext
    {
        private readonly Dictionary<long, Level> _levels = new Dictionary<long, Level>();

        public ReadContext(Document document)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            foreach (Level level in new FilteredElementCollector(document).OfClass(typeof(Level)).Cast<Level>())
            {
                _levels[RevitIds.ToLong(level.Id)] = level;
            }
        }

        public Document Document { get; }

        /// <summary>Key and name of a level; InvalidElementId or not a level gives (null, null).</summary>
        public void ReadLevel(ElementId levelId, out string key, out string name)
        {
            key = null;
            name = null;
            if (RevitIds.IsValid(levelId) && _levels.TryGetValue(RevitIds.ToLong(levelId), out Level level))
            {
                key = RevitIds.ToKey(level.Id);
                name = level.Name;
            }
        }

        /// <summary>Occupancy under worksharing (F8-08) and writability of the target parameter.</summary>
        public EditState ReadEditState(Element element, Parameter parameter)
        {
            bool readOnly = parameter == null || parameter.IsReadOnly;
            if (!Document.IsWorkshared)
            {
                return new EditState(false, null, readOnly);
            }

            CheckoutStatus status = WorksharingUtils.GetCheckoutStatus(Document, element.Id, out string owner);
            bool owned = status == CheckoutStatus.OwnedByOtherUser;
            return new EditState(owned, owned ? owner : null, readOnly);
        }

        public static PlanPoint? ToPlanPoint(XYZ point)
        {
            if (point == null)
            {
                return null;
            }

            return new PlanPoint(RevitUnits.ToMm(point.X), RevitUnits.ToMm(point.Y));
        }

        /// <summary>Key of a phase; InvalidElementId gives null.</summary>
        public static string PhaseKey(ElementId phaseId)
        {
            return RevitIds.IsValid(phaseId) ? RevitIds.ToKey(phaseId) : null;
        }
    }
}
