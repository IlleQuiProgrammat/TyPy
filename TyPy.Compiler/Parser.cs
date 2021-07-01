using System.Collections.Generic;
using TyPy.Compiler.LexingConstructs;
using TyPy.Compiler.ParsingConstructs;

namespace TyPy.Compiler
{
    public class Parser
    {
        public PipelineConfiguration Configuration { get; set; }

        public Parser(PipelineConfiguration configuration)
        {
            Configuration = configuration;
        }

        public ParseTreeNode Parse(List<Lexeme> lexemes)
        {
            if (!Configuration.Grammar.ContainsKey(Configuration.CompilationUnitToken))
            {
                throw new ParseException(
                    "The Configuration's CompilationUnitToken was not found in the specified grammar.");
            }

            var rootParse = Configuration.Grammar[Configuration.CompilationUnitToken];
            ParseTreeNode parseTreeNode;
            if (!rootParse.TryParse(lexemes.ToArray(), Configuration, out parseTreeNode))
            {
                throw new ParseException("Parsing failed somewhere.");
            }

            return parseTreeNode;
        }
    }
}