using System;

namespace TyPy.Compiler.ParsingConstructs
{
    public class ParseException : Exception
    {
        public ParseException() : base()
        {
        }
        
        public ParseException(string errorMessage) : base(errorMessage)
        {
        }
    }
}