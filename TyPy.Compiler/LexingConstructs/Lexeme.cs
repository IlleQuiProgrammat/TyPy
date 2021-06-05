namespace TyPy.Compiler.LexingConstructs
{
    public class Lexeme
    {
        public LexToken Token { get; }
        public string Content { get; }

        public Lexeme(LexToken token, string content)
        {
            Token = token;
            Content = content;
        }
    }
}