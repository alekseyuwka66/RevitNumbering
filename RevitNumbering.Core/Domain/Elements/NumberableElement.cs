namespace RevitNumbering.Core.Domain.Elements
{
    /// <summary>
    /// The base numbering unit. Every category has its own sealed descendant with its own data and rules.
    /// Raw facts only: the adapter fills them, decisions are made here and in the engine.
    /// </summary>
    public abstract class NumberableElement
    {
        protected NumberableElement(ElementCommon common)
        {
            Common = common ?? throw new ArgumentNullException(nameof(common));
        }

        public long Id => Common.Id;

        public string Name => Common.Name;

        /// <summary>null: the element has no level.</summary>
        public string LevelKey => Common.LevelKey;

        public string LevelName => Common.LevelName;

        public string CurrentValue => Common.CurrentValue;

        /// <summary>null: the element has no point and goes after the ordered ones.</summary>
        public PlanPoint? Location => Common.Location;

        public EditState Edit => Common.Edit;

        /// <summary>Area of the conflict search. null: the whole document.</summary>
        public virtual string ConflictGroup => null;

        protected ElementCommon Common { get; }

        /// <summary>
        /// Template Method: the order of checks is fixed here; a category adds its own states in
        /// ClassifySpecific. levelMissing: the template needs a level and the element has no known level.
        /// null: the element is numbered.
        /// </summary>
        public FixReason Classify(bool levelMissing)
        {
            if (Edit.IsOwnedByOtherUser)
            {
                return FixReason.OwnedByOtherUser;
            }

            if (Edit.IsParameterReadOnly)
            {
                return FixReason.ReadOnlyParameter;
            }

            FixReason own = ClassifySpecific();
            if (own != null)
            {
                return own;
            }

            return levelMissing ? FixReason.NoLevel : null;
        }

        /// <summary>States of the category. null: none.</summary>
        protected virtual FixReason ClassifySpecific()
        {
            return null;
        }
    }
}
