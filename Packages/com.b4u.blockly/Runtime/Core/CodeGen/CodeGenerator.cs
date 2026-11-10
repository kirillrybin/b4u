using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Blockly
{
    /// <summary>A piece of generated code and how tightly it binds (a lower <see cref="Order"/> binds tighter), so the caller knows when it needs brackets.</summary>
    public readonly struct Code
    {
        public readonly string Text;
        public readonly double Order;
        public Code(string text, double order) { Text = text; Order = order; }
    }

    /// <summary>Writes the code of an expression block.</summary>
    public delegate Code ValueGenerator(CodeGenerator g, Block block);
    /// <summary>Writes the code of a statement block: whole lines, each ending with a newline.</summary>
    public delegate string StatementGenerator(CodeGenerator g, Block block);

    /// <summary>Settings of one export.</summary>
    public sealed class CodeOptions
    {
        /// <summary>What the "print" block calls (default: console.log for JavaScript, print for Python). It takes one argument.</summary>
        public string Print;
    }

    /// <summary>
    /// A target language of the exporter: how each block type becomes text. The ready-made ones are <see cref="JavaScript"/> and
    /// (added by their own files) others; add your own blocks with <see cref="Value"/> and <see cref="Statement"/>, the same way
    /// <see cref="HandlerRegistry"/> takes custom behaviour for the interpreter.
    /// </summary>
    public sealed class CodeLanguage
    {
        /// <summary>The order of code that needs no brackets (a name, a literal) and of code that always does not matter (the top level).</summary>
        public const double Atomic = 0, None = 99;

        public readonly string Name;
        /// <summary>What one level of nesting adds to a line.</summary>
        public string Indent = "  ";
        public string DefaultPrint = "console.log";
        public readonly HashSet<string> Reserved = new HashSet<string>();
        /// <summary>Puts the variable line, the helper functions and the procedures in front of the main code.</summary>
        public Func<CodeGenerator, IList<string>, IList<string>, string, string> Assemble;
        /// <summary>Turns a string into a literal of the language.</summary>
        public Func<string, string> Quote;

        readonly Dictionary<string, ValueGenerator> values = new Dictionary<string, ValueGenerator>();
        readonly Dictionary<string, StatementGenerator> statements = new Dictionary<string, StatementGenerator>();

        public CodeLanguage(string name) { Name = name; }

        /// <summary>Sets the code of an expression block type; null takes the generator away.</summary>
        public void Value(string type, ValueGenerator g) { if (g == null) values.Remove(type); else values[type] = g; }
        /// <summary>Sets the code of a statement block type; null takes the generator away.</summary>
        public void Statement(string type, StatementGenerator g) { if (g == null) statements.Remove(type); else statements[type] = g; }
        public bool TryValue(string type, out ValueGenerator g) => values.TryGetValue(type, out g);
        public bool TryStatement(string type, out StatementGenerator g) => statements.TryGetValue(type, out g);

        static readonly List<CodeLanguage> all = new List<CodeLanguage>();
        /// <summary>The languages the Playground offers, in order.</summary>
        public static IReadOnlyList<CodeLanguage> All { get { Ensure(); return all; } }

        static CodeLanguage javaScript;
        /// <summary>JavaScript, shared; safe to add custom blocks to.</summary>
        public static CodeLanguage JavaScript { get { Ensure(); return javaScript; } }

        static void Ensure()
        {
            if (javaScript != null) return;
            javaScript = JavaScriptGenerators.Create();
            all.Add(javaScript);
        }

        /// <summary>Adds a language to <see cref="All"/>.</summary>
        public static void Register(CodeLanguage language)
        {
            Ensure();
            int i = all.FindIndex(l => l.Name == language.Name);
            if (i >= 0) all[i] = language; else all.Add(language);
        }

        /// <summary>Generates the program of the workspace. Throws <see cref="NotSupportedException"/> for a block without a generator.</summary>
        public string Generate(Workspace workspace, CodeOptions options = null)
            => new CodeGenerator(this, workspace, options ?? new CodeOptions()).Run();
    }

    /// <summary>One export in progress: hands out names, collects helper functions and turns blocks into text.</summary>
    public sealed class CodeGenerator
    {
        public readonly CodeLanguage Language;
        public readonly Workspace Workspace;
        public readonly CodeOptions Options;

        readonly HashSet<string> usedNames = new HashSet<string>();
        readonly Dictionary<string, string> variableNames = new Dictionary<string, string>();
        readonly List<string> variableOrder = new List<string>();
        readonly Dictionary<string, string> procedureNames = new Dictionary<string, string>();
        readonly Dictionary<string, string> helperNames = new Dictionary<string, string>();
        readonly List<string> definitions = new List<string>();

        internal CodeGenerator(CodeLanguage language, Workspace workspace, CodeOptions options)
        {
            Language = language; Workspace = workspace; Options = options;
            foreach (var r in language.Reserved) usedNames.Add(r);
        }

        /// <summary>The function the print block calls.</summary>
        public string Print => string.IsNullOrEmpty(Options.Print) ? Language.DefaultPrint : Options.Print;

        // ------------------------------------------------------------ names
        static string Legal(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in name ?? "") sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
            string s = sb.ToString();
            if (s.Length == 0) return "unnamed";
            return char.IsDigit(s[0]) ? "my_" + s : s;
        }

        /// <summary>A name nothing else uses: <paramref name="baseName"/>, or with a number after it (count, count2, count3).</summary>
        public string NewName(string baseName)
        {
            string b = Legal(baseName), n = b;
            for (int i = 2; usedNames.Contains(n); i++) n = b + i;
            usedNames.Add(n);
            return n;
        }

        /// <summary>The name of a variable, by its id (a name without a variable of that id is used as it is).</summary>
        public string VariableName(string idOrName)
        {
            string key = idOrName ?? "";
            if (!variableNames.TryGetValue(key, out var name))
            {
                name = NewName(Workspace.GetVariableById(key)?.Name ?? key);
                variableNames[key] = name;
            }
            if (!variableOrder.Contains(key)) variableOrder.Add(key);
            return name;
        }

        /// <summary>The name a procedure gets in the code.</summary>
        public string ProcedureName(string procedure)
        {
            string key = procedure ?? "";
            if (!procedureNames.TryGetValue(key, out var name)) procedureNames[key] = name = NewName(key);
            return name;
        }

        /// <summary>The names of a procedure's parameters, the same way a variable block names them.</summary>
        public List<string> ParameterNames(Block definition)
        {
            var names = new List<string>();
            foreach (var p in ProcedureInfo.Params(definition))
                names.Add(VariableName(p.Id ?? Workspace.GetVariableByName(p.Name)?.Id ?? p.Name));
            return names;
        }

        // ------------------------------------------------------------ helpers and definitions
        /// <summary>
        /// Adds a helper function once and returns its name. <paramref name="template"/> is the whole function with %NAME% where its name goes.
        /// </summary>
        public string Helper(string baseName, string template)
        {
            if (helperNames.TryGetValue(baseName, out var name)) return name;
            name = NewName(baseName);
            helperNames[baseName] = name;
            definitions.Add(template.Replace("\r\n", "\n").Replace("%NAME%", name));
            return name;
        }

        /// <summary>Adds a block of code that goes in front of the program (a procedure).</summary>
        public void AddDefinition(string code) => definitions.Add(code);

        // ------------------------------------------------------------ blocks
        /// <summary>The code of the block in a value input, in brackets when it binds looser than <paramref name="order"/>. Empty when the input is empty.</summary>
        public string ValueToCode(Block block, string input, double order)
        {
            var c = block.GetInput(input)?.Connection;
            var target = c?.Target?.Owner ?? c?.Shadow;
            if (target == null) return "";
            if (!Language.TryValue(target.Type, out var gen))
                throw new NotSupportedException(Loc.T("No code generator for block type '{0}'", target.Type));
            var code = gen(this, target);
            bool wrap = order <= code.Order && !(order == code.Order && (order == CodeLanguage.Atomic || order == CodeLanguage.None));
            return wrap ? "(" + code.Text + ")" : code.Text;
        }

        /// <summary>The statements in a statement input, one indent deeper.</summary>
        public string StatementToCode(Block block, string input)
            => Prefix(ChainToCode(block.GetInputTargetBlock(input)), Language.Indent);

        /// <summary>The code of a block and of the ones after it.</summary>
        public string ChainToCode(Block first)
        {
            var sb = new StringBuilder();
            for (var b = first; b != null; b = b.NextBlock)
            {
                if (b.Disabled) continue;
                if (!Language.TryStatement(b.Type, out var gen))
                    throw new NotSupportedException(Loc.T("No code generator for block type '{0}'", b.Type));
                sb.Append(gen(this, b));
            }
            return sb.ToString();
        }

        /// <summary>Puts <paramref name="prefix"/> in front of every line that is not empty.</summary>
        public static string Prefix(string text, string prefix)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++) if (lines[i].Length > 0) lines[i] = prefix + lines[i];
            return string.Join("\n", lines);
        }

        /// <summary>True for a number literal such as 5, -2.5 or 1e3.</summary>
        public static bool IsNumber(string s) => Regex.IsMatch(s, @"^\s*-?\d+(\.\d+)?([eE][+-]?\d+)?\s*$");

        // ------------------------------------------------------------ the program
        internal string Run()
        {
            foreach (var v in Workspace.Variables) { variableNames[v.Id] = NewName(v.Name); }
            foreach (var top in Workspace.TopBlocks)
                if (ProcedureInfo.IsDefinition(top) && !top.Disabled) ProcedureName(ProcedureInfo.Name(top));

            // top to bottom like the interpreter (stable for ties)
            var order = new List<KeyValuePair<int, Block>>();
            for (int i = 0; i < Workspace.TopBlocks.Count; i++) order.Add(new KeyValuePair<int, Block>(i, Workspace.TopBlocks[i]));
            order.Sort((a, b) =>
            {
                int c = a.Value.Y.CompareTo(b.Value.Y);
                return c != 0 ? c : a.Key.CompareTo(b.Key);
            });

            var code = new StringBuilder();
            foreach (var kv in order)
            {
                var b = kv.Value;
                if (b.Disabled) continue;
                if (ProcedureInfo.IsDefinition(b)) { ChainToCode(b); continue; }          // adds its definition, writes no code
                if (b.Output != null || b.Previous == null) continue;                        // an expression on its own, or not a statement chain
                code.Append(ChainToCode(b));
            }

            // declared in workspace order, then any other name met on the way
            var declared = new List<string>();
            foreach (var v in Workspace.Variables) if (variableOrder.Contains(v.Id)) declared.Add(variableNames[v.Id]);
            foreach (var key in variableOrder) if (Workspace.GetVariableById(key) == null) declared.Add(variableNames[key]);
            return Language.Assemble(this, declared, definitions, code.ToString());
        }
    }
}
