using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using RevitNumbering.Core.Numbering;
using RevitNumbering.Core.Orderings;
using RevitNumbering.Core.Settings;
using RevitNumbering.Core.Templates;
using RevitNumbering.UI.Resources;

namespace RevitNumbering.UI.ViewModels
{
    /// <summary>
    /// The settings panel: template, level code and ordering of the current category.
    /// Edits the CategorySettings object of the session directly and reports every change.
    /// </summary>
    public sealed class CategorySettingsViewModel : ObservableObject
    {
        private CategorySettings _settings;
        private string _errorText = string.Empty;
        private string _warningText = string.Empty;

        public CategorySettingsViewModel()
        {
            LevelParts = Enum.GetValues(typeof(LevelCodePart)).Cast<LevelCodePart>()
                .Select(part => new Option<LevelCodePart>(part, TextLookup.Get("LevelCodePart_" + part)))
                .ToList();

            // Every registered strategy: the JsonDerivedType lines of the base class.
            Orderings = typeof(Ordering).GetCustomAttributes(typeof(JsonDerivedTypeAttribute), false)
                .Cast<JsonDerivedTypeAttribute>()
                .Select(attribute => new Option<Type>(attribute.DerivedType,
                    TextLookup.Get("Ordering_" + attribute.TypeDiscriminator)))
                .ToList();
        }

        /// <summary>A setting changed: numbers must be calculated again.</summary>
        public event Action Changed;

        public List<Option<LevelCodePart>> LevelParts { get; }

        public List<Option<Type>> Orderings { get; }

        public string Template
        {
            get => _settings?.Template ?? string.Empty;
            set
            {
                if (_settings == null || value == _settings.Template)
                {
                    return;
                }

                _settings.Template = value;
                OnPropertyChanged();
                Changed?.Invoke();
            }
        }

        public Option<LevelCodePart> SelectedLevelPart
        {
            get => _settings == null ? null : LevelParts.Find(option => option.Value == _settings.LevelCode.Part);
            set
            {
                if (_settings == null || value == null || value.Value == _settings.LevelCode.Part)
                {
                    return;
                }

                _settings.LevelCode.Part = value.Value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsWordNumberVisible));
                Changed?.Invoke();
            }
        }

        public int WordNumber
        {
            get => _settings?.LevelCode.WordNumber ?? 1;
            set
            {
                if (_settings == null || value < 1 || value == _settings.LevelCode.WordNumber)
                {
                    return;
                }

                _settings.LevelCode.WordNumber = value;
                OnPropertyChanged();
                Changed?.Invoke();
            }
        }

        public bool IsWordNumberVisible => _settings?.LevelCode.Part == LevelCodePart.Word;

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

        /// <summary>The strategy object; its editor is chosen by its type.</summary>
        public Ordering Ordering => _settings?.Ordering;

        /// <summary>Errors that block Apply.</summary>
        public string ErrorText
        {
            get => _errorText;
            private set
            {
                if (SetProperty(ref _errorText, value))
                {
                    OnPropertyChanged(nameof(HasError));
                }
            }
        }

        public bool HasError => _errorText.Length > 0;

        public string WarningText
        {
            get => _warningText;
            private set
            {
                if (SetProperty(ref _warningText, value))
                {
                    OnPropertyChanged(nameof(HasWarning));
                }
            }
        }

        public bool HasWarning => _warningText.Length > 0;

        /// <summary>Shows the settings of another category (or of an imported file).</summary>
        public void Attach(CategorySettings settings)
        {
            _settings = settings;
            OnPropertyChanged(string.Empty);
        }

        /// <summary>A value was changed in a strategy editor.</summary>
        public void NotifyEditorChanged()
        {
            Changed?.Invoke();
        }

        public void ShowProblems(Preview preview)
        {
            ErrorText = preview == null ? string.Empty : string.Join("\n", preview.Errors.Select(TextLookup.Problem));
            WarningText = preview == null ? string.Empty : string.Join("\n", preview.Warnings.Select(TextLookup.Problem));
        }
    }
}
