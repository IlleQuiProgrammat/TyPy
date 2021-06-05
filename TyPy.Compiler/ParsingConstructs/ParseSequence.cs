using System;
using System.Collections.Generic;
using System.IO;
using TyPy.Compiler.LexingConstructs;

namespace TyPy.Compiler.ParsingConstructs
{
    public class ParseSequence : IParsable
    {
        public IParsable[] SubParsables { get; }
        public ParseToken[] ParseTokens { get; }

        public ParseSequence(params IParsable[] subParsables)
        {
            SubParsables = subParsables;
        }

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