namespace RevitNumbering.UI.Resources
{
    /// <summary>
    /// Texts found by a composed key: "Category_Doors", "LockReason_OwnedByOther" and the like.
    /// A missing string gives the key itself; tests do not let it come to that.
    /// </summary>
    public static class TextLookup
    {
        public static string Get(string key)
        {
            return Texts.ResourceManager.GetString(key, Texts.Culture) ?? key;
        }

        public static string Category(string categoryKey)
        {
            return Get("Category_" + categoryKey);
        }
    }
}
