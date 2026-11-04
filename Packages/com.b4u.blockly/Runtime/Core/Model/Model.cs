using System;
using System.Collections.Generic;

namespace Blockly
{
    public enum ConnectionType { PreviousStatement, NextStatement, OutputValue, InputValue }

    public enum InputKind { Value, Statement, Dummy }

    public sealed class Field
    {
        public string Name;          // null for non-serialized labels
        public ArgSpec Spec;
        public string Value;
        public bool IsLabel => Spec.Kind == ArgKind.Label;
    }

    public sealed class Connection
    {
        public readonly ConnectionType Type;
        public readonly Block Owner;
        public readonly Input Input;           // set for InputValue / statement-input connections
        public string[] Check;
        public Connection Target;              // the connection on the other side
        public Block Shadow;                   // default block shown when empty (never executed as parent)

        public Connection(Block owner, ConnectionType type, Input input = null, string[] check = null)
        {
            Owner = owner; Type = type; Input = input; Check = check;
        }

        /// <summary>Parent side: InputValue and NextStatement. Child side: OutputValue and PreviousStatement.</summary>
        public bool IsParentSide => Type == ConnectionType.InputValue || Type == ConnectionType.NextStatement;
        public Block TargetBlock => Target?.Owner;

        public string Describe()
            => "'" + Owner.Type + "'." + (Input?.Name ?? Type.ToString()) + "[" + (Check == null ? "any" : string.Join("|", Check)) + "]";

        public static ConnectionType Opposite(ConnectionType t)
        {
            switch (t)
            {
                case ConnectionType.InputValue: return ConnectionType.OutputValue;
                case ConnectionType.OutputValue: return ConnectionType.InputValue;
                case ConnectionType.NextStatement: return ConnectionType.PreviousStatement;
                default: return ConnectionType.NextStatement;
            }
        }

        public bool CheckCompatible(Connection other)
        {
            if (other == null || Opposite(Type) != other.Type) return false;
            if (Owner == other.Owner) return false;
            if (Check == null || other.Check == null) return true;
            foreach (var a in Check)
                foreach (var b in other.Check)
                    if (a == b) return true;
            return false;
        }

        /// <summary>Connects this (parent side) to a child-side connection. Disconnects previous links.</summary>
        public void Connect(Connection other)
        {
            if (!CheckCompatible(other))
                throw new InvalidOperationException("Incompatible connection: " + Describe() + " -> " + (other?.Describe() ?? "null"));
            var parent = IsParentSide ? this : other;
            var child = IsParentSide ? other : this;
            if (child.Owner.IsAncestorOrSelfOf(parent.Owner)) throw new InvalidOperationException("Cycle");
            parent.Disconnect();
            child.Disconnect();
            parent.Target = child;
            child.Target = parent;
        }

        public void Disconnect()
        {
            if (Target == null) return;
            var t = Target;
            Target = null;
            t.Target = null;
        }
    }

    public sealed class Input
    {
        public string Name;
        public InputKind Kind;
        public List<Field> Fields = new List<Field>();
        public Connection Connection;          // null for dummy inputs
        public string Align;
    }

    public sealed class Block
    {
        public string Id;
        public string Type;
        public BlockDefinition Definition;
        public double X, Y;
        public bool InputsInline, Collapsed, Disabled, IsShadow;
        /// <summary>For a shadow block: the connection it is the default value of (shadows are not "connected", so Parent is null).</summary>
        public Connection ShadowOf;
        public Dictionary<string, object> ExtraState;   // mutator state (kept verbatim for round-trip)
        public List<Input> Inputs = new List<Input>();
        public Connection Previous, Next, Output;

        public Block Parent
            => Output?.Target?.Owner ?? Previous?.Target?.Owner;

        public Block NextBlock => Next?.Target?.Owner;

        public Block Root { get { var b = this; while (b.Parent != null) b = b.Parent; return b; } }

        /// <summary>True if this block is <paramref name="other"/> or one of its ancestors.</summary>
        public bool IsAncestorOrSelfOf(Block other)
        {
            for (var b = other; b != null; b = b.Parent) if (b == this) return true;
            return false;
        }

        public Input GetInput(string name)
        {
            foreach (var i in Inputs) if (i.Name == name) return i;
            return null;
        }

        public Block GetInputTargetBlock(string name) => GetInput(name)?.Connection?.Target?.Owner;

        public Field GetField(string name)
        {
            foreach (var i in Inputs) foreach (var f in i.Fields) if (f.Name == name) return f;
            return null;
        }

        public string GetFieldValue(string name) => GetField(name)?.Value;

        public void SetFieldValue(string name, string value)
        {
            var f = GetField(name) ?? throw new KeyNotFoundException($"Block '{Type}' has no field '{name}'");
            f.Value = value;
        }

        public IEnumerable<Field> AllFields() { foreach (var i in Inputs) foreach (var f in i.Fields) yield return f; }

