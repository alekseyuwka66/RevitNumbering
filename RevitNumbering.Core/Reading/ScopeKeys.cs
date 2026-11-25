namespace RevitNumbering.Core.Reading
{
    /// <summary>Keys of the scopes registered by the adapter; the window refers to them by these constants.</summary>
    public static class ScopeKeys
    {
        /// <summary>No limit: the whole document.</summary>
        public const string Document = "document";

        /// <summary>F5-02: the selection snapshot taken by the button.</summary>
        public const string Selection = "selection";

        /// <summary>Elements of the active view (decision of the user; shown in the window — open question).</summary>
        public const string ActiveView = "activeView";
    }
}
