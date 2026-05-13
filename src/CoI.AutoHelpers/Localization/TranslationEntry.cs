using System;

namespace CoI.AutoHelpers.Localization
{
    public readonly struct TranslationEntry
    {
        public string Key { get; }
        public string SingularText { get; }
        public string? PluralText { get; }

        public bool HasPlural => !string.IsNullOrEmpty(PluralText);

        public TranslationEntry(string key, string singularText)
        {
            Key = ValidateRequired(key, nameof(key));
            SingularText = ValidateRequired(singularText, nameof(singularText));
            PluralText = null;
        }

        public TranslationEntry(string key, string singularText, string pluralText)
        {
            Key = ValidateRequired(key, nameof(key));
            SingularText = ValidateRequired(singularText, nameof(singularText));
            PluralText = ValidateRequired(pluralText, nameof(pluralText));
        }

        private static string ValidateRequired(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value must be non-empty.", paramName);
            }

            return value;
        }
    }
}
