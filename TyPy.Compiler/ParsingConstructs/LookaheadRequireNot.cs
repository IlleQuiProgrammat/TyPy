using System;
using TyPy.Compiler.LexingConstructs;

namespace TyPy.Compiler.ParsingConstructs
{
    /// <summary>
    /// Checks whether the next tokens in the lexeme stream can be parsed as the provided type. If they can, this
    /// determines that the node is un-parsable. In PEG grammars, this is equivalent to the not-predicate.
    /// </summary>
    public class LookaheadRequireNot : IParsable
    {
        public ParseToken Token { get; }
        public IParsable SubParsable { get; }

        /// <summary>
        /// Checks whether the next tokens in the lexeme stream can be parsed as the provided type. If they can, this
        /// determines that the node is un-parsable. In PEG grammars, this is equivalent to the not-predicate.
        /// </summary>
        /// <param name="token">The token that must not be present for successful parsing.</param>
        public LookaheadRequireNot(ParseToken token)
        {
            Token = token;
        }
        
        /// <summary>
        /// Checks whether the next tokens in the lexeme stream can be parsed as the provided type. If they can, this
        /// determines that the node is un-parsable. In PEG grammars, this is equivalent to the not-predicate.
        /// </summary>
        /// <param name="subParsable">The IParsable that must not be present for successful parsing.</param>
        public LookaheadRequireNot(IParsable subParsable)
        {
            SubParsable = subParsable;
        }
        
        public bool TryParse(ArraySegment<Lexeme> lexemes, PipelineConfiguration configuration, out ParseTreeNode parseTreeNode)
        {
            parseTreeNode = null;
            
            if (SubParsable is not null) return !SubParsable.TryParse(lexemes, configuration, out _);
            if (!configuration.Grammar.ContainsKey(Token))
                throw new ParseException($"Grammar is incomplete. Token {Token} could not be resolved.");

            return !configuration.Grammar[Token].TryParse(lexemes, configuration, out _);
        }
    }
}