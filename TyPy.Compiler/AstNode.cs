using System;
using System.Collections.Generic;
using TyPy.Compiler.LexingConstructs;
using TyPy.Compiler.ParsingConstructs;

namespace TyPy.Compiler
{
    public class AstNode
    {
        public AstNode Parent { get; set; }
        public ParseToken Token { get; set; } = ParseToken.Anonymous;
        public List<AstNode> Children { get; set; } = new();
        private int _lexemeCount;
        public int LexemeCount
        {
            get => _lexemeCount;
            set
            {
                if (Parent is not null) Parent.LexemeCount += value - _lexemeCount;
                _lexemeCount = value;
            }
        }

        public Lexeme Lexeme { get; set; }
        public bool IsLeaf => Lexeme is not null;

        public void AppendNode(AstNode node)
        {
            if (node == null)
            {
                return;
            }

            if (IsLeaf)
            {
                throw new InvalidOperationException("Cannot add a child node to a leaf.");
            }

            node.Parent = this;
            Children.Add(node);
            LexemeCount += node.LexemeCount;
        }
    }
}