using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Blockly.UI
{
    /// <summary>
    /// The list of ready-made themes an app (or the Playground's Theme button) can switch between. "Classic" is always the first entry
    /// (no theme, the default look); samples and your own code add the rest with <see cref="Register(string, BlockTheme)"/>.
    /// </summary>
    public static class BlockThemeCatalog
    {
        public const string Classic = "Classic";

        sealed class Entry { public string Name; public BlockTheme Theme; public Func<BlockTheme> Factory; }

        static readonly List<Entry> entries = new List<Entry>();

        /// <summary>Raised when a theme is added to the list.</summary>
        public static event Action Changed;

        /// <summary>The names in the order they were registered, Classic first.</summary>
        public static IReadOnlyList<string> Names
        {
            get
            {
                var names = new List<string> { Classic };
                foreach (var e in entries) names.Add(e.Name);
                return names;
            }
        }

        /// <summary>Adds a theme, or replaces the one with the same name.</summary>
        public static void Register(string name, BlockTheme theme) => Add(new Entry { Name = name, Theme = theme });

        /// <summary>Adds a theme that is built the first time it is chosen (a theme made in code needs no asset and costs nothing until then).</summary>
        public static void Register(string name, Func<BlockTheme> factory) => Add(new Entry { Name = name, Factory = factory });

        static void Add(Entry entry)
        {
            if (string.IsNullOrEmpty(entry.Name) || entry.Name == Classic) return;
            int i = entries.FindIndex(e => e.Name == entry.Name);
            if (i >= 0) entries[i] = entry; else entries.Add(entry);
            Changed?.Invoke();
        }

        public static void Remove(string name)
        {
            if (entries.RemoveAll(e => e.Name == name) > 0) Changed?.Invoke();
        }

        public static void Clear()
        {
            if (entries.Count == 0) return;
            entries.Clear();
            Changed?.Invoke();
        }

        /// <summary>The theme called <paramref name="name"/>; null for Classic or an unknown name.</summary>
        public static BlockTheme Get(string name)
        {
            var e = entries.Find(x => x.Name == name);
            if (e == null) return null;
            if (e.Theme == null && e.Factory != null) e.Theme = e.Factory();
            return e.Theme;
        }

        /// <summary>Applies the theme called <paramref name="name"/> (Classic restores the default look). False for an unknown name.</summary>
        public static bool Select(string name)
        {
            if (name == Classic) { BlockStyle.Theme = null; return true; }
            var theme = Get(name);
            if (theme == null) return false;
            BlockStyle.Theme = theme;
            return true;
        }

        /// <summary>The PlayerPrefs key that keeps the name of the theme the user chose last.</summary>
        public const string PrefsKey = "Blockly.Theme";

        /// <summary>Writes the name of the theme in use, so <see cref="RestoreSaved"/> can bring it back in the next session.</summary>
        public static void Remember()
        {
            try { PlayerPrefs.SetString(PrefsKey, CurrentName); PlayerPrefs.Save(); }
            catch (System.Exception e) when (e is System.InvalidOperationException || e is System.UnauthorizedAccessException) { }   // the choice is a convenience, never a failure
        }

        /// <summary>The theme the user chose in an earlier session; null when there is none or it is no longer in the list.</summary>
        public static string SavedName
        {
            get
            {
                string name = PlayerPrefs.GetString(PrefsKey, null);
                return !string.IsNullOrEmpty(name) && Names.Contains(name) ? name : null;
            }
        }

        /// <summary>Applies the theme chosen in the last session. Without a saved choice nothing changes (a skin's own theme stays). True if one was applied.</summary>
        public static bool RestoreSaved()
        {
            string name = SavedName;
            return name != null && Select(name);
        }

        /// <summary>Forgets the saved choice.</summary>
        public static void ForgetSaved() => PlayerPrefs.DeleteKey(PrefsKey);

        /// <summary>The name of the theme in use: Classic with no theme, the registered name of the current one, or "Custom" for any other.</summary>
        public static string CurrentName
        {
            get
            {
                if (BlockStyle.IsClassic) return Classic;
                var current = BlockStyle.Theme;
                var e = entries.Find(x => x.Theme == current);
                return e != null ? e.Name : "Custom";
            }
        }
    }
}
