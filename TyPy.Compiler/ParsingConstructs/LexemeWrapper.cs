using System;
using TyPy.Compiler.LexingConstructs;

namespace TyPy.Compiler.ParsingConstructs
{
    public class LexemeWrapper : IParsable
    {
        public LexToken Token { get; set; }
        public LexemeWrapper(LexToken token)
        {
            Token = token;
        }
        
        public bool TryParse(ArraySegment<Lexeme> lexemes, PipelineConfiguration configuration,
            out AstNode astNode)
        {
            var i = 0;
            if (i >= lexemes.Count)
            {
                astNode = null;
                return false;
            }
            while (configuration.DefaultSkipLexemes.Contains(lexemes[i].Token) && lexemes[i].Token != Token)
            {
                i++;
                if (i >= lexemes.Count)
                {
                    astNode = null;
                    return false;
                }
            }

            if (lexemes[i].Token == Token)
            {
                astNode = new AstNode {Lexeme = lexemes[i], LexemeCount = i + 1};
                return true;
            }
            
            astNode = null;
            return false;
        }
    }
}