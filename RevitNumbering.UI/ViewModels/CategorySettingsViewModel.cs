using System.Text.Json.Serialization;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RevitNumbering.Core.Numbering;
using RevitNumbering.Core.Orderings;
using RevitNumbering.Core.Settings;
using RevitNumbering.Core.Templates;
using RevitNumbering.UI.Resources;

namespace RevitNumbering.UI.ViewModels
{
    // The settings panel: template parts and ordering of the current category.
    // It edits the CategorySettings object of the session directly and reports every change.
    public sealed partial class CategorySettingsViewModel : ObservableObject
    {
        private CategorySettings _settings;

        // Errors that block Apply.
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasError))]
        private string _errorText = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasWarning))]
        private string _warningText = string.Empty;

        public CategorySettingsViewModel()
        {
            // Every registered strategy: the JsonDerivedType lines of the base class.
            Orderings = typeof(Ordering).GetCustomAttributes(typeof(JsonDerivedTypeAttribute), false)
                .Cast<JsonDerivedTypeAttribute>()
                .Select(attribute => new Option<Type>(attribute.DerivedType,
                    TextLookup.Get("Ordering_" + attribute.TypeDiscriminator)))
                .ToList();
        }

        private void EditValue<T>(T? current, T value, Action<T> set, [CallerMemberName] string name = null)
            where T : struct
        {
            if (_settings == null || current == null || EqualityComparer<T>.Default.Equals(current.Value, value))
            {
                return;
            }

            set(value);
            Edited(name);
        }

        private void EditText(string current, string value, Action<string> set, [CallerMemberName] string name = null)
        {
            if (_settings == null || current == value)
            {
                return;
            }

            set(value);
            Edited(name);
        }

        private void Edited(string name)
        {
            OnPropertyChanged(name);
            OnPropertyChanged(nameof(Example));
            Changed?.Invoke();
        }

        // A setting changed: the numbers must be calculated again.
        public event Action Changed;

        public List<Option<Type>> Orderings { get; }

        public string Prefix
        {
            get => _settings?.Prefix ?? string.Empty;
            set => EditText(_settings?.Prefix, value ?? string.Empty, v => _settings.Prefix = v);
        }

        public bool IncludeLevel
        {
            get => _settings?.IncludeLevel ?? true;
            set => EditValue(_settings?.IncludeLevel, value, v => _settings.IncludeLevel = v);
        }

        public bool LevelFirst
        {
            get => _settings?.LevelFirst ?? true;
            set => EditValue(_settings?.LevelFirst, value, v => _settings.LevelFirst = v);
        }

        public string Separator
        {
            get => _settings?.Separator ?? string.Empty;
            set => EditText(_settings?.Separator, value ?? string.Empty, v => _settings.Separator = v);
        }

        // The input is checked by IntRangeRule in the window.
        public int Width
        {
            get => _settings?.Width ?? CategorySettings.MinWidth;
            set => EditValue(_settings?.Width, value, v => _settings.Width = v);
        }

        public string Suffix
        {
            get => _settings?.Suffix ?? string.Empty;
            set => EditText(_settings?.Suffix, value ?? string.Empty, v => _settings.Suffix = v);
        }

        // The settings shown as a number, for example "Д-01-001".
        public string Example
        {
            get
            {
                if (_settings == null || _settings.Width < CategorySettings.MinWidth
                    || _settings.Width > CategorySettings.MaxWidth)
                {
                    return string.Empty;
                }

                return Template.From(_settings).Format(Texts.ExampleLevel, 1);
            }
        }

        // The level and the counter swap places.
        [RelayCommand]
        private void Swap()
        {
            LevelFirst = !LevelFirst;
        }

        public Option<Type> SelectedOrdering
        {
            get => _settings == null ? null : Orderings.Find(option => option.Value == _settings.Ordering.GetType());
            set
            {
                if (_settings == null || value == null || value.Value == _settings.Ordering.GetType())
                {
                    return;
                }

                // Another strategy: a new object with its default parameters.
                _settings.Ordering = (Ordering)Activator.CreateInstance(value.Value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(Ordering));
                Changed?.Invoke();
            }
        }

        // The strategy object. Its editor is chosen by its type.
        public Ordering Ordering => _settings?.Ordering;

        public bool HasError => ErrorText.Length > 0;

        public bool HasWarning => WarningText.Length > 0;

        // Shows the settings of another category.
        public void Attach(CategorySettings settings)
        {
            _settings = settings;
            OnPropertyChanged(string.Empty);
        }

        // Called when a value was changed in a strategy editor.
        public void NotifyEditorChanged()
        {
            Changed?.Invoke();
        }

        public void ShowProblems(Preview preview)
        {
            ErrorText = preview == null ? string.Empty : string.Join("\n", preview.Errors.Select(TextLookup.Problem));
            WarningText = preview?.NumberTooWide == null
                ? string.Empty
                : string.Format(Texts.WidthTooSmall, preview.NumberTooWide);
        }
    }
}
