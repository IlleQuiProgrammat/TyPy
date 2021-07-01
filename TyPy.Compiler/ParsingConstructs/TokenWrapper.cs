using System;
using System.Collections.Generic;
using System.IO;
using TyPy.Compiler.LexingConstructs;

namespace TyPy.Compiler.ParsingConstructs
{
    /// <summary>
    /// Resolves a ParseToken from a grammar so that parse tokens can be parsed along with parse-composable classes.
    /// </summary>
    public class TokenWrapper : IParsable
    {
        public ParseToken Token { get; }

        /// <summary>
        /// Resolves a ParseToken from a grammar so that parse tokens can be parsed along with parse-composable classes.
        /// </summary>
        /// <param name="token">The token to be resolved and parsed.</param>
        public TokenWrapper(ParseToken token)
        {
            Token = token;
        }
        
        public bool TryParse(ArraySegment<Lexeme> lexemes, PipelineConfiguration configuration,
            out ParseTreeNode parseTreeNode)
        {
            if (!configuration.Grammar.ContainsKey(Token))
            {
                throw new ParseException($"Grammar is incomplete. Token {Token} could not be resolved.");
            }

            var success = configuration.Grammar[Token].TryParse(lexemes, configuration, out parseTreeNode);
            if (success) parseTreeNode.Token = Token;
            return success;
        }
    }
}