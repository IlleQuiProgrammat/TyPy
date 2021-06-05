using System;
using TyPy.Compiler.LexingConstructs;

namespace TyPy.Compiler.ParsingConstructs
{
    public class ParseOptional : IParsable
    {
        public ParseToken Token { get; }
        public IParsable SubParsable { get; }

        public ParseOptional(ParseToken token)
        {
            Token = token;
        }

        public ParseOptional(IParsable subParsable)
        {
            SubParsable = subParsable;
        }
        
        public bool TryParse(ArraySegment<Lexeme> lexemes, PipelineConfiguration configuration,
            out AstNode astNode)
        {
            if (SubParsable is not null)
            {
                SubParsable.TryParse(lexemes, configuration, out astNode);
            }
            else
            {
                if (!configuration.Grammar.ContainsKey(Token))
                {
                    throw new ParseException($"Grammar is incomplete. Token {Token} could not be resolved.");
                }
                
                var success = configuration.Grammar[Token].TryParse(lexemes, configuration, out var localAstNode);
                if (success) localAstNode.Token = Token;
                astNode = localAstNode;
            }
            
            return true;
        }
    }
}