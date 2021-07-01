using System;
using TyPy.Compiler.LexingConstructs;

namespace TyPy.Compiler.ParsingConstructs
{
    /// <summary>
    /// This interface acts as a contract for all Backus-Naur Form (BNF) grammar parse-composable classes.
    /// Parse-composable means that several class instantiations can be composed to complete a full BNF grammar.
    /// </summary>
    public interface IParsable
    {
        /// <summary>
        /// Attempts to parse the current IParsable according to the configuration given in initialisation.
        /// </summary>
        /// <param name="lexemes">Represents a view into the lexemes. ArraySegment is chosen for performance reasons.</param>
        /// <param name="configuration">Configuration object for lexemes, skippable tokens, and parse tokens for referencing tokens.</param>
        /// <param name="parseTreeNode">`out` parameter of the successfully parsed node. `null` if the parsing was not successful.</param>
        /// <returns>`true` when the node was passed successfully, `false` when the node was not parsed successfully</returns>
        public bool TryParse(ArraySegment<Lexeme> lexemes, PipelineConfiguration configuration,
            out ParseTreeNode parseTreeNode);
    }
}