        /// <summary>All blocks in this subtree (self, inputs, then next chain), depth-first.</summary>
        public IEnumerable<Block> Descendants()
        {
            var stack = new Stack<Block>();
            stack.Push(this);
            while (stack.Count > 0)
            {
                var b = stack.Pop();
                yield return b;
                if (b.Next?.Target != null) stack.Push(b.Next.Target.Owner);
                for (int i = b.Inputs.Count - 1; i >= 0; i--)
                {
                    var c = b.Inputs[i].Connection;
                    if (c?.Target != null) stack.Push(c.Target.Owner);
                }
            }
        }

        /// <summary>Builds the block's inputs/fields/connections from its definition.</summary>
        public void Build(Dictionary<string, object> mutatorState = null)
        {
            var def = Definition;
            Inputs.Clear();
            Output = Previous = Next = null;
            var pending = new List<Field>();
            foreach (var part in def.Parts)
            {
                switch (part.Kind)
                {
                    case ArgKind.Label:
                        pending.Add(new Field { Spec = part, Value = part.Text });
                        break;
                    case ArgKind.Field:
                        pending.Add(new Field { Name = part.Name, Spec = part, Value = DefaultFieldValue(part) });
                        break;
                    default:
                        var input = new Input
                        {
                            Name = part.Name,
                            Align = part.Align,
                            Kind = part.Kind == ArgKind.InputValue ? InputKind.Value
                                 : part.Kind == ArgKind.InputStatement ? InputKind.Statement : InputKind.Dummy,
                        };
                        input.Fields.AddRange(pending);
                        pending.Clear();
                        if (input.Kind == InputKind.Value)
                            input.Connection = new Connection(this, ConnectionType.InputValue, input, part.Check);
                        else if (input.Kind == InputKind.Statement)
                            input.Connection = new Connection(this, ConnectionType.NextStatement, input, part.Check);
                        Inputs.Add(input);
                        break;
                }
            }
            if (pending.Count > 0)
                Inputs.Add(new Input { Kind = InputKind.Dummy, Fields = pending });

            if (def.HasOutput) Output = new Connection(this, ConnectionType.OutputValue, null, def.OutputCheck);
            if (def.HasPrevious) Previous = new Connection(this, ConnectionType.PreviousStatement, null, def.PreviousCheck);
            if (def.HasNext) Next = new Connection(this, ConnectionType.NextStatement, null, def.NextCheck);
            InputsInline = def.InputsInline ?? false;
            def.Mutator?.Apply(this, mutatorState);
        }

        static string DefaultFieldValue(ArgSpec s)
        {
            if (s.FieldType == "field_checkbox") return s.Checked ? "TRUE" : "FALSE";
            return s.Text ?? "";
        }
    }

    public sealed class Variable
    {
        public string Id, Name, Type;
    }

    public sealed class Workspace
    {
        public readonly BlockRegistry Registry;
        public readonly List<Block> TopBlocks = new List<Block>();
        public readonly List<Variable> Variables = new List<Variable>();
        int nextId = 1;

        public Workspace(BlockRegistry registry) { Registry = registry; }

        /// <summary>A block id nobody in this workspace has. Loaded programs bring their own ids, so a plain counter would collide.</summary>
        public string NewId()
        {
            string id;
            do id = "b" + (nextId++); while (IdInUse(id));
            return id;
        }

        bool IdInUse(string id)
        {
            foreach (var t in TopBlocks)
                foreach (var b in t.Descendants())
                    if (b.Id == id) return true;
            return false;
        }

        /// <summary>Empties the workspace (blocks and variables) but keeps the object, which views and runners hold on to.</summary>
        public void Clear()
        {
            TopBlocks.Clear();
            Variables.Clear();
        }

        public Block CreateBlock(string type, string id = null)
        {
            if (!Registry.TryGet(type, out var def))
                throw new KeyNotFoundException($"Unknown block type '{type}'");
            var b = new Block { Id = id ?? NewId(), Type = type, Definition = def };
            b.Build();
            return b;
        }

        public Variable GetVariableById(string id) => Variables.Find(v => v.Id == id);
        public Variable GetVariableByName(string name) => Variables.Find(v => v.Name == name);

        /// <summary>An id no variable uses. (Counting variables is not enough: after a delete the count collides with a live id.)</summary>
        string NewVariableId()
        {
            int n = Variables.Count + 1;
            string id;
            do id = "v" + n++; while (GetVariableById(id) != null);
            return id;
        }

        public Variable GetOrCreateVariable(string name, string id = null, string type = "")
        {
            var v = (id != null ? GetVariableById(id) : null) ?? GetVariableByName(name);
            if (v == null) Variables.Add(v = new Variable { Id = id ?? NewVariableId(), Name = name, Type = type });
            return v;
        }

        public IEnumerable<Block> AllBlocks()
        {
            foreach (var t in TopBlocks) foreach (var b in t.Descendants()) yield return b;
        }

        public Block FindBlock(string id)
        {
            foreach (var b in AllBlocks()) if (b.Id == id) return b;
            return null;
        }
    }
}
