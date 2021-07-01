using System.Linq.Expressions;
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
                new(LexToken.Caret, new Regex(@"\^")),
                new(LexToken.Comment, new Regex(@"#.*")),
                new(LexToken.Divide, new Regex("/")),
                new(LexToken.Minus, new Regex("-")),
                new(LexToken.Newline, new Regex(@"[\r\n]")),
                new(LexToken.Number, new Regex("[0-9]+")),
                new(LexToken.Plus, new Regex(@"\+")),
                new(LexToken.Times, new Regex(@"\*")),
                new(LexToken.CloseBrackets, new Regex(@"\)")),
                new(LexToken.OpenBrackets, new Regex(@"\(")),
                new(LexToken.Whitespace, new Regex(@"[\t ]+")),
                new(LexToken.Equals, new Regex("=")),
                new(LexToken.Name, new Regex(@"[a-zA-Z_][a-zA-Z0-9_]*")),
            },
            CompilationUnitToken = ParseToken.CompilationUnit,
            Grammar = new()
            {
                // # PEG grammar for Python


                // file: [statements] ENDMARKER 
                // interactive: statement_newline 
                // eval: expressions NEWLINE* ENDMARKER 
                // func_type: '(' [type_expressions] ')' '->' expression NEWLINE* ENDMARKER 
                {
                    ParseToken.CompilationUnit,
                    new ParseRepeat(new ParseSequence(new ParseAny(ParseToken.Expression, ParseToken.Comment),
                        new LexemeWrapper(LexToken.Newline)))
                },
                {ParseToken.Comment, new LexemeWrapper(LexToken.Comment)},

                // fstring: star_expressions
                {
                    ParseToken.FormatString,
                    new TokenWrapper(ParseToken.StarExpressions)
                },

                // # type_expressions allow */** but ignore them
                // type_expressions:
                //     | ','.expression+ ',' '*' expression ',' '**' expression 
                //     | ','.expression+ ',' '*' expression 
                //     | ','.expression+ ',' '**' expression 
                //     | '*' expression ',' '**' expression 
                //     | '*' expression 
                //     | '**' expression 
                //     | ','.expression+
                {
                    ParseToken.TypeExpressions,
                    new ParseAny(
                        // ','.expression+ ',' '*' expression ',' '**' expression 
                        new ParseSequence(
                            new ParseSeparated(
                                new LexemeWrapper(LexToken.Comma),
                                new TokenWrapper(ParseToken.Expression)
                            ),
                            new LexemeWrapper(LexToken.Star),
                            new TokenWrapper(ParseToken.Expression),
                            new LexemeWrapper(LexToken.Comma),
                            new LexemeWrapper(LexToken.DoubleStar),
                            new TokenWrapper(ParseToken.Expression)
                        ),

                        // ','.expression+ ',' '*' expression 
                        new ParseSequence(
                            new ParseSeparated(
                                new LexemeWrapper(LexToken.Comma),
                                new TokenWrapper(ParseToken.Expression)
                            ),
                            new LexemeWrapper(LexToken.Star),
                            new TokenWrapper(ParseToken.Expression),
                            new LexemeWrapper(LexToken.Comma)
                        ),

                        // ','.expression+ ',' '**' expression 
                        new ParseSequence(
                            new ParseSeparated(
                                new LexemeWrapper(LexToken.Comma),
                                new TokenWrapper(ParseToken.Expression)
                            ),
                            new LexemeWrapper(LexToken.Comma),
                            new LexemeWrapper(LexToken.DoubleStar),
                            new TokenWrapper(ParseToken.Expression)
                        ),

                        // '*' expression ',' '**' expression 
                        new ParseSequence(
                            new LexemeWrapper(LexToken.Star),
                            new TokenWrapper(ParseToken.Expression),
                            new LexemeWrapper(LexToken.Comma),
                            new LexemeWrapper(LexToken.DoubleStar),
                            new TokenWrapper(ParseToken.Expression)
                        ),

                        // '*' expression 
                        new ParseSequence(
                            new LexemeWrapper(LexToken.Star),
                            new TokenWrapper(ParseToken.Expression)
                        ),

                        // '**' expression 
                        new ParseSequence(
                            new LexemeWrapper(LexToken.DoubleStar),
                            new TokenWrapper(ParseToken.Expression)
                        ),

                        // ','.expression+
                        new ParseSeparated(
                            new LexemeWrapper(LexToken.Comma),
                            new TokenWrapper(ParseToken.Expression)
                        )
                    )
                },

                // statements: statement+ 
                {
                    ParseToken.Statements,
                    new ParseRepeat(ParseToken.Statement)
                },

                // statement: compound_stmt  | simple_stmt
                {
                    ParseToken.Statement,
                    new ParseAny(ParseToken.CompoundStatement, ParseToken.SimpleStatement)
                },

                // (ONLY USED BY INTERACTIVE) statement_newline:
                //     | compound_stmt NEWLINE 
                //     | simple_stmt
                //     | NEWLINE 
                //     | ENDMARKER

                // simple_stmt:
                //     | small_stmt !';' NEWLINE  # Not needed, there for speedup
                //     | ';'.small_stmt+ [';'] NEWLINE
                {
                    ParseToken.SimpleStatement,
                    new ParseAny(
                        // small_stmt !';' NEWLINE  # Not needed, there for speedup
                        new ParseSequence(
                            new TokenWrapper(ParseToken.SmallStatement),
                            new LookaheadRequireNot(new LexemeWrapper(LexToken.SemiColon)),
                            new LexemeWrapper(LexToken.Newline)
                        ),

                        // ';'.small_stmt+ [';'] NEWLINE
                        new ParseSequence(
                            new ParseSeparated(
                                new LexemeWrapper(LexToken.SemiColon),
                                new TokenWrapper(ParseToken.SmallStatement)
                            ),
                            new ParseOptional(new LexemeWrapper(LexToken.SemiColon)),
                            new LexemeWrapper(LexToken.Newline)
                        )
                    )
                },

                // # NOTE: assignment MUST precede expression, else parsing a simple assignment
                // # will throw a SyntaxError.
                // small_stmt:
                //     | assignment
                //     | star_expressions 
                //     | return_stmt
                //     | import_stmt
                //     | raise_stmt
                //     | 'pass' 
                //     | del_stmt
                //     | yield_stmt
                //     | assert_stmt
                //     | 'break' 
                //     | 'continue' 
                //     | global_stmt
                //     | nonlocal_stmt
                {
                    ParseToken.SmallStatement,
                    new ParseAny(
                        new TokenWrapper(ParseToken.Assignment),
                        new TokenWrapper(ParseToken.StarExpressions),
                        new TokenWrapper(ParseToken.ReturnStatement),
                        new TokenWrapper(ParseToken.ImportStatement),
                        new LexemeWrapper(LexToken.Pass),
                        new TokenWrapper(ParseToken.DelStatement),
                        new TokenWrapper(ParseToken.YieldStatement),
                        new TokenWrapper(ParseToken.AssertStatement),
                        new LexemeWrapper(LexToken.Break),
                        new LexemeWrapper(LexToken.Continue),
                        new TokenWrapper(ParseToken.GlobalStatement),
                        new TokenWrapper(ParseToken.NonLocalStatement)
                    )
                },

                // compound_stmt:
                //     | function_def
                //     | if_stmt
                //     | class_def
                //     | with_stmt
                //     | for_stmt
                //     | try_stmt
                //     | while_stmt
                {
                    ParseToken.CompoundStatement,
                    new ParseAny(
                        ParseToken.FunctionDefinition,
                        ParseToken.IfStatement,
                        ParseToken.ClassDefinition,
                        ParseToken.WithStatement,
                        ParseToken.ForStatement,
                        ParseToken.TryStatement,
                        ParseToken.WhileStatement
                    )
                },

                // # NOTE: annotated_rhs may start with 'yield'; yield_expr must start with 'yield'
                // assignment:
                //     | NAME ':' expression ['=' annotated_rhs ] 
                //     | ('(' single_target ')' 
                //          | single_subscript_attribute_target) ':' expression ['=' annotated_rhs ] 
                //     | (star_targets '=' )+ (yield_expr | star_expressions) !'=' [TYPE_COMMENT] 
                //     | single_target augassign ~ (yield_expr | star_expressions) 
                {
                    ParseToken.Assignment,
                    new ParseAny(
                        // NAME ':' expression ['=' annotated_rhs ] 
                        new ParseSequence(
                            new LexemeWrapper(LexToken.Name),
                            new LexemeWrapper(LexToken.Colon),
                            new ParseOptional(new ParseSequence(
                                new LexemeWrapper(LexToken.Equals),
                                new TokenWrapper(ParseToken.AnnotatedRhs)
                            ))
                        ),

                        // ('(' single_target ')' | single_subscript_attribute_target) ':' expression ['=' annotated_rhs] 
                        new ParseSequence(
                            new ParseAny(
                                new ParseSequence(
                                    new LexemeWrapper(LexToken.OpenBrackets),
                                    new TokenWrapper(ParseToken.SingleTarget),
                                    new LexemeWrapper(LexToken.CloseBrackets)
                                ),
                                new TokenWrapper(ParseToken.SingleSubscriptAttributeTarget)
                            ),
                            new LexemeWrapper(LexToken.Colon),
                            new TokenWrapper(ParseToken.Expression),
                            new ParseOptional(new ParseSequence(
                                new LexemeWrapper(LexToken.Equals),
                                new TokenWrapper(ParseToken.AnnotatedRhs)
                            ))
                        ),

                        // (star_targets '=' )+ (yield_expr | star_expressions) !'=' [TYPE_COMMENT] 
                        new ParseSequence(
                            new ParseRepeat(new ParseSequence(
                                new TokenWrapper(ParseToken.StarTargets),
                                new LexemeWrapper(LexToken.Equals)
                            )),
                            new ParseAny(ParseToken.YieldExpression, ParseToken.StarExpressions),
                            new LookaheadRequireNot(new LexemeWrapper(LexToken.Equals)),
                            new ParseOptional(new LexemeWrapper(LexToken.TypeComment))
                        ),

                        // TODO: We skip the cut operator because it is already handled by the ordered parsing
                        // single_target augassign ~ (yield_expr | star_expressions)
                        new ParseSequence(
                            new TokenWrapper(ParseToken.SingleTarget),
                            new TokenWrapper(ParseToken.AugmentingAssignment),
                            new ParseAny(ParseToken.YieldExpression, ParseToken.StarExpressions)
                        )
                    )
                },

                // augassign:
                //     | '+=' 
                //     | '-=' 
                //     | '*=' 
                //     | '@=' 
                //     | '/=' 
                //     | '%=' 
                //     | '&=' 
                //     | '|=' 
                //     | '^=' 
                //     | '<<=' 
                //     | '>>=' 
                //     | '**=' 
                //     | '//=' 
                {
                    ParseToken.AugmentingAssignment,
                    new ParseAny(
                        new LexemeWrapper(LexToken.PlusEquals),
                        new LexemeWrapper(LexToken.MinusEquals),
                        new LexemeWrapper(LexToken.TimesEquals),
                        new LexemeWrapper(LexToken.AtEquals),
                        new LexemeWrapper(LexToken.DivEquals),
                        new LexemeWrapper(LexToken.ModEquals),
                        new LexemeWrapper(LexToken.AndEquals),
                        new LexemeWrapper(LexToken.OrEquals),
                        new LexemeWrapper(LexToken.XorEquals),
                        new LexemeWrapper(LexToken.LShiftEquals),
                        new LexemeWrapper(LexToken.RShiftEquals),
                        new LexemeWrapper(LexToken.PowEquals),
                        new LexemeWrapper(LexToken.FloorDivEquals)
                    )
                },

                // global_stmt: 'global' ','.NAME+ 
                {
                    ParseToken.GlobalStatement,
                    new ParseSequence(
                        new LexemeWrapper(LexToken.Global),
                        new ParseSeparated(new LexemeWrapper(LexToken.Comma), new LexemeWrapper(LexToken.Name))
                    )
                },

                // nonlocal_stmt: 'nonlocal' ','.NAME+ 
                {
                    ParseToken.NonLocalStatement,
                    new ParseSequence(
                        new LexemeWrapper(LexToken.NonLocal),
                        new ParseSeparated(new LexemeWrapper(LexToken.Comma), new LexemeWrapper(LexToken.Name))
                    )
                },

                // yield_stmt: yield_expr 
                {
                    ParseToken.YieldStatement,
                    new TokenWrapper(ParseToken.YieldExpression)
                },

                // assert_stmt: 'assert' expression [',' expression ] 
                {
                    ParseToken.AssertStatement,
                    new ParseSequence(
                        new LexemeWrapper(LexToken.Assert),
                        new TokenWrapper(ParseToken.Expression),
                        new ParseOptional(new ParseSequence(
                            new LexemeWrapper(LexToken.Comma),
                            new TokenWrapper(ParseToken.Expression)
                        ))
                    )
                },

                // del_stmt:
                //     | 'del' del_targets &(';' | NEWLINE) 
                {
                    ParseToken.DelStatement,
                    new ParseSequence(
                        new LexemeWrapper(LexToken.Del),
                        new TokenWrapper(ParseToken.DelTargets),
                        new LookaheadRequire(new ParseAny(
                            new LexemeWrapper(LexToken.SemiColon),
                            new LexemeWrapper(LexToken.Newline)
                        ))
                    )
                },

                // import_stmt: import_name | import_from
                {
                    ParseToken.ImportStatement,
                    new ParseAny(ParseToken.ImportName, ParseToken.ImportFrom)
                },

                // import_name: 'import' dotted_as_names
                {
                    ParseToken.ImportName,
                    new ParseSequence(
                        new LexemeWrapper(LexToken.Import),
                        new TokenWrapper(ParseToken.DottedAsNames)
                    )
                },

                // # note below: the ('.' | '...') is necessary because '...' is tokenized as ELLIPSIS
                // import_from:
                //     | 'from' ('.' | '...')* dotted_name 'import' import_from_targets 
                //     | 'from' ('.' | '...')+ 'import' import_from_targets
                {
                    ParseToken.ImportFrom,
                    new ParseAny(
                        new ParseSequence(
                            new LexemeWrapper(LexToken.From),
                            new ParseOptional(new ParseRepeat(
                                new ParseAny(new LexemeWrapper(LexToken.Dot), new LexemeWrapper(LexToken.Ellipsis))
                            )),
                            new TokenWrapper(ParseToken.DottedName),
                            new LexemeWrapper(LexToken.Import),
                            new TokenWrapper(ParseToken.ImportFromTargets)
                        ),
                        new ParseSequence(
                            new LexemeWrapper(LexToken.From),
                            new ParseRepeat(
                                new ParseAny(new LexemeWrapper(LexToken.Dot), new LexemeWrapper(LexToken.Ellipsis))
                            ),
                            new LexemeWrapper(LexToken.Import),
                            new TokenWrapper(ParseToken.ImportFromTargets)
                        )
                    )
                },

                // import_from_targets:
                //     | '(' import_from_as_names [','] ')' 
                //     | import_from_as_names !','
                //     | '*' 
                {
                    ParseToken.ImportFromTargets,
                    new ParseAny(
                        new ParseSequence(
                            new LexemeWrapper(LexToken.OpenBrackets),
                            new TokenWrapper(ParseToken.ImportFromAsNames),
                            new ParseOptional(new LexemeWrapper(LexToken.Comma)),
                            new LexemeWrapper(LexToken.CloseBrackets)
                        ),
                        new ParseSequence(
                            new TokenWrapper(ParseToken.ImportFromAsNames),
                            new LookaheadRequireNot(new LexemeWrapper(LexToken.Comma))
                        ),
                        new LexemeWrapper(LexToken.Star)
                    )
                },

                // import_from_as_names:
                //     | ','.import_from_as_name+ 
                {
                    ParseToken.ImportFromAsNames,
                    new ParseSeparated(new LexemeWrapper(LexToken.Comma), new TokenWrapper(ParseToken.ImportFromAsName))
                },

                // import_from_as_name:
                //     | NAME ['as' NAME ] 
                {
                    ParseToken.ImportFromAsName,
                    new ParseSequence(
                        new LexemeWrapper(LexToken.Name),
                        new ParseOptional(new ParseSequence(
                            new LexemeWrapper(LexToken.As),
                            new LexemeWrapper(LexToken.Name)
                        ))
                    )
                },

                // dotted_as_names:
                //     | ','.dotted_as_name+ 
                {
                    ParseToken.DottedAsNames,
                    new ParseSeparated(new LexemeWrapper(LexToken.Comma), new TokenWrapper(ParseToken.DottedAsName))
                },

                // dotted_as_name:
                //     | dotted_name ['as' NAME ] 
                {
                    ParseToken.DottedAsNames,
                    new ParseSequence(
                        new TokenWrapper(ParseToken.DottedName),
                        new ParseOptional(new ParseSequence(
                            new LexemeWrapper(LexToken.As),
                            new TokenWrapper(ParseToken.DottedName)
                        ))
                    )
                },

                // dotted_name:
                //     | dotted_name '.' NAME 
                //     | NAME
                // Converted to:
                // dotted_name: '.'.NAME+
                {
                    ParseToken.DottedName,
                    new ParseSeparated(new LexemeWrapper(LexToken.Dot), new LexemeWrapper(LexToken.Name))
                },

                // if_stmt:
                //     | 'if' named_expression ':' block elif_stmt 
                //     | 'if' named_expression ':' block [else_block] 
                // Converted to:
                // if_stmt: 'if' named_expression ':' block (elif_stmt | else_block?)
                {
                    ParseToken.IfStatement,
                    new ParseSequence(
                        new LexemeWrapper(LexToken.If),
                        new TokenWrapper(ParseToken.NamedExpression),
                        new LexemeWrapper(LexToken.Colon),
                        new TokenWrapper(ParseToken.Block),
                        new ParseAny(
                            new TokenWrapper(ParseToken.ElifStatement),
                            new ParseOptional(ParseToken.ElseBlock)
                        )
                    )
                },

                // elif_stmt:
                //     | 'elif' named_expression ':' block elif_stmt 
                //     | 'elif' named_expression ':' block [else_block] 
                // Converted to:
                // elif_stmt: 'elif' named_expression ':' block (elif_stmt | else_block?)
                {
                    ParseToken.ElifStatement,
                    new ParseSequence(
                        new LexemeWrapper(LexToken.ElIf),
                        new TokenWrapper(ParseToken.NamedExpression),
                        new LexemeWrapper(LexToken.Colon),
                        new TokenWrapper(ParseToken.Block),
                        new ParseAny(
                            new TokenWrapper(ParseToken.ElifStatement),
                            new ParseOptional(ParseToken.ElseBlock)
                        )
                    )
                },

                // else_block: 'else' ':' block 
                {
                    ParseToken.ElseBlock,
                    new ParseSequence(
                        new LexemeWrapper(LexToken.Else),
                        new LexemeWrapper(LexToken.Colon),
                        new TokenWrapper(ParseToken.Block)
                    )
                },

                // while_stmt:
                //     | 'while' named_expression ':' block [else_block] 
                {
                    ParseToken.WhileStatement,
                    new ParseSequence(
                        new LexemeWrapper(LexToken.While),
                        new TokenWrapper(ParseToken.NamedExpression),
                        new LexemeWrapper(LexToken.Colon),
                        new TokenWrapper(ParseToken.Block),
                        new ParseOptional(ParseToken.ElseBlock)
                    )
                },

                // TODO: Cut operator
                // for_stmt:
                //     | 'for' star_targets 'in' ~ star_expressions ':' [TYPE_COMMENT] block [else_block] 
                //     | ASYNC 'for' star_targets 'in' ~ star_expressions ':' [TYPE_COMMENT] block [else_block]
                {
                    ParseToken.ForStatement,
                    new ParseAny(
                        new ParseSequence(
                            new LexemeWrapper(LexToken.For),
                            new TokenWrapper(ParseToken.StarTargets),
                            new LexemeWrapper(LexToken.In),
                            new TokenWrapper(ParseToken.StarExpressions),
                            new LexemeWrapper(LexToken.Colon),
                            new ParseOptional(new LexemeWrapper(LexToken.TypeComment)),
                            new TokenWrapper(ParseToken.Block),
                            new ParseOptional(ParseToken.ElseBlock)),
                        new ParseSequence(
                            new LexemeWrapper(LexToken.Async),
                            new LexemeWrapper(LexToken.For),
                            new TokenWrapper(ParseToken.StarTargets),
                            new LexemeWrapper(LexToken.In),
                            new TokenWrapper(ParseToken.StarExpressions),
                            new LexemeWrapper(LexToken.Colon),
                            new ParseOptional(new LexemeWrapper(LexToken.TypeComment)),
                            new TokenWrapper(ParseToken.Block),
                            new ParseOptional(ParseToken.ElseBlock)
                        )
                    )
                },

                // with_stmt:
                //     | 'with' '(' ','.with_item+ ','? ')' ':' block 
                //     | 'with' ','.with_item+ ':' [TYPE_COMMENT] block 
                //     | ASYNC 'with' '(' ','.with_item+ ','? ')' ':' block 
                //     | ASYNC 'with' ','.with_item+ ':' [TYPE_COMMENT] block 
                {
                    ParseToken.WithStatement,
                    new ParseAny(
                        new ParseSequence(
                            new LexemeWrapper(LexToken.With),
                            new LexemeWrapper(LexToken.OpenBrackets),
                            new ParseSeparated(new LexemeWrapper(LexToken.Comma),
                                new TokenWrapper(ParseToken.WithItem)),
                            new ParseOptional(new LexemeWrapper(LexToken.Comma)),
                            new LexemeWrapper(LexToken.CloseBrackets),
                            new LexemeWrapper(LexToken.Colon),
                            new TokenWrapper(ParseToken.Block)
                        ),
                        new ParseSequence(
                            new LexemeWrapper(LexToken.With),
                            new ParseSeparated(new LexemeWrapper(LexToken.Comma),
                                new TokenWrapper(ParseToken.WithItem)),
                            new ParseOptional(new LexemeWrapper(LexToken.TypeComment)),
                            new LexemeWrapper(LexToken.Colon),
                            new TokenWrapper(ParseToken.Block)
                        ),
                        new ParseSequence(
                            new LexemeWrapper(LexToken.Async),
                            new LexemeWrapper(LexToken.With),
                            new LexemeWrapper(LexToken.OpenBrackets),
                            new ParseSeparated(new LexemeWrapper(LexToken.Comma),
                                new TokenWrapper(ParseToken.WithItem)),
                            new ParseOptional(new LexemeWrapper(LexToken.Comma)),
                            new LexemeWrapper(LexToken.CloseBrackets),
                            new LexemeWrapper(LexToken.Colon),
                            new TokenWrapper(ParseToken.Block)
                        ),
                        new ParseSequence(
                            new LexemeWrapper(LexToken.Async),
                            new LexemeWrapper(LexToken.With),
                            new ParseSeparated(new LexemeWrapper(LexToken.Comma),
                                new TokenWrapper(ParseToken.WithItem)),
                            new ParseOptional(new LexemeWrapper(LexToken.TypeComment)),
                            new LexemeWrapper(LexToken.Colon),
                            new TokenWrapper(ParseToken.Block)
                        )
                    )
                },

                // with_item:
                //     | expression 'as' star_target &(',' | ')' | ':') 
                //     | expression 
                {
                    ParseToken.WithItem,
                    new ParseAny(
                        new ParseSequence(
                            new TokenWrapper(ParseToken.Expression),
                            new LexemeWrapper(LexToken.As),
                            new TokenWrapper(ParseToken.StarTarget),
                            new LookaheadRequire(new ParseAny(
                                new LexemeWrapper(LexToken.Comma),
                                new LexemeWrapper(LexToken.CloseBrackets),
                                new LexemeWrapper(LexToken.Colon)
                            ))
                        ),
                        new TokenWrapper(ParseToken.Expression)
                    )
                },

                // Automatically Generated and hand corrected
                {
					ParseToken.TryStatement,
					new ParseAny(
						new ParseSequence(
							new LexemeWrapper(LexToken.Try),
							new LexemeWrapper(LexToken.Colon),
							new TokenWrapper(ParseToken.Block),
							new TokenWrapper(ParseToken.FinallyBlock)
						),
						new ParseSequence(
							new LexemeWrapper(LexToken.Try),
							new LexemeWrapper(LexToken.Colon),
							new TokenWrapper(ParseToken.Block),
							new ParseRepeat(
								new TokenWrapper(ParseToken.ExceptBlock)
							),
							new ParseOptional(
								new TokenWrapper(ParseToken.ElseBlock)
							),
							new ParseOptional(
								new TokenWrapper(ParseToken.FinallyBlock)
							)
						)
					)
				},
				{
					ParseToken.ExceptBlock,
					new ParseAny(
						new ParseSequence(
							new LexemeWrapper(LexToken.Except),
							new TokenWrapper(ParseToken.Expression),
							new ParseOptional(
								new ParseSequence(
									new LexemeWrapper(LexToken.As),
									new LexemeWrapper(LexToken.Name)
								)
							),
							new LexemeWrapper(LexToken.Colon),
							new TokenWrapper(ParseToken.Block)
						),
						new ParseSequence(
							new LexemeWrapper(LexToken.Except),
							new LexemeWrapper(LexToken.Colon),
							new TokenWrapper(ParseToken.Block)
						)
					)
				},
				{
					ParseToken.FinallyBlock, 
					new ParseSequence(
						new LexemeWrapper(LexToken.Finally),
						new LexemeWrapper(LexToken.Colon),
						new TokenWrapper(ParseToken.Block)
					)
				},
				{
					ParseToken.ReturnStatement, 
					new ParseSequence(
						new LexemeWrapper(LexToken.Return),
						new ParseOptional(
							new TokenWrapper(ParseToken.StarExpressions)
						)
					)
				},
				{
					ParseToken.RaiseStatement,
					new ParseAny(
						new ParseSequence(
							new LexemeWrapper(LexToken.Raise),
							new TokenWrapper(ParseToken.Expression),
							new ParseOptional(
								new ParseSequence(
									new LexemeWrapper(LexToken.From),
									new TokenWrapper(ParseToken.Expression)
								)
							)
						),
						new LexemeWrapper(LexToken.Raise)
					)
				},
				{
					ParseToken.FunctionDefinition,
					new ParseAny(
						new ParseSequence(
							new TokenWrapper(ParseToken.Decorators),
							new TokenWrapper(ParseToken.FunctionDefinitionRaw)
						),
						new TokenWrapper(ParseToken.FunctionDefinitionRaw)
					)
				},
				{
					ParseToken.FunctionDefinitionRaw,
					new ParseAny(
						new ParseSequence(
							new LexemeWrapper(LexToken.Def),
							new LexemeWrapper(LexToken.Name),
							new LexemeWrapper(LexToken.OpenBrackets),
							new ParseOptional(
								new TokenWrapper(ParseToken.Params)
							),
							new LexemeWrapper(LexToken.CloseBrackets),
							new ParseOptional(
								new ParseSequence(
									new LexemeWrapper(LexToken.RightArrow),
									new TokenWrapper(ParseToken.Expression)
								)
							),
							new LexemeWrapper(LexToken.Colon),
							new ParseOptional(
								new TokenWrapper(ParseToken.FuncTypeComment)
							),
							new TokenWrapper(ParseToken.Block)
						),
						new ParseSequence(
							new LexemeWrapper(LexToken.Async),
							new LexemeWrapper(LexToken.Def),
							new LexemeWrapper(LexToken.Name),
							new LexemeWrapper(LexToken.OpenBrackets),
							new ParseOptional(
								new TokenWrapper(ParseToken.Params)
							),
							new LexemeWrapper(LexToken.CloseBrackets),
							new ParseOptional(
								new ParseSequence(
									new LexemeWrapper(LexToken.RightArrow),
									new TokenWrapper(ParseToken.Expression)
								)
							),
							new LexemeWrapper(LexToken.Colon),
							new ParseOptional(
								new TokenWrapper(ParseToken.FuncTypeComment)
							),
							new TokenWrapper(ParseToken.Block)
						)
					)
				},
				{
					ParseToken.FuncTypeComment,
					new ParseAny(
						new ParseSequence(
							new LexemeWrapper(LexToken.Newline),
							new LexemeWrapper(LexToken.TypeComment),
							new LookaheadRequire(new ParseSequence(
					new LexemeWrapper(LexToken.Newline),
					new LexemeWrapper(LexToken.Indent)
				))
						),
						new LexemeWrapper(LexToken.TypeComment)
					)
				},
				{
					ParseToken.Params, 
					new TokenWrapper(ParseToken.Parameters)
				},
				{
					ParseToken.Parameters,
					new ParseAny(
						new ParseSequence(
							new TokenWrapper(ParseToken.SlashNoDefault),
							new ParseOptional(new ParseRepeat(
								new TokenWrapper(ParseToken.ParamNoDefault)
							)),
							new ParseOptional(new ParseRepeat(
								new TokenWrapper(ParseToken.ParamWithDefault)
							)),
							new ParseOptional(
								new TokenWrapper(ParseToken.StarEtc)
							)
						),
						new ParseSequence(
							new TokenWrapper(ParseToken.SlashWithDefault),
							new ParseOptional(new ParseRepeat(
								new TokenWrapper(ParseToken.ParamWithDefault)
							)),
							new ParseOptional(
								new TokenWrapper(ParseToken.StarEtc)
							)
						),
						new ParseSequence(
							new ParseRepeat(
								new TokenWrapper(ParseToken.ParamNoDefault)
							),
							new ParseOptional(new ParseRepeat(
								new TokenWrapper(ParseToken.ParamWithDefault)
							)),
							new ParseOptional(
								new TokenWrapper(ParseToken.StarEtc)
							)
						),
						new ParseSequence(
							new ParseRepeat(
								new TokenWrapper(ParseToken.ParamWithDefault)
							),
							new ParseOptional(
								new TokenWrapper(ParseToken.StarEtc)
							)
						),
						new TokenWrapper(ParseToken.StarEtc)
					)
				},
				{
					ParseToken.SlashNoDefault,
					new ParseAny(
						new ParseSequence(
							new ParseRepeat(
								new TokenWrapper(ParseToken.ParamNoDefault)
							),
							new LexemeWrapper(LexToken.ForwardSlash),
							new LexemeWrapper(LexToken.Comma)
						),
						new ParseSequence(
							new ParseRepeat(
								new TokenWrapper(ParseToken.ParamNoDefault)
							),
							new LexemeWrapper(LexToken.ForwardSlash),
							new LookaheadRequire(new LexemeWrapper(LexToken.CloseBrackets))
						)
					)
				},
				{
					ParseToken.SlashWithDefault,
					new ParseAny(
						new ParseSequence(
							new ParseOptional(new ParseRepeat(
								new TokenWrapper(ParseToken.ParamNoDefault)
							)),
							new ParseRepeat(
								new TokenWrapper(ParseToken.ParamWithDefault)
							),
							new LexemeWrapper(LexToken.ForwardSlash),
							new LexemeWrapper(LexToken.Comma)
						),
						new ParseSequence(
							new ParseOptional(new ParseRepeat(
								new TokenWrapper(ParseToken.ParamNoDefault)
							)),
							new ParseRepeat(
								new TokenWrapper(ParseToken.ParamWithDefault)
							),
							new LexemeWrapper(LexToken.ForwardSlash),
							new LookaheadRequire(new LexemeWrapper(LexToken.CloseBrackets))
						)
					)
				},
				{
					ParseToken.StarEtc,
					new ParseAny(
						new ParseSequence(
							new LexemeWrapper(LexToken.Star),
							new TokenWrapper(ParseToken.ParamNoDefault),
							new ParseOptional(new ParseRepeat(
								new TokenWrapper(ParseToken.ParamMaybeDefault)
							)),
							new ParseOptional(
								new TokenWrapper(ParseToken.Keywords)
							)
						),
						new ParseSequence(
							new LexemeWrapper(LexToken.Star),
							new LexemeWrapper(LexToken.Comma),
							new ParseRepeat(
								new TokenWrapper(ParseToken.ParamMaybeDefault)
							),
							new ParseOptional(
								new TokenWrapper(ParseToken.Keywords)
							)
						),
						new TokenWrapper(ParseToken.Keywords)
					)
				},
				{
					ParseToken.Keywords, 
					new ParseSequence(
						new LexemeWrapper(LexToken.DoubleStar),
						new TokenWrapper(ParseToken.ParamNoDefault)
					)
				},
				{
					ParseToken.ParamNoDefault,
					new ParseAny(
						new ParseSequence(
							new TokenWrapper(ParseToken.Param),
							new LexemeWrapper(LexToken.Comma),
							new ParseOptional(
								new LexemeWrapper(LexToken.TypeComment)
							)
						),
						new ParseSequence(
							new TokenWrapper(ParseToken.Param),
							new ParseOptional(
								new LexemeWrapper(LexToken.TypeComment)
							),
							new LookaheadRequire(new LexemeWrapper(LexToken.CloseBrackets))
						)
					)
				},
				{
					ParseToken.ParamWithDefault,
					new ParseAny(
						new ParseSequence(
							new TokenWrapper(ParseToken.Param),
							new TokenWrapper(ParseToken.Default),
							new LexemeWrapper(LexToken.Comma),
							new ParseOptional(
								new LexemeWrapper(LexToken.TypeComment)
							)
						),
						new ParseSequence(
							new TokenWrapper(ParseToken.Param),
							new TokenWrapper(ParseToken.Default),
							new ParseOptional(
								new LexemeWrapper(LexToken.TypeComment)
							),
							new LookaheadRequire(new LexemeWrapper(LexToken.CloseBrackets))
						)
					)
				},
				{
					ParseToken.ParamMaybeDefault,
					new ParseAny(
						new ParseSequence(
							new TokenWrapper(ParseToken.Param),
							new ParseOptional(
								new TokenWrapper(ParseToken.Default)
							),
							new LexemeWrapper(LexToken.Comma),
							new ParseOptional(
								new LexemeWrapper(LexToken.TypeComment)
							)
						),
						new ParseSequence(
							new TokenWrapper(ParseToken.Param),
							new ParseOptional(
								new TokenWrapper(ParseToken.Default)
							),
							new ParseOptional(
								new LexemeWrapper(LexToken.TypeComment)
							),
							new LookaheadRequire(new LexemeWrapper(LexToken.CloseBrackets))
						)
					)
				},
				{
					ParseToken.Param, 
					new ParseSequence(
						new LexemeWrapper(LexToken.Name),
						new ParseOptional(
							new TokenWrapper(ParseToken.Annotation)
						)
					)
				},
				{
					ParseToken.Annotation, 
					new ParseSequence(
						new LexemeWrapper(LexToken.Colon),
						new TokenWrapper(ParseToken.Expression)
					)
				},
				{
					ParseToken.Default, 
					new ParseSequence(
						new LexemeWrapper(LexToken.Equals),
						new TokenWrapper(ParseToken.Expression)
					)
				},
				{
					ParseToken.Decorators, 
					new ParseRepeat(
						new ParseSequence(
							new LexemeWrapper(LexToken.At),
							new TokenWrapper(ParseToken.NamedExpression),
							new LexemeWrapper(LexToken.Newline)
						)
					)
				},
				{
					ParseToken.ClassDefinition,
					new ParseAny(
						new ParseSequence(
							new TokenWrapper(ParseToken.Decorators),
							new TokenWrapper(ParseToken.ClassDefinitionRaw)
						),
						new TokenWrapper(ParseToken.ClassDefinitionRaw)
					)
				},
				{
					ParseToken.ClassDefinitionRaw, 
					new ParseSequence(
						new LexemeWrapper(LexToken.Class),
						new LexemeWrapper(LexToken.Name),
						new ParseOptional(
							new ParseSequence(
								new LexemeWrapper(LexToken.OpenBrackets),
								new ParseOptional(
									new TokenWrapper(ParseToken.Arguments)
								),
								new LexemeWrapper(LexToken.CloseBrackets)
							)
						),
						new LexemeWrapper(LexToken.Colon),
						new TokenWrapper(ParseToken.Block)
					)
				},
				{
					ParseToken.Block,
					new ParseAny(
						new ParseSequence(
							new LexemeWrapper(LexToken.Newline),
							new LexemeWrapper(LexToken.Indent),
							new TokenWrapper(ParseToken.Statements),
							new LexemeWrapper(LexToken.Dedent)
						),
						new TokenWrapper(ParseToken.SimpleStatement)
					)
				},
				{
					ParseToken.StarExpressions,
					new ParseAny(
						new ParseSequence(
							new TokenWrapper(ParseToken.StarExpression),
							new ParseRepeat(
								new ParseSequence(
									new LexemeWrapper(LexToken.Comma),
									new TokenWrapper(ParseToken.StarExpression)
								)
							),
							new ParseOptional(
								new LexemeWrapper(LexToken.Comma)
							)
						),
						new ParseSequence(
							new TokenWrapper(ParseToken.StarExpression),
							new LexemeWrapper(LexToken.Comma)
						),
						new TokenWrapper(ParseToken.StarExpression)
					)
				},
				{
					ParseToken.StarExpression,
					new ParseAny(
						new ParseSequence(
							new LexemeWrapper(LexToken.Star),
							new TokenWrapper(ParseToken.BitwiseOr)
						),
						new TokenWrapper(ParseToken.Expression)
					)
				},
				{
					ParseToken.StarNamedExpressions, 
					new ParseSequence(
						new ParseSeparated(new LexemeWrapper(LexToken.Comma), new TokenWrapper(ParseToken.StarNamedExpression)),
						new ParseOptional(
							new LexemeWrapper(LexToken.Comma)
						)
					)
				},
				{
					ParseToken.StarNamedExpression,
					new ParseAny(
						new ParseSequence(
							new LexemeWrapper(LexToken.Star),
							new TokenWrapper(ParseToken.BitwiseOr)
						),
						new TokenWrapper(ParseToken.NamedExpression)
					)
				},
				{
					ParseToken.NamedExpression,
					new ParseAny(
						new ParseSequence(
							new LexemeWrapper(LexToken.Name),
							new LexemeWrapper(LexToken.Taurus),
							new Cut(),
							new TokenWrapper(ParseToken.Expression)
						),
						new ParseSequence(
							new TokenWrapper(ParseToken.Expression),
							new LookaheadRequireNot(new LexemeWrapper(LexToken.Taurus))
						)
					)
				},
				{
					ParseToken.AnnotatedRhs, 
					new ParseAny(
						new TokenWrapper(ParseToken.YieldExpression),
						new TokenWrapper(ParseToken.StarExpressions)
					)
				},
				{
					ParseToken.Expressions,
					new ParseAny(
						new ParseSequence(
							new TokenWrapper(ParseToken.Expression),
							new ParseRepeat(
								new ParseSequence(
									new LexemeWrapper(LexToken.Comma),
									new TokenWrapper(ParseToken.Expression)
								)
							),
							new ParseOptional(
								new LexemeWrapper(LexToken.Comma)
							)
						),
						new ParseSequence(
							new TokenWrapper(ParseToken.Expression),
							new LexemeWrapper(LexToken.Comma)
						),
						new TokenWrapper(ParseToken.Expression)
					)
				},
				{
					ParseToken.Expression,
					new ParseAny(
						new ParseSequence(
							new TokenWrapper(ParseToken.Disjunction),
							new LexemeWrapper(LexToken.If),
							new TokenWrapper(ParseToken.Disjunction),
							new LexemeWrapper(LexToken.Else),
							new TokenWrapper(ParseToken.Expression)
						),
						new TokenWrapper(ParseToken.Disjunction),
						new TokenWrapper(ParseToken.LambdaDefinition)
					)
				},
				{
					ParseToken.LambdaDefinition, 
					new ParseSequence(
						new LexemeWrapper(LexToken.Lambda),
						new ParseOptional(
							new TokenWrapper(ParseToken.LambdaParams)
						),
						new LexemeWrapper(LexToken.Colon),
						new TokenWrapper(ParseToken.Expression)
					)
				},
				{
					ParseToken.LambdaParams, 
					new TokenWrapper(ParseToken.LambdaParameters)
				},
				{
					ParseToken.LambdaParameters,
					new ParseAny(
						new ParseSequence(
							new TokenWrapper(ParseToken.LambdaSlashNoDefault),
							new ParseOptional(new ParseRepeat(
								new TokenWrapper(ParseToken.LambdaParamNoDefault)
							)),
							new ParseOptional(new ParseRepeat(
								new TokenWrapper(ParseToken.LambdaParamWithDefault)
							)),
							new ParseOptional(
								new TokenWrapper(ParseToken.LambdaStarEtc)
							)
						),
						new ParseSequence(
							new TokenWrapper(ParseToken.LambdaSlashWithDefault),
							new ParseOptional(new ParseRepeat(
								new TokenWrapper(ParseToken.LambdaParamWithDefault)
							)),
							new ParseOptional(
								new TokenWrapper(ParseToken.LambdaStarEtc)
							)
						),
						new ParseSequence(
							new ParseRepeat(
								new TokenWrapper(ParseToken.LambdaParamNoDefault)
							),
							new ParseOptional(new ParseRepeat(
								new TokenWrapper(ParseToken.LambdaParamWithDefault)
							)),
							new ParseOptional(
								new TokenWrapper(ParseToken.LambdaStarEtc)
							)
						),
						new ParseSequence(
							new ParseRepeat(
								new TokenWrapper(ParseToken.LambdaParamWithDefault)
							),
							new ParseOptional(
								new TokenWrapper(ParseToken.LambdaStarEtc)
							)
						),
						new TokenWrapper(ParseToken.LambdaStarEtc)
					)
				},
				{
					ParseToken.LambdaSlashNoDefault,
					new ParseAny(
						new ParseSequence(
							new ParseRepeat(
								new TokenWrapper(ParseToken.LambdaParamNoDefault)
							),
							new LexemeWrapper(LexToken.ForwardSlash),
							new LexemeWrapper(LexToken.Comma)
						),
						new ParseSequence(
							new ParseRepeat(
								new TokenWrapper(ParseToken.LambdaParamNoDefault)
							),
							new LexemeWrapper(LexToken.ForwardSlash),
							new LookaheadRequire(new LexemeWrapper(LexToken.Colon))
						)
					)
				},
				{
					ParseToken.LambdaSlashWithDefault,
					new ParseAny(
						new ParseSequence(
							new ParseOptional(new ParseRepeat(
								new TokenWrapper(ParseToken.LambdaParamNoDefault)
							)),
							new ParseRepeat(
								new TokenWrapper(ParseToken.LambdaParamWithDefault)
							),
							new LexemeWrapper(LexToken.ForwardSlash),
							new LexemeWrapper(LexToken.Comma)
						),
						new ParseSequence(
							new ParseOptional(new ParseRepeat(
								new TokenWrapper(ParseToken.LambdaParamNoDefault)
							)),
							new ParseRepeat(
								new TokenWrapper(ParseToken.LambdaParamWithDefault)
							),
							new LexemeWrapper(LexToken.ForwardSlash),
							new LookaheadRequire(new LexemeWrapper(LexToken.Colon))
						)
					)
				},
				{
					ParseToken.LambdaStarEtc,
					new ParseAny(
						new ParseSequence(
							new LexemeWrapper(LexToken.Star),
							new TokenWrapper(ParseToken.LambdaParamNoDefault),
							new ParseOptional(new ParseRepeat(
								new TokenWrapper(ParseToken.LambdaParamMaybeDefault)
							)),
							new ParseOptional(
								new TokenWrapper(ParseToken.LambdaKwds)
							)
						),
						new ParseSequence(
							new LexemeWrapper(LexToken.Star),
							new LexemeWrapper(LexToken.Comma),
							new ParseRepeat(
								new TokenWrapper(ParseToken.LambdaParamMaybeDefault)
							),
							new ParseOptional(
								new TokenWrapper(ParseToken.LambdaKwds)
							)
						),
						new TokenWrapper(ParseToken.LambdaKwds)
					)
				},
				{
					ParseToken.LambdaKwds, 
					new ParseSequence(
						new LexemeWrapper(LexToken.DoubleStar),
						new TokenWrapper(ParseToken.LambdaParamNoDefault)
					)
				},
				{
					ParseToken.LambdaParamNoDefault,
					new ParseAny(
						new ParseSequence(
							new TokenWrapper(ParseToken.LambdaParam),
							new LexemeWrapper(LexToken.Comma)
						),
						new ParseSequence(
							new TokenWrapper(ParseToken.LambdaParam),
							new LookaheadRequire(new LexemeWrapper(LexToken.Colon))
						)
					)
				},
				{
					ParseToken.LambdaParamWithDefault,
					new ParseAny(
						new ParseSequence(
							new TokenWrapper(ParseToken.LambdaParam),
							new TokenWrapper(ParseToken.Default),
							new LexemeWrapper(LexToken.Comma)
						),
						new ParseSequence(
							new TokenWrapper(ParseToken.LambdaParam),
							new TokenWrapper(ParseToken.Default),
							new LookaheadRequire(new LexemeWrapper(LexToken.Colon))
						)
					)
				},
				{
					ParseToken.LambdaParamMaybeDefault,
					new ParseAny(
						new ParseSequence(
							new TokenWrapper(ParseToken.LambdaParam),
							new ParseOptional(
								new TokenWrapper(ParseToken.Default)
							),
							new LexemeWrapper(LexToken.Comma)
						),
						new ParseSequence(
							new TokenWrapper(ParseToken.LambdaParam),
							new ParseOptional(
								new TokenWrapper(ParseToken.Default)
							),
							new LookaheadRequire(new LexemeWrapper(LexToken.Colon))
						)
					)
				},
				{
					ParseToken.LambdaParam, 
					new LexemeWrapper(LexToken.Name)
				},
				{
					ParseToken.Disjunction,
					new ParseAny(
						new ParseSequence(
							new TokenWrapper(ParseToken.Conjunction),
							new ParseRepeat(
								new ParseSequence(
									new LexemeWrapper(LexToken.Or),
									new TokenWrapper(ParseToken.Conjunction)
								)
							)
						),
						new TokenWrapper(ParseToken.Conjunction)
					)
				},
				{
					ParseToken.Conjunction,
					new ParseAny(
						new ParseSequence(
							new TokenWrapper(ParseToken.Inversion),
							new ParseRepeat(
								new ParseSequence(
									new LexemeWrapper(LexToken.And),
									new TokenWrapper(ParseToken.Inversion)
								)
							)
						),
						new TokenWrapper(ParseToken.Inversion)
					)
				},
				{
					ParseToken.Inversion,
					new ParseAny(
						new ParseSequence(
							new LexemeWrapper(LexToken.Not),
							new TokenWrapper(ParseToken.Inversion)
						),
						new TokenWrapper(ParseToken.Comparison)
					)
				},
				{
					ParseToken.Comparison,
					new ParseAny(
						new ParseSequence(
							new TokenWrapper(ParseToken.BitwiseOr),
							new ParseRepeat(
								new TokenWrapper(ParseToken.CompareOpBitwiseOrPair)
							)
						),
						new TokenWrapper(ParseToken.BitwiseOr)
					)
				},
				{
					ParseToken.CompareOpBitwiseOrPair,
					new ParseAny(
						new TokenWrapper(ParseToken.EqBitwiseOr),
						new TokenWrapper(ParseToken.NeqBitwiseOr),
						new TokenWrapper(ParseToken.LteBitwiseOr),
						new TokenWrapper(ParseToken.LtBitwiseOr),
						new TokenWrapper(ParseToken.GteBitwiseOr),
						new TokenWrapper(ParseToken.GtBitwiseOr),
						new TokenWrapper(ParseToken.NotInBitwiseOr),
						new TokenWrapper(ParseToken.InBitwiseOr),
						new TokenWrapper(ParseToken.IsNotBitwiseOr),
						new TokenWrapper(ParseToken.IsBitwiseOr)
					)
				},
				{
					ParseToken.EqBitwiseOr, 
					new ParseSequence(
						new LexemeWrapper(LexToken.DoubleEquals),
						new TokenWrapper(ParseToken.BitwiseOr)
					)
				},
				{
					ParseToken.NeqBitwiseOr, 
					new ParseSequence(
						new LexemeWrapper(LexToken.Neq),
						new TokenWrapper(ParseToken.BitwiseOr)
					)
				},
				{
					ParseToken.LteBitwiseOr, 
					new ParseSequence(
						new LexemeWrapper(LexToken.Leq),
						new TokenWrapper(ParseToken.BitwiseOr)
					)
				},
				{
					ParseToken.LtBitwiseOr, 
					new ParseSequence(
						new LexemeWrapper(LexToken.Lt),
						new TokenWrapper(ParseToken.BitwiseOr)
					)
				},
				{
					ParseToken.GteBitwiseOr, 
					new ParseSequence(
						new LexemeWrapper(LexToken.Geq),
						new TokenWrapper(ParseToken.BitwiseOr)
					)
				},
				{
					ParseToken.GtBitwiseOr, 
					new ParseSequence(
						new LexemeWrapper(LexToken.Gt),
						new TokenWrapper(ParseToken.BitwiseOr)
					)
				},
				{
					ParseToken.NotInBitwiseOr, 
					new ParseSequence(
						new LexemeWrapper(LexToken.Not),
						new LexemeWrapper(LexToken.In),
						new TokenWrapper(ParseToken.BitwiseOr)
					)
				},
				{
					ParseToken.InBitwiseOr, 
					new ParseSequence(
						new LexemeWrapper(LexToken.In),
						new TokenWrapper(ParseToken.BitwiseOr)
					)
				},
				{
					ParseToken.IsNotBitwiseOr, 
					new ParseSequence(
						new LexemeWrapper(LexToken.Is),
						new LexemeWrapper(LexToken.Not),
						new TokenWrapper(ParseToken.BitwiseOr)
					)
				},
				{
					ParseToken.IsBitwiseOr, 
					new ParseSequence(
						new LexemeWrapper(LexToken.Is),
						new TokenWrapper(ParseToken.BitwiseOr)
					)
				},
				{
					ParseToken.BitwiseOr,
					new ParseAny(
						new ParseSequence(
							new TokenWrapper(ParseToken.BitwiseOr),
							new LexemeWrapper(LexToken.Pipe),
							new TokenWrapper(ParseToken.BitwiseXor)
						),
						new TokenWrapper(ParseToken.BitwiseXor)
					)
				},
				{
					ParseToken.BitwiseXor,
					new ParseAny(
						new ParseSequence(
							new TokenWrapper(ParseToken.BitwiseXor),
							new LexemeWrapper(LexToken.Caret),
							new TokenWrapper(ParseToken.BitwiseAnd)
						),
						new TokenWrapper(ParseToken.BitwiseAnd)
					)
				},
				{
					ParseToken.BitwiseAnd,
					new ParseAny(
						new ParseSequence(
							new TokenWrapper(ParseToken.BitwiseAnd),
							new LexemeWrapper(LexToken.Ampersand),
							new TokenWrapper(ParseToken.ShiftExpression)
						),
						new TokenWrapper(ParseToken.ShiftExpression)
					)
				},
				{
					ParseToken.ShiftExpression,
					new ParseAny(
						new ParseSequence(
							new TokenWrapper(ParseToken.ShiftExpression),
							new LexemeWrapper(LexToken.DoubleAngleLeft),
							new TokenWrapper(ParseToken.Sum)
						),
						new ParseSequence(
							new TokenWrapper(ParseToken.ShiftExpression),
							new LexemeWrapper(LexToken.DoubleAngleRight),
							new TokenWrapper(ParseToken.Sum)
						),
						new TokenWrapper(ParseToken.Sum)
					)
				},
				{
					ParseToken.Sum,
					new ParseAny(
						new ParseSequence(
							new TokenWrapper(ParseToken.Sum),
							new LexemeWrapper(LexToken.Plus),
							new TokenWrapper(ParseToken.Term)
						),
						new ParseSequence(
							new TokenWrapper(ParseToken.Sum),
							new LexemeWrapper(LexToken.Minus),
							new TokenWrapper(ParseToken.Term)
						),
						new TokenWrapper(ParseToken.Term)
					)
				},
				{
					ParseToken.Term,
					new ParseAny(
						new ParseSequence(
							new TokenWrapper(ParseToken.Term),
							new LexemeWrapper(LexToken.Star),
							new TokenWrapper(ParseToken.Factor)
						),
						new ParseSequence(
							new TokenWrapper(ParseToken.Term),
							new LexemeWrapper(LexToken.ForwardSlash),
							new TokenWrapper(ParseToken.Factor)
						),
						new ParseSequence(
							new TokenWrapper(ParseToken.Term),
							new LexemeWrapper(LexToken.DoubleForwardSlash),
							new TokenWrapper(ParseToken.Factor)
						),
						new ParseSequence(
							new TokenWrapper(ParseToken.Term),
							new LexemeWrapper(LexToken.Percent),
							new TokenWrapper(ParseToken.Factor)
						),
						new ParseSequence(
							new TokenWrapper(ParseToken.Term),
							new LexemeWrapper(LexToken.At),
							new TokenWrapper(ParseToken.Factor)
						),
						new TokenWrapper(ParseToken.Factor)
					)
				},
				{
					ParseToken.Factor,
					new ParseAny(
						new ParseSequence(
							new LexemeWrapper(LexToken.Plus),
							new TokenWrapper(ParseToken.Factor)
						),
						new ParseSequence(
							new LexemeWrapper(LexToken.Minus),
							new TokenWrapper(ParseToken.Factor)
						),
						new ParseSequence(
							new LexemeWrapper(LexToken.Tilde),
							new TokenWrapper(ParseToken.Factor)
						),
						new TokenWrapper(ParseToken.Power)
					)
				},
				{
					ParseToken.Power,
					new ParseAny(
						new ParseSequence(
							new TokenWrapper(ParseToken.AwaitPrimary),
							new LexemeWrapper(LexToken.DoubleStar),
							new TokenWrapper(ParseToken.Factor)
						),
						new TokenWrapper(ParseToken.AwaitPrimary)
					)
				},
				{
					ParseToken.AwaitPrimary,
					new ParseAny(
						new ParseSequence(
							new LexemeWrapper(LexToken.Await),
							new TokenWrapper(ParseToken.Primary)
						),
						new TokenWrapper(ParseToken.Primary)
					)
				},
				{
					ParseToken.Primary,
					new ParseAny(
						// TODO: new TokenWrapper(ParseToken.InvalidPrimary),
						new ParseSequence(
							new TokenWrapper(ParseToken.Primary),
							new LexemeWrapper(LexToken.Dot),
							new LexemeWrapper(LexToken.Name)
						),
						new ParseSequence(
							new TokenWrapper(ParseToken.Primary),
							new TokenWrapper(ParseToken.GeneratorExpression)
						),
						new ParseSequence(
							new TokenWrapper(ParseToken.Primary),
							new LexemeWrapper(LexToken.OpenBrackets),
							new ParseOptional(
								new TokenWrapper(ParseToken.Arguments)
							),
							new LexemeWrapper(LexToken.CloseBrackets)
						),
						new ParseSequence(
							new TokenWrapper(ParseToken.Primary),
							new LexemeWrapper(LexToken.OpenSqBrackets),
							new TokenWrapper(ParseToken.Slices),
							new LexemeWrapper(LexToken.CloseSqBrackets)
						),
						new TokenWrapper(ParseToken.Atom)
					)
				},
				{
					ParseToken.Slices,
					new ParseAny(
						new ParseSequence(
							new TokenWrapper(ParseToken.Slice),
							new LookaheadRequireNot(new LexemeWrapper(LexToken.Comma))
						),
						new ParseSequence(
							new ParseSeparated(new LexemeWrapper(LexToken.Comma), new TokenWrapper(ParseToken.Slice)),
							new ParseOptional(
								new LexemeWrapper(LexToken.Comma)
							)
						)
					)
				},
				{
					ParseToken.Slice,
					new ParseAny(
						new ParseSequence(
							new ParseOptional(
								new TokenWrapper(ParseToken.Expression)
							),
							new LexemeWrapper(LexToken.Colon),
							new ParseOptional(
								new TokenWrapper(ParseToken.Expression)
							),
							new ParseOptional(
								new ParseSequence(
									new LexemeWrapper(LexToken.Colon),
									new ParseOptional(
										new TokenWrapper(ParseToken.Expression)
									)
								)
							)
						),
						new TokenWrapper(ParseToken.Expression)
					)
				},
				{
					ParseToken.Atom,
					new ParseAny(
						new LexemeWrapper(LexToken.Name),
						new LexemeWrapper(LexToken.True),
						new LexemeWrapper(LexToken.False),
						new LexemeWrapper(LexToken.None),
						new LexemeWrapper(LexToken.PegParserAtom),
						new TokenWrapper(ParseToken.Strings),
						new LexemeWrapper(LexToken.Number),
						new ParseAny(
							new TokenWrapper(ParseToken.Tuple),
							new TokenWrapper(ParseToken.Group),
							new TokenWrapper(ParseToken.GeneratorExpression)
						),
						new ParseAny(
							new TokenWrapper(ParseToken.List),
							new TokenWrapper(ParseToken.ListComprehension)
						),
						new ParseAny(
							new TokenWrapper(ParseToken.Dictionary),
							new TokenWrapper(ParseToken.Set),
							new TokenWrapper(ParseToken.DictionaryComprehension),
							new TokenWrapper(ParseToken.SetComprehension)
						),
						new LexemeWrapper(LexToken.Ellipsis)
					)
				},
				{
					ParseToken.Strings, 
					new ParseRepeat(
						new LexemeWrapper(LexToken.String)
					)
				},
				{
					ParseToken.List, 
					new ParseSequence(
						new LexemeWrapper(LexToken.OpenSqBrackets),
						new ParseOptional(
							new TokenWrapper(ParseToken.StarNamedExpressions)
						),
						new LexemeWrapper(LexToken.CloseSqBrackets)
					)
				},
				{
					ParseToken.ListComprehension, 
					new ParseSequence(
						new LexemeWrapper(LexToken.OpenSqBrackets),
						new TokenWrapper(ParseToken.NamedExpression),
						new Cut(),
						new TokenWrapper(ParseToken.ForIfClauses),
						new LexemeWrapper(LexToken.CloseSqBrackets)
					)
				},
				{
					ParseToken.Tuple, 
					new ParseSequence(
						new LexemeWrapper(LexToken.OpenBrackets),
						new ParseOptional(
							new ParseSequence(
								new TokenWrapper(ParseToken.StarNamedExpression),
								new LexemeWrapper(LexToken.Comma),
								new ParseOptional(
									new TokenWrapper(ParseToken.StarNamedExpressions)
								)
							)
						),
						new LexemeWrapper(LexToken.CloseBrackets)
					)
				},
				{
					ParseToken.Group, 
					new ParseSequence(
						new LexemeWrapper(LexToken.OpenBrackets),
						new ParseAny(
							new TokenWrapper(ParseToken.YieldExpression),
							new TokenWrapper(ParseToken.NamedExpression)
						),
						new LexemeWrapper(LexToken.CloseBrackets)
					)
				},
				{
					ParseToken.GeneratorExpression, 
					new ParseSequence(
						new LexemeWrapper(LexToken.OpenBrackets),
						new TokenWrapper(ParseToken.NamedExpression),
						new Cut(),
						new TokenWrapper(ParseToken.ForIfClauses),
						new LexemeWrapper(LexToken.CloseBrackets)
					)
				},
				{
					ParseToken.Set, 
					new ParseSequence(
						new LexemeWrapper(LexToken.OpenCurlyBrackets),
						new TokenWrapper(ParseToken.StarNamedExpressions),
						new LexemeWrapper(LexToken.CloseCurlyBrackets)
					)
				},
				{
					ParseToken.SetComprehension, 
					new ParseSequence(
						new LexemeWrapper(LexToken.OpenCurlyBrackets),
						new TokenWrapper(ParseToken.NamedExpression),
						new Cut(),
						new TokenWrapper(ParseToken.ForIfClauses),
						new LexemeWrapper(LexToken.CloseCurlyBrackets)
					)
				},
				{
					ParseToken.Dictionary, 
					new ParseSequence(
						new LexemeWrapper(LexToken.OpenCurlyBrackets),
						new ParseOptional(
							new TokenWrapper(ParseToken.DoubleStarredKeyValuePairs)
						),
						new LexemeWrapper(LexToken.CloseCurlyBrackets)
					)
				},
				{
					ParseToken.DictionaryComprehension, 
					new ParseSequence(
						new LexemeWrapper(LexToken.OpenCurlyBrackets),
						new TokenWrapper(ParseToken.KeyValuePair),
						new TokenWrapper(ParseToken.ForIfClauses),
						new LexemeWrapper(LexToken.CloseCurlyBrackets)
					)
				},
				{
					ParseToken.DoubleStarredKeyValuePairs, 
					new ParseSequence(
						new ParseSeparated(new LexemeWrapper(LexToken.Comma), new TokenWrapper(ParseToken.DoubleStarredKeyValuePair)),
						new ParseOptional(
							new LexemeWrapper(LexToken.Comma)
						)
					)
				},
				{
					ParseToken.DoubleStarredKeyValuePair,
					new ParseAny(
						new ParseSequence(
							new LexemeWrapper(LexToken.DoubleStar),
							new TokenWrapper(ParseToken.BitwiseOr)
						),
						new TokenWrapper(ParseToken.KeyValuePair)
					)
				},
				{
					ParseToken.KeyValuePair, 
					new ParseSequence(
						new TokenWrapper(ParseToken.Expression),
						new LexemeWrapper(LexToken.Colon),
						new TokenWrapper(ParseToken.Expression)
					)
				},
				{
					ParseToken.ForIfClauses, 
					new ParseRepeat(
						new TokenWrapper(ParseToken.ForIfClause)
					)
				},
				{
					ParseToken.ForIfClause,
					new ParseAny(
						new ParseSequence(
							new LexemeWrapper(LexToken.Async),
							new LexemeWrapper(LexToken.For),
							new TokenWrapper(ParseToken.StarTargets),
							new LexemeWrapper(LexToken.In),
							new Cut(),
							new TokenWrapper(ParseToken.Disjunction),
							new ParseOptional(new ParseRepeat(
								new ParseSequence(
									new LexemeWrapper(LexToken.If),
									new TokenWrapper(ParseToken.Disjunction)
								)
							))
						),
						new ParseSequence(
							new LexemeWrapper(LexToken.For),
							new TokenWrapper(ParseToken.StarTargets),
							new LexemeWrapper(LexToken.In),
							new Cut(),
							new TokenWrapper(ParseToken.Disjunction),
							new ParseOptional(new ParseRepeat(
								new ParseSequence(
									new LexemeWrapper(LexToken.If),
									new TokenWrapper(ParseToken.Disjunction)
								)
							))
						)
					)
				},
				{
					ParseToken.YieldExpression,
					new ParseAny(
						new ParseSequence(
							new LexemeWrapper(LexToken.Yield),
							new LexemeWrapper(LexToken.From),
							new TokenWrapper(ParseToken.Expression)
						),
						new ParseSequence(
							new LexemeWrapper(LexToken.Yield),
							new ParseOptional(
								new TokenWrapper(ParseToken.StarExpressions)
							)
						)
					)
				},
				{
					ParseToken.Arguments, 
					new ParseSequence(
						new TokenWrapper(ParseToken.Args),
						new ParseOptional(
							new LexemeWrapper(LexToken.Comma)
						),
						new LookaheadRequire(new LexemeWrapper(LexToken.CloseBrackets))
					)
				},
				{
					ParseToken.Args,
					new ParseAny(
						new ParseSequence(
							new ParseSeparated(new LexemeWrapper(LexToken.Comma), new ParseAny(
					new TokenWrapper(ParseToken.StarredExpression),
					new ParseSequence(
						new TokenWrapper(ParseToken.NamedExpression),
						new LookaheadRequireNot(new LexemeWrapper(LexToken.Equals))
					)
				)),
							new ParseOptional(
								new ParseSequence(
									new LexemeWrapper(LexToken.Comma),
									new TokenWrapper(ParseToken.KwArgs)
								)
							)
						),
						new TokenWrapper(ParseToken.KwArgs)
					)
				},
				{
					ParseToken.KwArgs,
					new ParseAny(
						new ParseSequence(
							new ParseSeparated(new LexemeWrapper(LexToken.Comma), new TokenWrapper(ParseToken.KwArgOrStarred)),
							new LexemeWrapper(LexToken.Comma),
							new ParseSeparated(new LexemeWrapper(LexToken.Comma), new TokenWrapper(ParseToken.KwArgOrDoubleStarred))
						),
						new ParseSeparated(new LexemeWrapper(LexToken.Comma), new TokenWrapper(ParseToken.KwArgOrStarred)),
						new ParseSeparated(new LexemeWrapper(LexToken.Comma), new TokenWrapper(ParseToken.KwArgOrDoubleStarred))
					)
				},
				{
					ParseToken.StarredExpression, 
					new ParseSequence(
						new LexemeWrapper(LexToken.Star),
						new TokenWrapper(ParseToken.Expression)
					)
				},
				{
					ParseToken.KwArgOrStarred,
					new ParseAny(
						new ParseSequence(
							new LexemeWrapper(LexToken.Name),
							new LexemeWrapper(LexToken.Equals),
							new TokenWrapper(ParseToken.Expression)
						),
						new TokenWrapper(ParseToken.StarredExpression)
					)
				},
				{
					ParseToken.KwArgOrDoubleStarred,
					new ParseAny(
						new ParseSequence(
							new LexemeWrapper(LexToken.Name),
							new LexemeWrapper(LexToken.Equals),
							new TokenWrapper(ParseToken.Expression)
						),
						new ParseSequence(
							new LexemeWrapper(LexToken.DoubleStar),
							new TokenWrapper(ParseToken.Expression)
						)
					)
				},
				{
					ParseToken.StarTargets,
					new ParseAny(
						new ParseSequence(
							new TokenWrapper(ParseToken.StarTarget),
							new LookaheadRequireNot(new LexemeWrapper(LexToken.Comma))
						),
						new ParseSequence(
							new TokenWrapper(ParseToken.StarTarget),
							new ParseOptional(new ParseRepeat(
								new ParseSequence(
									new LexemeWrapper(LexToken.Comma),
									new TokenWrapper(ParseToken.StarTarget)
								)
							)),
							new ParseOptional(
								new LexemeWrapper(LexToken.Comma)
							)
						)
					)
				},
				{
					ParseToken.StarTargetsListSeq, 
					new ParseSequence(
						new ParseSeparated(new LexemeWrapper(LexToken.Comma), new TokenWrapper(ParseToken.StarTarget)),
						new ParseOptional(
							new LexemeWrapper(LexToken.Comma)
						)
					)
				},
				{
					ParseToken.StarTargetsTupleSeq,
					new ParseAny(
						new ParseSequence(
							new TokenWrapper(ParseToken.StarTarget),
							new ParseRepeat(
								new ParseSequence(
									new LexemeWrapper(LexToken.Comma),
									new TokenWrapper(ParseToken.StarTarget)
								)
							),
							new ParseOptional(
								new LexemeWrapper(LexToken.Comma)
							)
						),
						new ParseSequence(
							new TokenWrapper(ParseToken.StarTarget),
							new LexemeWrapper(LexToken.Comma)
						)
					)
				},
				{
					ParseToken.StarTarget,
					new ParseAny(
						new ParseSequence(
							new LexemeWrapper(LexToken.Star),
							new ParseSequence(
								new LookaheadRequireNot(new LexemeWrapper(LexToken.Star)),
								new TokenWrapper(ParseToken.StarTarget)
							)
						),
						new TokenWrapper(ParseToken.TargetWithStarAtom)
					)
				},
				{
					ParseToken.TargetWithStarAtom,
					new ParseAny(
						new ParseSequence(
							new TokenWrapper(ParseToken.TPrimary),
							new LexemeWrapper(LexToken.Dot),
							new LexemeWrapper(LexToken.Name),
							new LookaheadRequireNot(new TokenWrapper(ParseToken.TLookahead))
						),
						new ParseSequence(
							new TokenWrapper(ParseToken.TPrimary),
							new LexemeWrapper(LexToken.OpenSqBrackets),
							new TokenWrapper(ParseToken.Slices),
							new LexemeWrapper(LexToken.CloseSqBrackets),
							new LookaheadRequireNot(new TokenWrapper(ParseToken.TLookahead))
						),
						new TokenWrapper(ParseToken.StarAtom)
					)
				},
				{
					ParseToken.StarAtom,
					new ParseAny(
						new LexemeWrapper(LexToken.Name),
						new ParseSequence(
							new LexemeWrapper(LexToken.OpenBrackets),
							new TokenWrapper(ParseToken.TargetWithStarAtom),
							new LexemeWrapper(LexToken.CloseBrackets)
						),
						new ParseSequence(
							new LexemeWrapper(LexToken.OpenBrackets),
							new ParseOptional(
								new TokenWrapper(ParseToken.StarTargetsTupleSeq)
							),
							new LexemeWrapper(LexToken.CloseBrackets)
						),
						new ParseSequence(
							new LexemeWrapper(LexToken.OpenSqBrackets),
							new ParseOptional(
								new TokenWrapper(ParseToken.StarTargetsListSeq)
							),
							new LexemeWrapper(LexToken.CloseSqBrackets)
						)
					)
				},
				{
					ParseToken.SingleTarget,
					new ParseAny(
						new TokenWrapper(ParseToken.SingleSubscriptAttributeTarget),
						new LexemeWrapper(LexToken.Name),
						new ParseSequence(
							new LexemeWrapper(LexToken.OpenBrackets),
							new TokenWrapper(ParseToken.SingleTarget),
							new LexemeWrapper(LexToken.CloseBrackets)
						)
					)
				},
				{
					ParseToken.SingleSubscriptAttributeTarget,
					new ParseAny(
						new ParseSequence(
							new TokenWrapper(ParseToken.TPrimary),
							new LexemeWrapper(LexToken.Dot),
							new LexemeWrapper(LexToken.Name),
							new LookaheadRequireNot(new TokenWrapper(ParseToken.TLookahead))
						),
						new ParseSequence(
							new TokenWrapper(ParseToken.TPrimary),
							new LexemeWrapper(LexToken.OpenSqBrackets),
							new TokenWrapper(ParseToken.Slices),
							new LexemeWrapper(LexToken.CloseSqBrackets),
							new LookaheadRequireNot(new TokenWrapper(ParseToken.TLookahead))
						)
					)
				},
				{
					ParseToken.DelTargets, 
					new ParseSequence(
						new ParseSeparated(new LexemeWrapper(LexToken.Comma), new TokenWrapper(ParseToken.DelTarget)),
						new ParseOptional(
							new LexemeWrapper(LexToken.Comma)
						)
					)
				},
				{
					ParseToken.DelTarget,
					new ParseAny(
						new ParseSequence(
							new TokenWrapper(ParseToken.TPrimary),
							new LexemeWrapper(LexToken.Dot),
							new LexemeWrapper(LexToken.Name),
							new LookaheadRequireNot(new TokenWrapper(ParseToken.TLookahead))
						),
						new ParseSequence(
							new TokenWrapper(ParseToken.TPrimary),
							new LexemeWrapper(LexToken.OpenSqBrackets),
							new TokenWrapper(ParseToken.Slices),
							new LexemeWrapper(LexToken.CloseSqBrackets),
							new LookaheadRequireNot(new TokenWrapper(ParseToken.TLookahead))
						),
						new TokenWrapper(ParseToken.DelTAtom)
					)
				},
				{
					ParseToken.DelTAtom,
					new ParseAny(
						new LexemeWrapper(LexToken.Name),
						new ParseSequence(
							new LexemeWrapper(LexToken.OpenBrackets),
							new TokenWrapper(ParseToken.DelTarget),
							new LexemeWrapper(LexToken.CloseBrackets)
						),
						new ParseSequence(
							new LexemeWrapper(LexToken.OpenBrackets),
							new ParseOptional(
								new TokenWrapper(ParseToken.DelTargets)
							),
							new LexemeWrapper(LexToken.CloseBrackets)
						),
						new ParseSequence(
							new LexemeWrapper(LexToken.OpenSqBrackets),
							new ParseOptional(
								new TokenWrapper(ParseToken.DelTargets)
							),
							new LexemeWrapper(LexToken.CloseSqBrackets)
						)
					)
				},
				{
					ParseToken.TPrimary,
					new ParseAny(
						new ParseSequence(
							new TokenWrapper(ParseToken.TPrimary),
							new LexemeWrapper(LexToken.Dot),
							new LexemeWrapper(LexToken.Name),
							new LookaheadRequire(new TokenWrapper(ParseToken.TLookahead))
						),
						new ParseSequence(
							new TokenWrapper(ParseToken.TPrimary),
							new LexemeWrapper(LexToken.OpenSqBrackets),
							new TokenWrapper(ParseToken.Slices),
							new LexemeWrapper(LexToken.CloseSqBrackets),
							new LookaheadRequire(new TokenWrapper(ParseToken.TLookahead))
						),
						new ParseSequence(
							new TokenWrapper(ParseToken.TPrimary),
							new TokenWrapper(ParseToken.GeneratorExpression),
							new LookaheadRequire(new TokenWrapper(ParseToken.TLookahead))
						),
						new ParseSequence(
							new TokenWrapper(ParseToken.TPrimary),
							new LexemeWrapper(LexToken.OpenBrackets),
							new ParseOptional(
								new TokenWrapper(ParseToken.Arguments)
							),
							new LexemeWrapper(LexToken.CloseBrackets),
							new LookaheadRequire(new TokenWrapper(ParseToken.TLookahead))
						),
						new ParseSequence(
							new TokenWrapper(ParseToken.Atom),
							new LookaheadRequire(new TokenWrapper(ParseToken.TLookahead))
						)
					)
				},
				{
					ParseToken.TLookahead, 
					new ParseAny(
						new LexemeWrapper(LexToken.OpenBrackets),
						new LexemeWrapper(LexToken.OpenSqBrackets),
						new LexemeWrapper(LexToken.Dot)
					)
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
            ParseTreeNode.PrettyPrint(astNode);
        }
    }
}