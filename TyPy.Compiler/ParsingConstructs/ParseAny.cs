using System;
using TyPy.Compiler.LexingConstructs;

namespace TyPy.Compiler.ParsingConstructs
{
    public class ParseAny : IParsable
    {
        public IParsable[] SubParsables { get; }
        public ParseToken[] ParseTokens { get; }
        
        public ParseAny(params IParsable[] subParsables)
        {
            SubParsables = subParsables;
        }

        public ParseAny(params ParseToken[] parseTokens)
        {
            ParseTokens = parseTokens;
        }
        
        public bool TryParse(ArraySegment<Lexeme> lexemes, PipelineConfiguration configuration,
            out AstNode astNode)
        {
            if (SubParsables is not null)
            {
                foreach (var subParsable in SubParsables)
                {
                    if (subParsable.TryParse(lexemes, configuration, out var localAstNode))
                    {
                        astNode = localAstNode;
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
                        astNode = localAstNode;
                        astNode.Token = parseToken;
                        return true;
                    }
                }
            }

            astNode = null;
            return false;
        }
    }
}