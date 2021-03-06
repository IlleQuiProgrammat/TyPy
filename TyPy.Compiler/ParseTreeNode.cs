using System;
using System.Collections.Generic;
using System.Linq;
using TyPy.Compiler.LexingConstructs;
using TyPy.Compiler.ParsingConstructs;

namespace TyPy.Compiler
{
    /// <summary>
    /// Represents the Parse Tree (AST) generated by the parser.
    /// </summary>
    public class ParseTreeNode
    {
        /// <summary>
        /// The parent node in the AST, null if it is the root
        /// </summary>
        public ParseTreeNode Parent { get; set; }
        /// <summary>
        /// The explicit token that was used to create this. Defaults to anonymous and is overridden by Token Wrapping
        /// see <see cref="TokenWrapper" />.
        /// </summary>
        public ParseToken Token { get; set; } = ParseToken.Anonymous;
        /// <summary>
        /// The child nodes parsed from the grammar. Data must not be directly set and should instead go through
        /// <see cref="AppendNode" />
        /// </summary>
        public List<ParseTreeNode> Children { get; set; } = new();
        private int _lexemeCount;
        /// <summary>
        /// The total number of lexemes that were parsed to create the following node. For lexeme nodes this may be more
        /// than one due to the ability for the stream to skip tokens. This is helpful as it allows us to know how far
        /// to seek in the lexeme stream in <see cref="ParseSequence"/> and <see cref="ParseRepeat" />
        /// Setting propagates this value up the tree using the Parent reference. This ensures that we can efficiently
        /// query the number of lexemes all descendants of a node has.
        /// </summary>
        public int LexemeCount
        {
            get => _lexemeCount;
            set
            {
                if (Parent is not null) Parent.LexemeCount += value - _lexemeCount;
                _lexemeCount = value;
            }
        }
        /// <summary>
        /// If the node has come from the parsing of a lexeme, then the lexeme that was parsed along with the string at
        /// the position which it was parsed from is stored in this object. By definition, this only occurs at the leaf
        /// nodes of the abstract syntax tree.
        /// </summary>
        public Lexeme Lexeme { get; set; }
        /// <summary>
        /// Determines whether or not the current node is a leaf in the abstract syntax tree.
        /// </summary>
        public bool IsLeaf => Lexeme is not null;

        /// <summary>
        /// Adds a node as a child to the current node, updating lexeme counts and parents. This should be used over
        /// manually adding to the children.
        /// </summary>
        /// <param name="node">
        /// The node that should be added to the abstract syntax tree. If null, for example for optional parameters,
        /// this function has no effect.
        /// </param>
        /// <exception cref="InvalidOperationException">Occurs when you attempt to add a node to a leaf</exception>
        public void AppendNode(ParseTreeNode node)
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

        public static void PrettyPrint(ParseTreeNode node, string indent = "", bool isLast = true)
        {
            var marker = isLast ? "└───" : "├───";
            Console.Write(indent);
            Console.Write(marker);
            Console.Write(node.Token);

            if (node.IsLeaf)
            {
                Console.Write($"; Lexeme: {node.Lexeme.Token} (\"{node.Lexeme.Content}\") ");
            }
            
            Console.WriteLine();
            
            var lastChild = node.Children.LastOrDefault();
            indent += isLast ? "    " : "│   ";
            
            foreach (var child in node.Children)
            {
                PrettyPrint(child, indent, child == lastChild);
            }
        }
    }
}