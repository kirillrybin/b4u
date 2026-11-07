using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Blockly.Storage
{
    /// <summary>
    /// A folder for moving programs in and out as plain <c>.json</c> / <c>.xml</c> files, for platforms that have no file dialog.
    /// Exports go in; files the user puts in the folder can be imported.
    /// </summary>
    public sealed class FileExchange
    {
        readonly string directory;

        public FileExchange(string directory) { this.directory = directory; }

        public string Folder => directory;

        static bool IsProgramFile(string fileName)
        {
            string ext = Path.GetExtension(fileName);
            return string.Equals(ext, ".json", StringComparison.OrdinalIgnoreCase) || string.Equals(ext, ".xml", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Writes <paramref name="text"/> to <c>name.json</c> (the name made safe as a file name) and returns the full path.</summary>
        public string Export(string name, string text)
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, ProgramNames.Sanitize(name) + ".json");
            string temp = path + ".tmp";
            File.WriteAllText(temp, text ?? "");
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
            return path;
        }

        /// <summary>The program files in the folder, sorted without regard to case. Empty if the folder does not exist.</summary>
        public IReadOnlyList<string> List()
        {
            if (!Directory.Exists(directory)) return new string[0];
            return Directory.GetFiles(directory).Select(Path.GetFileName).Where(IsProgramFile)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>The text of a file from <see cref="List"/>, or null if it is gone. Only plain file names of the folder are accepted.</summary>
        public string Read(string fileName)
        {
            if (string.IsNullOrEmpty(fileName) || Path.GetFileName(fileName) != fileName || !IsProgramFile(fileName))
                throw new ArgumentException("Not a program file name: " + fileName, nameof(fileName));
            string path = Path.Combine(directory, fileName);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
    }
}
