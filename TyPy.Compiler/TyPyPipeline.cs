using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TyPy.Compiler.LexingConstructs;
using TyPy.Compiler.ParsingConstructs;

namespace TyPy.Compiler
{
    public class TyPyPipeline
    {
        /// <summary>
        /// Describes the configuration of the pipeline. Currently set to an infix notation parser. See BNF:
        /// * Infix notation bnf
        /// * compilation_unit: ((expression | comment) NEWLINE)+;
        /// * expression: m_expression (('+' | '-') expression)?;
        /// * m_expression: i_expression (('*' | '/') m_expression)?;
        /// * i_expression: b_expression ('^' i_expression);
        /// * b_expression: NUMBER | '(' expression ')';
        /// * comment: '#' [\w ]*;
        /// * NUMBER: [0-9]+;
        /// </summary>
        public readonly PipelineConfiguration Configuration = new()
        {
            LexicalSyntax = new()
            {
                {LexToken.Caret, new Regex(@"\^")},
                {LexToken.Comment, new Regex(@"#.*")},
                {LexToken.Divide, new Regex("/")},
                {LexToken.Minus, new Regex("-")},
                {LexToken.Newline, new Regex(@"[\r\n]")},
                {LexToken.Number, new Regex("[0-9]+")},
                {LexToken.Plus, new Regex(@"\+")},
                {LexToken.Times, new Regex(@"\*")},
                {LexToken.CloseBrackets, new Regex(@"\)")},
                {LexToken.OpenBrackets, new Regex(@"\(")},
                {LexToken.Whitespace, new Regex(@"[\t ]+")}
            },
            CompilationUnitToken = ParseToken.CompilationUnit,
            Grammar = new()
            {
                {
                    ParseToken.CompilationUnit,
                    new ParseRepeat(new ParseSequence(new ParseAny(ParseToken.Expression, ParseToken.Comment),
                        new LexemeWrapper(LexToken.Newline)))
                },
                {ParseToken.Comment, new LexemeWrapper(LexToken.Comment)},
                {
                    ParseToken.Expression,
                    new ParseSequence(new TokenWrapper(ParseToken.DivMulExpression),
                        new ParseOptional(new ParseSequence(
                            new ParseAny(new LexemeWrapper(LexToken.Plus), new LexemeWrapper(LexToken.Minus)),
                            new TokenWrapper(ParseToken.Expression))))
                },
                {
                    ParseToken.DivMulExpression,
                    new ParseSequence(new TokenWrapper(ParseToken.IndexExpression),
                        new ParseOptional(new ParseSequence(
                            new ParseAny(new LexemeWrapper(LexToken.Times), new LexemeWrapper(LexToken.Divide)),
                            new TokenWrapper(ParseToken.DivMulExpression))))
                },
                {
                    ParseToken.IndexExpression,
                    new ParseSequence(new TokenWrapper(ParseToken.BracketedExpression),
                        new ParseOptional(new ParseSequence(new LexemeWrapper(LexToken.Caret),
                            new TokenWrapper(ParseToken.IndexExpression))))
                },
                {
                    ParseToken.BracketedExpression,
                    new ParseAny(new LexemeWrapper(LexToken.Number), new ParseSequence(
                        new LexemeWrapper(LexToken.OpenBrackets),
                        new TokenWrapper(ParseToken.Expression),
                        new LexemeWrapper(LexToken.CloseBrackets)))
                }
            },
            DefaultSkipLexemes = new() {LexToken.Whitespace}
        };

        private readonly Parser _parser;
        private readonly Lexer _lexer;

        public TyPyPipeline()
        {
            _lexer = new Lexer(Configuration);
            _parser = new Parser(Configuration);
        }

        public void Execute(string fileContents)
        {
            var lexemes = _lexer.Lex(fileContents);
            var astNode = _parser.Parse(lexemes);
            foreach (var line in astNode.Children)
            {
                Console.WriteLine(ComputeExpression(line.Children[0]));
            }
        }

        private double ComputeExpression(AstNode astNode)
        {
            if (astNode.Lexeme?.Token == LexToken.Number)
            {
                return int.Parse(astNode.Lexeme.Content);
            }

            if (astNode.Children.Count == 1)
            {
                return ComputeExpression(astNode.Children[0]);
            }

            switch (astNode.Token)
            {
                case ParseToken.Anonymous:
                    throw new ParseException("Anonymous token cannot appear in this context.");
                case ParseToken.Comment:
                    return 0;
                case ParseToken.Expression:
                    return HandleExpression(astNode);
                case ParseToken.DivMulExpression:
                    return HandleDivMulExpression(astNode);
                case ParseToken.IndexExpression:
                    return Math.Pow(ComputeExpression(astNode.Children[0]),
                        ComputeExpression(astNode.Children[1].Children[1]));
                case ParseToken.BracketedExpression:
                    return ComputeExpression(astNode.Children[1]);
                default:
                    throw new ParseException($"Token ${astNode.Token} has no parse configuration.");
            }
        }

        private double HandleExpression(AstNode astNode)
        {
            var initial = ComputeExpression(astNode.Children[0]);
            var currentNode = astNode.Children[1];
            while (currentNode.Parent.Token == ParseToken.Expression)
            {
                var calculation = currentNode.Children[0].Lexeme.Token;
                if (calculation == LexToken.Plus)
                {
                    initial += ComputeExpression(currentNode.Children[1].Children[0]);
                }
                else
                {
                    initial -= ComputeExpression(currentNode.Children[1].Children[0]);
                }

                if (currentNode.Children[1].Children.Count == 1)
                {
                    return initial;
                }
                currentNode = currentNode.Children[1].Children[1];
            }

            return initial;
        }

        private double HandleDivMulExpression(AstNode astNode)
        {
            var initial = ComputeExpression(astNode.Children[0]);
            var currentNode = astNode.Children[1];
            while (currentNode.Parent.Token == ParseToken.DivMulExpression)
            {
                var calculation = currentNode.Children[0].Lexeme.Token;
                if (calculation == LexToken.Times)
                {
                    initial *= ComputeExpression(currentNode.Children[1].Children[0]);
                }
                else
                {
                    initial /= ComputeExpression(currentNode.Children[1].Children[0]);
                }

                if (currentNode.Children[1].Children.Count == 1)
                {
                    return initial;
                }
                currentNode = currentNode.Children[1].Children[1];
            }

            return initial;
        }
    }
}