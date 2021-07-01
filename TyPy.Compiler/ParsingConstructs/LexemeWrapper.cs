using System;
using TyPy.Compiler.LexingConstructs;

namespace TyPy.Compiler.ParsingConstructs
{
    /// <summary>
    /// Parse-composable class (see <see cref="IParsable"/>) that permits the reading of a lexeme from the lexical
    /// stream provided. This skips the default skippable lexemes given by the configuration
    /// (<see cref="PipelineConfiguration"/>), unless that lexeme is equal to the lexeme we want to parse. This enables
    /// indentation based on whitespace to work correctly.
    /// </summary>
    public class LexemeWrapper : IParsable
    {
        /// <summary>
        /// Stores the token that should be read from the lexical stream as given by the constructor.
        /// </summary>
        public LexToken Token { get; }
        
        /// <summary>
        /// Initialises the class to process the given token.
        /// </summary>
        /// <param name="token">
        /// The token that should be read from the lexical stream after skippable characters (excluding this token)
        /// have been removed.
        /// </param>
        public LexemeWrapper(LexToken token)
        {
            Token = token;
        }
        
        public bool TryParse(ArraySegment<Lexeme> lexemes, PipelineConfiguration configuration,
            out ParseTreeNode parseTreeNode)
        {
            var i = 0;
            if (i >= lexemes.Count)
            {
                parseTreeNode = null;
                return false;
            }
            while (configuration.DefaultSkipLexemes.Contains(lexemes[i].Token) && lexemes[i].Token != Token)
            {
                i++;
                if (i >= lexemes.Count)
                {
                    parseTreeNode = null;
                    return false;
                }
            }

            if (lexemes[i].Token == Token)
            {
                parseTreeNode = new ParseTreeNode {Lexeme = lexemes[i], LexemeCount = i + 1};
                return true;
            }
            
            parseTreeNode = null;
            return false;
        }
    }
}