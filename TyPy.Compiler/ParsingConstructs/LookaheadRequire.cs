using System;
using TyPy.Compiler.LexingConstructs;

namespace TyPy.Compiler.ParsingConstructs
{
    /// <summary>
    /// Checks whether the next tokens in the lexeme stream can be parsed as the provided type. In PEG grammars
    /// this is equivalent to the and-predicate. Importantly, the tokens are not consumed.
    /// </summary>
    public class LookaheadRequire : IParsable
    {
        public ParseToken Token { get; }
        public IParsable SubParsable { get; }

        /// <summary>
        /// Checks whether the next tokens in the lexeme stream can be parsed as the provided type. In PEG grammars
        /// this is equivalent to the and-predicate. The tokens are not consumed, however.
        /// </summary>
        /// <param name="token">The token that is required but not consumed.</param>
        public LookaheadRequire(ParseToken token)
        {
            Token = token;
        }
        
        /// <summary>
        /// Checks whether the next tokens in the lexeme stream can be parsed as the provided type. In PEG grammars
        /// this is equivalent to the and-predicate. The tokens are not consumed, however.
        /// </summary>
        /// <param name="subParsable">The IParsable that is required but not consumed.</param>
        public LookaheadRequire(IParsable subParsable)
        {
            SubParsable = subParsable;
        }
        
        public bool TryParse(ArraySegment<Lexeme> lexemes, PipelineConfiguration configuration, out ParseTreeNode parseTreeNode)
        {
            parseTreeNode = null;
            
            if (SubParsable is not null) return SubParsable.TryParse(lexemes, configuration, out _);
            if (!configuration.Grammar.ContainsKey(Token))
                throw new ParseException($"Grammar is incomplete. Token {Token} could not be resolved.");

            return configuration.Grammar[Token].TryParse(lexemes, configuration, out _);
        }
    }
}