using System;
using TyPy.Compiler.LexingConstructs;

namespace TyPy.Compiler.ParsingConstructs
{
    /// <summary>
    /// Parse-composable class (see <see cref="IParsable"/>) that permits the parsing of only one of the given
    /// parameters. It is assumed that only one route will be valid and hence short-circuits when that condition is met.
    /// This is done for efficiency purposes but does not necessarily test the correctness of the grammar.
    /// </summary>
    public class ParseAny : IParsable
    {
        public IParsable[] SubParsables { get; }
        public ParseToken[] ParseTokens { get; }
        
        /// <summary>
        /// Accepts a variable number of of IParsables of which the first one that succeeds is used.
        /// </summary>
        /// <param name="subParsables"></param>
        public ParseAny(params IParsable[] subParsables)
        {
            SubParsables = subParsables;
        }
        
        /// <summary>
        /// Accepts a variable number of of ParseTokens which are resolved by the grammar specified by the configuration
        /// (<see cref="PipelineConfiguration" />). The first one that succeeds is used.
        /// </summary>
        /// <param name="parseTokens"></param>
        public ParseAny(params ParseToken[] parseTokens)
        {
            ParseTokens = parseTokens;
        }
        
        public bool TryParse(ArraySegment<Lexeme> lexemes, PipelineConfiguration configuration,
            out ParseTreeNode parseTreeNode)
        {
            if (SubParsables is not null)
            {
                foreach (var subParsable in SubParsables)
                {
                    if (subParsable.TryParse(lexemes, configuration, out var localAstNode))
                    {
                        parseTreeNode = localAstNode;
                        return true;
                    }
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

                    if (configuration.Grammar[parseToken].TryParse(lexemes, configuration, out var localAstNode))
                    {
                        parseTreeNode = localAstNode;
                        parseTreeNode.Token = parseToken;
                        return true;
                    }
                }
            }

            parseTreeNode = null;
            return false;
        }
    }
}