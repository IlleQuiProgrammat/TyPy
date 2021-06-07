using System;
using System.Collections.Generic;
using System.IO;
using TyPy.Compiler.LexingConstructs;

namespace TyPy.Compiler.ParsingConstructs
{
    /// <summary>
    /// Parse-composable class (see <see cref="IParsable"/>) that parses the sequence of IParsables given. All such
    /// Sub-Parsables must be successfully parsed in order for this to be successfully parsed.
    /// </summary>
    public class ParseSequence : IParsable
    {
        public IParsable[] SubParsables { get; }
        public ParseToken[] ParseTokens { get; }

        
        /// <summary>
        /// Accepts a variable number of of ParseTokens which are resolved by the grammar specified by the configuration
        /// (<see cref="PipelineConfiguration" />). These first one that succeeds is used.
        /// </summary>
        /// <param name="subParsables">The ordered sequence of parsables to parse.</param>
        public ParseSequence(params IParsable[] subParsables)
        {
            SubParsables = subParsables;
        }

        /// <summary>
        /// Accepts a variable number of of ParseTokens which are resolved by the grammar specified by the configuration
        /// (<see cref="PipelineConfiguration" />). These are parsed in order.
        /// </summary>
        /// <param name="parseTokens">The ordered sequence of tokens to parse.</param>
        public ParseSequence(params ParseToken[] parseTokens)
        {
            ParseTokens = parseTokens;
        }

        public bool TryParse(ArraySegment<Lexeme> lexemes, PipelineConfiguration configuration,
            out AstNode astNode)
        {
            astNode = new AstNode();
            if (SubParsables is not null)
            {
                foreach (var subParsable in SubParsables)
                {
                    if (!subParsable.TryParse(lexemes.Slice(astNode.LexemeCount), configuration, out var localAstNode))
                    {
                        astNode = null;
                        return false;
                    }

                    astNode.AppendNode(localAstNode);
                }
            }
            else
            {
                foreach (var parseToken in ParseTokens)
                {
                    if (!configuration.Grammar.ContainsKey(parseToken))
                    {
                        throw new ParseException($"Grammar is incomplete. Token {parseToken} could not be resolved.");
                    }

                    if (!configuration.Grammar[parseToken].TryParse(lexemes.Slice(astNode.LexemeCount), configuration,
                        out var localAstNode))
                    {
                        astNode = null;
                        return false;
                    }

                    localAstNode.Token = parseToken;
                    astNode.AppendNode(localAstNode);
                }
            }

            return true;
        }
    }
}