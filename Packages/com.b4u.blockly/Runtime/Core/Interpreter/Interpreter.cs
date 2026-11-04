using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace Blockly
{
    public delegate object ExpressionHandler(Interpreter it, Block block);
    public delegate IEnumerator StatementHandler(Interpreter it, Block block);

    /// <summary>Maps block types to C# behaviour. Register custom blocks here.</summary>
    public sealed class HandlerRegistry
    {
        readonly Dictionary<string, ExpressionHandler> expressions = new Dictionary<string, ExpressionHandler>();
        readonly Dictionary<string, StatementHandler> statements = new Dictionary<string, StatementHandler>();

        static HandlerRegistry standard;
        /// <summary>Shared registry with all built-in Blockly blocks. Safe to add custom blocks to.</summary>
        public static HandlerRegistry Standard
        {
            get
            {
                if (standard == null) { standard = new HandlerRegistry(); StandardHandlers.RegisterAll(standard); }
                return standard;
            }
        }

        public void Expression(string type, ExpressionHandler h) => expressions[type] = h;
        public void Statement(string type, StatementHandler h) => statements[type] = h;

        /// <summary>A statement that runs to completion in a single step.</summary>
        public void Simple(string type, Action<Interpreter, Block> action)
            => statements[type] = (it, b) => { action(it, b); return EmptyEnumerator.Instance; };

        public bool TryExpression(string type, out ExpressionHandler h) => expressions.TryGetValue(type, out h);
        public bool TryStatement(string type, out StatementHandler h) => statements.TryGetValue(type, out h);
    }

    sealed class EmptyEnumerator : IEnumerator
    {
        public static readonly EmptyEnumerator Instance = new EmptyEnumerator();
        public bool MoveNext() => false;
        public void Reset() { }
        public object Current => null;
    }

    public enum Signal { None, Break, Continue, Return }

    /// <summary>
    /// Step-based tree-walking interpreter. Statements run as iterators that yield between steps, so
    /// execution can be paused, single-stepped and time-sliced across frames on every platform
    /// (no threads, no JIT, WebGL-safe).
    /// </summary>
    public sealed class Interpreter
    {
        public readonly Workspace Workspace;
        public readonly HandlerRegistry Handlers;
        public Action<string> Print = _ => { };
        public Random Random = new Random();

        /// <summary>Raised just before a statement block executes (for highlighting).</summary>
        public event Action<Block> BlockExecuting;

        readonly Dictionary<string, object> variables = new Dictionary<string, object>();
        readonly Dictionary<string, Block> procedures = new Dictionary<string, Block>();
        IEnumerator main;
        int callDepth, syncDepth;
        long syncSteps;

        /// <summary>Deeper recursion reports "Maximum call stack size exceeded" instead of overflowing the native stack.</summary>
        public int MaxCallDepth = 200;
        /// <summary>
        /// A procedure called from inside an expression runs to completion without yielding; this caps its steps
        /// so a runaway loop cannot freeze a frame. Statement-position calls are fully time-sliced.
        /// </summary>
        public long MaxSyncSteps = 1_000_000;

        public Signal Pending;
        public object ReturnValue;

        public bool IsRunning => main != null;
        public string Error { get; private set; }
        public long StepCount { get; private set; }

        public Interpreter(Workspace workspace, HandlerRegistry handlers = null)
        {
            Workspace = workspace;
            Handlers = handlers ?? HandlerRegistry.Standard;
        }

        // ---------- lifecycle ----------

        public void Start()
        {
            variables.Clear();
            Error = null;
            Pending = Signal.None;
            StepCount = 0;
            callDepth = syncDepth = 0;
            procedures.Clear();
            foreach (var top in Workspace.TopBlocks)
                if (ProcedureInfo.IsDefinition(top) && !top.Disabled)
                    procedures[top.GetFieldValue("NAME") ?? ""] = top;
            main = RunProgram();
        }

        public void Stop() { main = null; }

        /// <summary>Runs one step. Returns true while the program has more to do.</summary>
        public bool Step()
        {
            if (main == null) return false;
            try
            {
                if (main.MoveNext()) { StepCount++; return true; }
            }
            catch (Exception e)
            {
                Error = e.Message;
            }
            main = null;
            return false;
        }

        /// <summary>Runs steps until the time budget is spent. Returns true if still running.</summary>
        public bool RunFor(double milliseconds)
        {
            var sw = Stopwatch.StartNew();
            while (IsRunning)
            {
                if (!Step()) return false;
                if (sw.Elapsed.TotalMilliseconds >= milliseconds) break;
            }
            return IsRunning;
        }

        /// <summary>Runs to completion or until the step limit (guards infinite loops in tests/tools).</summary>
        public bool RunToCompletion(long maxSteps = 10_000_000)
        {
            while (IsRunning)
            {
                if (StepCount >= maxSteps) { Error = "Step limit exceeded"; main = null; return false; }
                Step();
            }
            return Error == null;
        }

        IEnumerator RunProgram()
        {
            var tops = new List<Block>(Workspace.TopBlocks);
            // Blockly generates code for top blocks ordered top-to-bottom (stable for ties).
            var indexed = new List<KeyValuePair<int, Block>>();
            for (int i = 0; i < tops.Count; i++) indexed.Add(new KeyValuePair<int, Block>(i, tops[i]));
            indexed.Sort((a, b) =>
            {
                int c = a.Value.Y.CompareTo(b.Value.Y);
                return c != 0 ? c : a.Key.CompareTo(b.Key);
            });
            foreach (var kv in indexed)
            {
                var block = kv.Value;
                if (block.Output != null || block.Previous == null || block.Disabled) continue; // orphan expression / not a statement chain
                var chain = RunChain(block);
                while (chain.MoveNext()) yield return null;
                Pending = Signal.None;
            }
        }

        // ---------- statements ----------

        public IEnumerator RunChain(Block first)
        {
            for (var b = first; b != null; b = b.NextBlock)
            {
                if (b.Disabled) continue;
                BlockExecuting?.Invoke(b);
                if (!Handlers.TryStatement(b.Type, out var handler))
                    throw new NotSupportedException("No statement handler for block type '" + b.Type + "'");
                var it = handler(this, b);
                while (it.MoveNext()) yield return null;
                if (Pending != Signal.None) yield break;
                yield return null;
            }
        }

        // ---------- expressions ----------

        public object Eval(Block b)
        {
            if (b == null) return Undefined.Value;
            if (!Handlers.TryExpression(b.Type, out var handler))
                throw new NotSupportedException("No expression handler for block type '" + b.Type + "'");
            return handler(this, b);
        }

        /// <summary>Evaluates a value input, falling back to its shadow block and then to <paramref name="def"/>.</summary>
        public object EvalInput(Block b, string name, object def = null)
        {
            var input = b.GetInput(name);
            var c = input?.Connection;
            if (c == null) return def;
            var target = c.Target?.Owner ?? c.Shadow;
            return target == null ? def : Eval(target);
        }

        public bool HasInputBlock(Block b, string name)
        {
            var c = b.GetInput(name)?.Connection;
            return c != null && (c.Target != null || c.Shadow != null);
        }

        // ---------- procedures ----------

        sealed class CallFrame
        {
            public Block Definition;
            public string[] Ids;
            public object[] Saved;
            public bool[] Had;
        }

        /// <summary>Evaluates arguments in the caller's scope, then binds parameters (JS function-parameter semantics).</summary>
        CallFrame EnterCall(Block call)
        {
            string name = ProcedureInfo.Name(call) ?? "";
            if (!procedures.TryGetValue(name, out var def))
                throw new InvalidOperationException(name + " is not defined");
            if (callDepth >= MaxCallDepth)
                throw new InvalidOperationException("Maximum call stack size exceeded");

            var ps = ProcedureInfo.Params(def);
            var args = new object[ps.Count];
            for (int i = 0; i < args.Length; i++) args[i] = EvalInput(call, "ARG" + i, Undefined.Value);

            var f = new CallFrame { Definition = def, Ids = new string[ps.Count], Saved = new object[ps.Count], Had = new bool[ps.Count] };
            for (int i = 0; i < args.Length; i++)
            {
                f.Ids[i] = ps[i].Id ?? Workspace.GetVariableByName(ps[i].Name)?.Id ?? ps[i].Name;
                f.Had[i] = variables.TryGetValue(f.Ids[i], out f.Saved[i]);
                variables[f.Ids[i]] = args[i];
            }
            callDepth++;
            return f;
        }

        void ExitCall(CallFrame f)
        {
            callDepth--;
            for (int i = f.Ids.Length - 1; i >= 0; i--)
            {
                if (f.Had[i]) variables[f.Ids[i]] = f.Saved[i];
                else variables.Remove(f.Ids[i]);
            }
        }

        /// <summary>Value the call produced once its body finished; consumes a pending Return.</summary>
        object CallResult(CallFrame f)
        {
            object result = Undefined.Value;
            if (Pending == Signal.Return) result = ReturnValue;
            else if (f.Definition.GetInput("RETURN") != null) result = EvalInput(f.Definition, "RETURN", Undefined.Value);
            Pending = Signal.None;
            ReturnValue = Undefined.Value;
            return result;
        }

        /// <summary>Procedure call used as a statement: time-sliced like any other block.</summary>
        public IEnumerator CallStatement(Block call)
        {
            var f = EnterCall(call);
            try
            {
                var body = RunChain(f.Definition.GetInputTargetBlock("STACK"));
                while (body.MoveNext()) yield return null;
                CallResult(f);
            }
            finally { ExitCall(f); }
        }

        /// <summary>Procedure call used inside an expression: runs to completion (bounded by MaxSyncSteps).</summary>
        public object CallValue(Block call)
        {
            bool outermost = syncDepth++ == 0;
            if (outermost) syncSteps = 0;
            var f = EnterCall(call);
            try
            {
                var body = RunChain(f.Definition.GetInputTargetBlock("STACK"));
                while (body.MoveNext())
                    if (++syncSteps > MaxSyncSteps)
                        throw new InvalidOperationException("Procedure call inside an expression ran too long; call it as a statement instead");
                return CallResult(f);
            }
            finally { ExitCall(f); syncDepth--; }
        }

        // ---------- variables ----------

        public object GetVariable(string id)
            => id != null && variables.TryGetValue(id, out var v) ? v : Undefined.Value;

        public void SetVariable(string id, object value) => variables[id] = value;

        public object GetVariableByName(string name)
        {
            var v = Workspace.GetVariableByName(name);
            return v == null ? Undefined.Value : GetVariable(v.Id);
        }
    }
}
