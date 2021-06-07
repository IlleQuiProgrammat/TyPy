using System;

namespace TyPy.Compiler.ParsingConstructs
{
    /// <summary>
    /// Base class that represents a parsing error.
    /// </summary>
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