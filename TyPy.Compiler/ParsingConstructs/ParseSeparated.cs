using System;
using TyPy.Compiler.LexingConstructs;

namespace TyPy.Compiler.ParsingConstructs
{
    /// <summary>
    /// Parse-composable class (see <see cref="IParsable"/>) that permits the parsing of a given rule separated by
    /// another rule any number of times. This, like others, will greedily parse, potentially removing tokens for future
    /// use. It is assumed that this will be handled for in the grammar. It is done for efficiency purposes but does not
    /// necessarily test the correctness of the grammar. This requires at least 1 token to be parsed. Wrap in an
    /// optional parsable if you permit parsing nothing.
    /// </summary>
    public class ParseSeparated : IParsable
    {
        public IParsable SeparatorParsable { get; set; }
        public IParsable Parsable { get; set; }

        public ParseToken SeparatorToken { get; set; }
        public ParseToken Token { get; set; }

        /// <summary>
        /// Accepts a ParseTokens which are resolved by the grammar specified by the configuration
        /// (<see cref="PipelineConfiguration" />). This is parsed as many times as exists in the lexeme stream.
        /// </summary>
        /// <param name="separator">The separator to separate n > 1 occurrences</param>
        /// <param name="token">Token separated by the separator</param>
        public ParseSeparated(ParseToken separator, ParseToken token)
        {
            SeparatorToken = separator;
            Token = token;
        }

        /// <summary>
        /// Accepts an IParsable which is parsed as many times as exists in the lexeme stream separated by the
        /// separator. Ordering is consistent with `s.e+` rules.
        /// </summary>
        /// <param name="separator">An IParsable that separates the parsable tokens</param>
        /// <param name="parsable">An IParsable that should parse as many as exist separated by separator</param>
        public ParseSeparated(IParsable separator, IParsable parsable)
        {
            SeparatorParsable = separator;
            Parsable = parsable;
        }

        public bool TryParse(ArraySegment<Lexeme> lexemes, PipelineConfiguration configuration,
            out ParseTreeNode parseTreeNode)
        {
            IParsable resolvedParsable;
            IParsable resolvedSeparator;

            if (Parsable is not null)
            {
                resolvedParsable = Parsable;
                resolvedSeparator = SeparatorParsable;
            }
            else
            {
                if (!configuration.Grammar.ContainsKey(Token))
                {
                    throw new ParseException($"Grammar is incomplete. Token {Token} could not be resolved.");
                }

                if (!configuration.Grammar.ContainsKey(SeparatorToken))
                {
                    throw new ParseException($"Grammar is incomplete. Token {SeparatorToken} could not be resolved.");
                }

                resolvedParsable = configuration.Grammar[Token];
                resolvedSeparator = configuration.Grammar[SeparatorToken];
            }

            var success = true;
            var localAstNode = new ParseTreeNode();

            success &= resolvedParsable.TryParse(lexemes.Slice(localAstNode.LexemeCount), configuration,
                out var firstAstNode);

            if (success)
            {
                if (Parsable is null)
                {
                    firstAstNode.Token = Token;
                }

                localAstNode.AppendNode(firstAstNode);
            }
            else
            {
                parseTreeNode = null;
                return false;
            }

            do
            {
                success &= resolvedSeparator.TryParse(lexemes.Slice(localAstNode.LexemeCount), configuration, out _);
                success &= resolvedParsable.TryParse(lexemes.Slice(localAstNode.LexemeCount), configuration,
                    out var nextAstNode);

                if (success)
                {
                    if (Parsable is null)
                    {
                        nextAstNode.Token = Token;
                    }

                    localAstNode.AppendNode(nextAstNode);
                }
            } while (success);

            parseTreeNode = localAstNode;
            return true;
        }
    }
}