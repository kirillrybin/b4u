using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Blockly.Storage
{
    /// <summary>Where saved programs live: a flat list of named JSON texts.</summary>
    public interface IProgramStore
    {
        /// <summary>Names of the saved programs, sorted without regard to case.</summary>
        IReadOnlyList<string> List();
        bool Exists(string name);
        /// <summary>The saved text, or null if there is no such program.</summary>
        string Load(string name);
        /// <summary>Stores a program under <see cref="ProgramNames.Sanitize"/>d <paramref name="name"/> (replacing an older one) and returns the name used.</summary>
        string Save(string name, string text);
        bool Delete(string name);
    }

    public static class ProgramNames
    {
        public const int MaxLength = 80;

        /// <summary>
        /// A name that is safe as a file name on every platform: separators and reserved characters become '_', runs of
        /// whitespace collapse, leading dots and trailing dots/spaces go, and the length is capped. Never empty.
        /// </summary>
        public static string Sanitize(string wanted)
        {
            var sb = new StringBuilder();
            bool space = false;
            foreach (char c in wanted ?? "")
            {
                if (char.IsWhiteSpace(c)) { space = sb.Length > 0; continue; }
                if (space) { sb.Append(' '); space = false; }
                sb.Append(c < ' ' || "/\\:*?\"<>|".IndexOf(c) >= 0 ? '_' : c);
            }
            string name = sb.ToString().TrimStart('.').TrimEnd('.', ' ');
            if (name.Length > MaxLength) name = name.Substring(0, MaxLength).TrimEnd('.', ' ');
            return name.Length == 0 ? "untitled" : name;
        }

        /// <summary>Error text for something the user typed as a name, or null when it can be used (it may still be changed by Sanitize).</summary>
        public static string Validate(string typed)
            => string.IsNullOrWhiteSpace(typed) ? "Enter a name." : null;
    }

    /// <summary>Keeps programs in memory (tests, platforms without a writable disk).</summary>
    public sealed class MemoryProgramStore : IProgramStore
    {
        readonly Dictionary<string, string> items = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<string> List() => items.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
        public bool Exists(string name) => items.ContainsKey(ProgramNames.Sanitize(name));
        public string Load(string name) => items.TryGetValue(ProgramNames.Sanitize(name), out var t) ? t : null;
        public string Save(string name, string text)
        {
            string key = ProgramNames.Sanitize(name);
            var existing = items.Keys.FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
            if (existing != null) items.Remove(existing);
            items[key] = text ?? "";
            return key;
        }
        public bool Delete(string name) => items.Remove(ProgramNames.Sanitize(name));
    }

    /// <summary>One <c>name.json</c> file per program in a folder, written through a temporary file so a crash never leaves half a program.</summary>
    public sealed class FileProgramStore : IProgramStore
    {
        readonly string directory;

        public FileProgramStore(string directory) { this.directory = directory; }

        public string Folder => directory;

        string PathOf(string name) => Path.Combine(directory, ProgramNames.Sanitize(name) + ".json");

        public IReadOnlyList<string> List()
        {
            if (!Directory.Exists(directory)) return new List<string>();
            return Directory.GetFiles(directory, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public bool Exists(string name) => File.Exists(PathOf(name));

        public string Load(string name)
        {
            string path = PathOf(name);
            return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
        }

        public string Save(string name, string text)
        {
            Directory.CreateDirectory(directory);
            string safe = ProgramNames.Sanitize(name);
            string path = PathOf(safe);
            string temp = path + ".tmp";
            File.WriteAllText(temp, text ?? "", new UTF8Encoding(false));
            if (File.Exists(path)) File.Delete(path);                // File.Replace is not available on every platform
            File.Move(temp, path);
            return safe;
        }

        public bool Delete(string name)
        {
            string path = PathOf(name);
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }
    }
}
