using System.Collections.Generic;
using System.Text.RegularExpressions;
using TyPy.Compiler.LexingConstructs;
using TyPy.Compiler.ParsingConstructs;

namespace TyPy.Compiler
{
    public class PipelineConfiguration
    {
        public Dictionary<LexToken, Regex> LexicalSyntax { get; set; }
        public Dictionary<ParseToken, IParsable> Grammar { get; set; }
        public ParseToken CompilationUnitToken { get; set; }
        public HashSet<LexToken> DefaultSkipLexemes { get; set; }
    }
}