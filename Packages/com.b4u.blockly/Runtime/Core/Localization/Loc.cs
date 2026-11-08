using System;
using System.Collections.Generic;
using System.Linq;

namespace Blockly
{
    /// <summary>
    /// Translation of the texts the package shows: buttons, menus, dialogs, messages, block labels and category names.
    /// The English text itself is the key, so code reads naturally and a missing translation shows the English text.
    /// English and Russian come with the package; <see cref="Register"/> adds a language (or more texts to one).
    /// </summary>
    public static class Loc
    {
        public const string English = "en";

        static readonly Dictionary<string, Dictionary<string, string>> tables = new Dictionary<string, Dictionary<string, string>>();
        static readonly Dictionary<string, string> names = new Dictionary<string, string> { { English, "English" } };
        static readonly List<string> order = new List<string> { English };
        static Dictionary<string, string> current;
        static string language = English;

        /// <summary>Raised after <see cref="Language"/> changed; views re-read their texts.</summary>
        public static event Action Changed;

        static Loc() { Russian.Register(); }

        /// <summary>The language code in use ("en", "ru"...). Setting an unknown code is ignored.</summary>
        public static string Language
        {
            get => language;
            set
            {
                if (string.IsNullOrEmpty(value) || value == language || !order.Contains(value)) return;
                language = value;
                current = tables.TryGetValue(value, out var t) ? t : null;
                Changed?.Invoke();
            }
        }

        /// <summary>The codes of the languages, English first.</summary>
        public static IReadOnlyList<string> Languages => order;

        /// <summary>The name of a language in that language ("Русский"), for a language menu.</summary>
        public static string NameOf(string code) => names.TryGetValue(code, out var n) ? n : code;

        /// <summary>Adds the texts of a language: English text to its translation. Texts already there are replaced.</summary>
        public static void Register(string code, string displayName, IDictionary<string, string> table)
        {
            if (string.IsNullOrEmpty(code) || code == English || table == null) return;
            if (!tables.TryGetValue(code, out var t)) { tables[code] = t = new Dictionary<string, string>(); order.Add(code); }
            foreach (var kv in table) t[kv.Key] = kv.Value;
            if (!string.IsNullOrEmpty(displayName)) names[code] = displayName;
            if (code == language) current = t;
        }

        /// <summary>The text in the current language; the English text itself when there is no translation.</summary>
        public static string T(string text)
        {
            if (string.IsNullOrEmpty(text) || current == null) return text;
            return current.TryGetValue(text, out var translated) ? translated : text;
        }

        /// <summary>A translated text with {0}, {1}... filled in.</summary>
        public static string T(string text, params object[] args) => string.Format(T(text), args);

        /// <summary>The first of the given language codes that is available, or English: for choosing a language from the device's.</summary>
        public static string Pick(params string[] codes) => codes.FirstOrDefault(order.Contains) ?? English;
    }
}
