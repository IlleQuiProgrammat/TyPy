using System;
using System.Collections.Generic;
using System.IO;
using TyPy.Compiler.LexingConstructs;

namespace TyPy.Compiler.ParsingConstructs
{
    public class TokenWrapper : IParsable
    {
        public ParseToken Token { get; }

        public TokenWrapper(ParseToken token)
        {
            Token = token;
        }
        
        public bool TryParse(ArraySegment<Lexeme> lexemes, PipelineConfiguration configuration,
            out AstNode astNode)
        {
            if (!configuration.Grammar.ContainsKey(Token))
            {
                throw new ParseException($"Grammar is incomplete. Token {Token} could not be resolved.");
            }

            var success = configuration.Grammar[Token].TryParse(lexemes, configuration, out astNode);
            if (success) astNode.Token = Token;
            return success;
        }
    }
}