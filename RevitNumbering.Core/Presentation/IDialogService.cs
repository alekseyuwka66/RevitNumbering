namespace RevitNumbering.Core.Presentation
{
    public enum SaveChoice
    {
        Save,
        Discard,
        Cancel
    }

    public interface IDialogService
    {
        SaveChoice AskSaveSettings();

        /// <summary>null: cancelled.</summary>
        string PickExportPath();

        string PickImportPath();
    }
}
