using System;
using System.Collections.Generic;
using TyPy.Compiler.LexingConstructs;

namespace TyPy.Compiler
{
    public class Lexer
    {
        public PipelineConfiguration Configuration { get; set; }

        public Lexer(PipelineConfiguration configuration)
        {
            Configuration = configuration;
        }

        public List<Lexeme> Lex(string code)
        {
            var characterPosition = 0;
            var lexemes = new List<Lexeme>();
            while (characterPosition < code.Length)
            {
                var successful = false;
                foreach (var lexeme in Configuration.LexicalSyntax)
                {
                    var match = lexeme.Value.Match(code, characterPosition);
                    if (match.Success && match.Index == characterPosition)
                    {
                        successful = true;
                        lexemes.Add(new Lexeme(lexeme.Key, match.Value));
                        characterPosition += match.Length;
                        break;
                    }
                }

                if (!successful)
                {
                    throw new LexException("No match found");
                }
            }

            return lexemes;
        }
    }
}