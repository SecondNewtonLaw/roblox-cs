﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace RobloxCS
{
    public abstract class BaseTransformer : CSharpSyntaxRewriter
    {
        protected SyntaxNode _root;
        protected readonly SyntaxTree _tree;
        protected readonly ConfigData _config;

        public BaseTransformer(SyntaxTree tree, ConfigData config)
        {
            _root = tree.GetRoot();
            _tree = tree;
            _config = config;
        }

        public SyntaxTree TransformTree()
        {
            return _tree.WithRootAndOptions(Visit(_root), _tree.Options);
        }

        protected string? TryGetName(SyntaxNode node)
        {
            return Utility.GetNamesFromNode(node).FirstOrDefault();
        }

        protected string GetName(SyntaxNode node)
        {
            return Utility.GetNamesFromNode(node).First();
        }

        protected SyntaxToken CreateIdentifierToken(string text, string? valueText = null, SyntaxTriviaList? trivia = null)
        {
            var triviaList = trivia ?? SyntaxFactory.TriviaList();
            return SyntaxFactory.VerbatimIdentifier(triviaList, text, valueText ?? text, triviaList);
        }
    }
}
