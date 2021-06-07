using System;
using TyPy.Compiler.LexingConstructs;

namespace TyPy.Compiler.ParsingConstructs
{
    /// <summary>
    /// Parse-composable class (see <see cref="IParsable"/>) that permits the parsing of the given parameter. any number
    /// if times. This, like others, will greedily parse, potentially removing tokens for future use. It is assumed
    /// that this will be handled for in the grammar. It is done for efficiency purposes but does not necessarily test
    /// the correctness of the grammar. This requires at least 1. To make it perform like `*`, one should wrap it in an
    /// optional class.
    /// </summary>
    public class ParseRepeat : IParsable
    {
        public IParsable SubParsable { get; }
        public ParseToken Token { get; }
        
        /// <summary>
        /// Accepts a ParseToken which is resolved by the grammar specified by the configuration
        /// (<see cref="PipelineConfiguration" />). This is parsed as many times as exists in the lexeme stream.
        /// </summary>
        /// <param name="token">A token that should parse as many as exist</param>
        public ParseRepeat(ParseToken token)
        {
            Token = token;
        }

        /// <summary>
        /// Accepts an IParsable which is parsed as many times as exists in the lexeme stream.
        /// </summary>
        /// <param name="subParsable">An IParsable that should parse as many as exist</param>
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