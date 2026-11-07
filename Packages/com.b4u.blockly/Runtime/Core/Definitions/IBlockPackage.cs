using System.Collections.Generic;
using Blockly.Editing;

namespace Blockly
{
    /// <summary>
    /// A set of custom blocks that plugs into the built-in ones: their JSON definitions, the C# that runs them and,
    /// optionally, a toolbox category to pick them from. Add one with <c>StandardBlockSet.AddPackage</c>.
    /// </summary>
    public interface IBlockPackage
    {
        /// <summary>Block definitions in Blockly's JSON format (the same text you would give <c>Blockly.defineBlocksWithJsonArray</c>).</summary>
        string BlocksJson { get; }

        /// <summary>Adds the interpreter side of the blocks: <c>r.Expression(type, ...)</c> or <c>r.Statement(type, ...)</c>.</summary>
        void RegisterHandlers(HandlerRegistry handlers);

        /// <summary>Toolbox categories to append after the built-in ones, or null to leave the toolbox alone.</summary>
        IEnumerable<ToolboxCategory> ToolboxCategories { get; }
    }
}
