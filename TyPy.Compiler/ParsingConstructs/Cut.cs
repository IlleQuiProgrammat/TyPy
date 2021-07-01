using System;
using TyPy.Compiler.LexingConstructs;

namespace TyPy.Compiler.ParsingConstructs
{
    public class Cut : IParsable
    {
        public bool TryParse(ArraySegment<Lexeme> lexemes, PipelineConfiguration configuration, out ParseTreeNode parseTreeNode)
        {
            throw new NotImplementedException();
        }
    }
}