using System;

namespace TyPy.Compiler.LexingConstructs
{
    public class LexException : Exception
    {
        public LexException() : base()
        {
        }
    
        public LexException(string errorMessage) : base(errorMessage)
        {
        }
    }
}