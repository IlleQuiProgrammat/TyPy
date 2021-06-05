using System;
using TyPy.Compiler.LexingConstructs;

namespace TyPy.Compiler.ParsingConstructs
{
    public class ParseRepeat : IParsable
    {
        public IParsable SubParsable { get; }
        public ParseToken Token { get; }

        public ParseRepeat(ParseToken token)
        {
            Token = token;
        }

        public ParseRepeat(IParsable subParsable)
        {
            SubParsable = subParsable;
        }
        
        public bool TryParse(ArraySegment<Lexeme> lexemes, PipelineConfiguration configuration,
            out AstNode astNode)
        {
            IParsable resolvedParsable;
            
            if (SubParsable is not null)
            {
                resolvedParsable = SubParsable;
            }
            else
            {
                
                if (!configuration.Grammar.ContainsKey(Token))
                {
                    throw new ParseException($"Grammar is incomplete. Token {Token} could not be resolved.");
                }

                resolvedParsable = configuration.Grammar[Token];
            }
            
            var success = true;
            var localAstNode = new AstNode();
            var count = 0;
            do
            {
                success &= resolvedParsable.TryParse(lexemes.Slice(localAstNode.LexemeCount), configuration, out var nextAstNode);
                if (success)
                {
                    count++;
                    
                    if (SubParsable is null)
                    {
                        nextAstNode.Token = Token;
                    }
                    
                    localAstNode.AppendNode(nextAstNode);
                }
            } while (success);

            if (count == 0)
            {
                astNode = null;
                return false;
            }

            astNode = localAstNode;
            return true;
        }
    }
}