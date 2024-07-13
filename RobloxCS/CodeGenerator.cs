﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace RobloxCS
{
    internal sealed class CodeGenerator : CSharpSyntaxWalker
    {
        private readonly List<SyntaxKind> _memberParentSyntaxes = new List<SyntaxKind>([
            SyntaxKind.NamespaceDeclaration,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.InterfaceDeclaration,
            SyntaxKind.StructDeclaration
        ]);

        private readonly SyntaxTree _tree;
        private readonly ConfigData _config;
        private readonly CSharpCompilation _compiler;
        private readonly SemanticModel _semanticModel;
        private readonly INamespaceSymbol _globalNamespace;
        private readonly INamespaceSymbol _runtimeLibNamespace;
        private readonly int _indentSize;

        private readonly StringBuilder _output = new StringBuilder();
        private int _indent = 0;

        public CodeGenerator(SyntaxTree tree, CSharpCompilation compiler, ConfigData config, int indentSize = 4)
        {
            _tree = tree;
            _config = config;
            _compiler = compiler;
            _semanticModel = compiler.GetSemanticModel(tree);
            _globalNamespace = compiler.GlobalNamespace;
            _runtimeLibNamespace = _globalNamespace.GetNamespaceMembers().FirstOrDefault(ns => ns.Name == Utility.RuntimeAssemblyName)!;
            _indentSize = indentSize;
        }

        public string GenerateLua()
        {
            WriteHeader();
            Visit(_tree.GetRoot());
            return _output.ToString().Trim();
        }

        private void WriteHeader()
        {
            if (Utility.IsDebug())
            {
                WriteLine($"package.path = \"{Utility.GetRbxcsDirectory()}/{Utility.RuntimeAssemblyName}/?.lua;\" .. package.path");
            }
            WriteLine($"local CS = require({GetLuaRuntimeLibPath()})");
            WriteLine();
        }

        private string GetLuaRuntimeLibPath()
        {
            if (Utility.IsDebug())
            {
                return "\"RuntimeLib\"";
            }
            else
            {
                // TODO: read rojo project file and locate rbxcs_include
                return "game:GetService(\"ReplicatedStorage\").rbxcs_include.RuntimeLib";
            }
        }

        public override void VisitForEachVariableStatement(ForEachVariableStatementSyntax node)
        {
            Visit(node.Variable);
        }

        public override void VisitForEachStatement(ForEachStatementSyntax node)
        {
            Write("for _, ");
            Write(GetName(node));
            Write(" in ");
            Visit(node.Expression);
            WriteLine(" do");
            _indent++;

            Visit(node.Statement);

            _indent--;
            WriteLine("end");
        }

        public override void VisitForStatement(ForStatementSyntax node)
        {
            WriteLine("do");
            _indent++;

            foreach (var initializer in node.Initializers)
            {
                Visit(initializer);
            }
            WriteLine("while true do");
            _indent++;

            Write("if not (");
            Visit(node.Condition);
            WriteLine(") then break end");
            Visit(node.Statement);
            foreach (var incrementor in node.Incrementors)
            {
                Visit(incrementor);
            }

            _indent--;
            WriteLine("end");

            _indent--;
            WriteLine("end");
        }

        public override void VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
        {
            Write("function");
            Visit(node.ParameterList);
            WriteLine();
            _indent++;

            Visit(node.Body);

            _indent--;
            Write("end");
        }

        public override void VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
        {
            Write("function(");
            Visit(node.Parameter);
            Write(')');
            WriteLine();
            _indent++;

            if (node.Block != null || node.ExpressionBody.IsKind(SyntaxKind.SimpleAssignmentExpression))
            {
                Visit(node.Body);
            }
            else
            {
                Write("return ");
                Visit(node.ExpressionBody);
            }
            
            _indent--;
            WriteLine();
            Write("end");
        }

        public override void VisitConditionalAccessExpression(ConditionalAccessExpressionSyntax node)
        {
            Write("if ");
            Visit(node.Expression);
            Write(" == nil then nil else ");
            Visit(node.WhenNotNull);
        }

        public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
        {
            Write("if ");
            Visit(node.Condition);
            Write(" then ");
            Visit(node.WhenTrue);
            Write(" else ");
            Visit(node.WhenFalse);
        }

        public override void VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
        {
            Write(Utility.GetMappedOperator(node.OperatorToken.Text));
            Visit(node.Operand);
        }

        public override void VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            var operatorText = Utility.GetMappedOperator(node.OperatorToken.Text);
            switch (operatorText)
            {
                case "??":
                    Write("if ");
                    Visit(node.Left);
                    Write(" == nil then ");
                    Visit(node.Right);
                    Write(" else ");
                    Visit(node.Left);
                    return;
            }

            Visit(node.Left);
            Write($" {operatorText} ");
            Visit(node.Right);
        }

        public override void VisitTupleExpression(TupleExpressionSyntax node)
        {
            WriteListTable(node.Arguments.Select(arg => arg.Expression).ToList());
        }

        public override void VisitCollectionExpression(CollectionExpressionSyntax node)
        {
            Write('{');
            foreach (var element in node.Elements)
            {
                var children = element.ChildNodes();
                foreach (var child in children)
                {
                    Visit(child);
                }
                if (element != node.Elements.Last())
                {
                    Write(", ");
                }
            }
            Write('}');
        }

        public override void VisitArrayCreationExpression(ArrayCreationExpressionSyntax node)
        {
            Visit(node.Initializer);
        }

        public override void VisitImplicitArrayCreationExpression(ImplicitArrayCreationExpressionSyntax node)
        {
            Visit(node.Initializer);
        }

        public override void VisitInitializerExpression(InitializerExpressionSyntax node)
        {
            WriteListTable(node.Expressions.ToList());
        }

        public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            Visit(node.Type);
            Write(".new");
            Visit(node.ArgumentList);
        }

        public override void VisitUsingDirective(UsingDirectiveSyntax node)
        {
            // do nothing (for now)
        }

        public override void VisitReturnStatement(ReturnStatementSyntax node)
        {
            Write("return ");
            Visit(node.Expression);
            WriteLine();
        }

        public override void VisitExpressionStatement(ExpressionStatementSyntax node)
        {
            base.VisitExpressionStatement(node);
            WriteLine();
        }

        public override void VisitArgumentList(ArgumentListSyntax node)
        {
            Write('(');
            foreach (var argument in node.Arguments)
            {
                Visit(argument);
                if (argument != node.Arguments.Last())
                {
                    Write(", ");
                }
            }
            Write(')');
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (node.Expression is MemberAccessExpressionSyntax)
            {
                var memberAccess = (MemberAccessExpressionSyntax)node.Expression;
                var objectName = GetName(memberAccess.Expression);
                var name = GetName(memberAccess.Name);

                switch (name)
                {
                    case "ToString":
                        Write("tostring(");
                        Visit(memberAccess.Expression);
                        Write(')');
                        return;
                    case "Write":
                    case "WriteLine":
                        {
                            var objectSymbolInfo = _semanticModel.GetSymbolInfo(memberAccess.Expression);
                            var objectDefinitionSymbol = (ITypeSymbol)objectSymbolInfo.Symbol!;
                            if (objectDefinitionSymbol.Name != "Console") return;

                            Write("print");
                            Visit(node.ArgumentList);
                            return;
                        }
                    case "Create":
                        {
                            if (objectName != "Instance") break;

                            var symbolInfo = _semanticModel.GetSymbolInfo(node);
                            var methodSymbol = (IMethodSymbol)symbolInfo.Symbol!;
                            if (!methodSymbol.IsGenericMethod)
                                throw new Exception("Attempt to macro Instance.Create<T>() but it is not generic?");

                            var arguments = node.ArgumentList.Arguments;
                            var instanceType = methodSymbol.TypeArguments.First();
                            Visit(memberAccess.Expression);
                            Write($".new(\"{instanceType.Name}\"");
                            if (arguments.Count > 0)
                            {
                                Write(", ");
                                foreach (var argument in arguments)
                                {
                                    Visit(argument.Expression);
                                    if (argument != arguments.Last())
                                    {
                                        Write(", ");
                                    }
                                }
                            }

                            Write(')');
                            return;
                        }
                    case "IsA":
                        {
                            var symbolInfo = _semanticModel.GetSymbolInfo(node);
                            var methodSymbol = (IMethodSymbol)symbolInfo.Symbol!;
                            if (!methodSymbol.IsGenericMethod)
                                throw new Exception("Attempt to macro Instance.IsA<T>() but it is not generic?");

                            var objectSymbolInfo = _semanticModel.GetSymbolInfo(memberAccess.Expression);
                            var objectDefinitionSymbol = (ILocalSymbol)objectSymbolInfo.Symbol?.OriginalDefinition!;
                            var superclasses = objectDefinitionSymbol.Type.AllInterfaces;
                            if (objectDefinitionSymbol.Name != "Instance" && !superclasses.Select(@interface => @interface.Name).Contains("Instance")) return;

                            var arguments = node.ArgumentList.Arguments;
                            var instanceType = methodSymbol.TypeArguments.First();
                            Visit(memberAccess.Expression);
                            Write($":IsA(\"{instanceType.Name}\"");
                            if (arguments.Count > 0)
                            {
                                Write(", ");
                                foreach (var argument in arguments)
                                {
                                    Visit(argument.Expression);
                                    if (argument != arguments.Last())
                                    {
                                        Write(", ");
                                    }
                                }
                            }
                            else
                            {
                                Write(')');
                            }
                            return;
                        }
                }

                var nameSymbolInfo = _semanticModel.GetSymbolInfo(node);
                {
                    if (nameSymbolInfo.Symbol is IMethodSymbol methodSymbol)
                    {
                        var operatorText = methodSymbol.IsStatic ? "." : ":";
                        Visit(memberAccess.Expression);
                        Write(operatorText);
                        Visit(memberAccess.Name);
                        Visit(node.ArgumentList);
                        return;
                    }
                }
            }

            Visit(node.Expression);
            Visit(node.ArgumentList);
        }

        public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            var objectSymbolInfo = _semanticModel.GetSymbolInfo(node.Expression);
            if (objectSymbolInfo.Symbol?.OriginalDefinition is INamedTypeSymbol objectDefinitionSymbol)
            {
                var superclasses = objectDefinitionSymbol.AllInterfaces;
                if (objectDefinitionSymbol.Name == "Services" || superclasses.Select(@interface => @interface.Name).Contains("Services"))
                {
                    Write("game:GetService(\"");
                    Visit(node.Name);
                    Write("\")");
                    return;
                }
            }

            if (objectSymbolInfo.Symbol != null && (objectSymbolInfo.Symbol.Kind == SymbolKind.Namespace || (objectSymbolInfo.Symbol.Kind == SymbolKind.NamedType && objectSymbolInfo.Symbol.IsStatic)))
            {
                var fullyQualifiedName = objectSymbolInfo.Symbol.ToDisplayString();
                var root = node.SyntaxTree.GetRoot();
                var compilationUnit = root as CompilationUnitSyntax;
                var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>().ToList();
                var filePathsContainingType = objectSymbolInfo.Symbol.Locations
                    .Where(location => location.SourceTree != null)
                    .Select(location => location.SourceTree!.FilePath)
                    .FirstOrDefault();

                if (compilationUnit != null)
                {
                    foreach (var usingDirective in compilationUnit.Usings)
                    {
                        usings.Add(usingDirective);
                    }
                }

                var noFullQualification = Constants.NO_FULL_QUALIFICATION_TYPES.Contains(fullyQualifiedName);
                var typeIsImported = usings.Any(usingDirective => usingDirective.Name != null && fullyQualifiedName.StartsWith(GetName(usingDirective)));
                var cannotAccess = filePathsContainingType != null && filePathsContainingType != node.SyntaxTree.FilePath && typeIsImported;
                if (cannotAccess)
                {
                    Logger.Debug("Attempt to fully qualify member of un-imported namespace.");
                    return;
                }
                else if (noFullQualification && fullyQualifiedName != "System")
                {
                    if (node.Expression is IdentifierNameSyntax identifier)
                    {
                        Visit(node.Name);
                    }
                    else if (node.Expression is MemberAccessExpressionSyntax memberAccess)
                    {
                        if (fullyQualifiedName == "RobloxRuntime" && GetName(memberAccess) != "Globals")
                        {
                            Visit(memberAccess.Name);
                            Write(".");
                        }
                        Visit(node.Name);
                    }
                    else
                    {
                        throw new NotSupportedException("Unsupported node.Expression type for a NO_FULL_QUALIFICATION_NAMESPACES member");
                    }
                    return;
                }
            }

            if (objectSymbolInfo.Symbol?.OriginalDefinition is ILocalSymbol typeSymbol)
            {
                switch (typeSymbol.Type.Name)
                {
                    case "ValueTuple":
                        var name = GetName(node.Name);
                        if (name.StartsWith("Item"))
                        {
                            var itemIndex = name.Split("Item").Last();
                            Visit(node.Expression);
                            Write($"[{itemIndex}]");
                            return;
                        }
                        break;
                }
            }

            Visit(node.Expression);
            Write(".");
            Visit(node.Name);
        }

        public override void VisitVariableDeclarator(VariableDeclaratorSyntax node)
        {
            Write($"local {node.Identifier.ValueText}");
            if (node.Initializer != null)
            {
                Write(" = ");
                Visit(node.Initializer);
            }
            WriteLine();
        }

        public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            Visit(node.Left);
            Write(" = ");
            Visit(node.Right);
        }

        public override void VisitIdentifierName(IdentifierNameSyntax node)
        {
            var identifierName = node.Identifier.ValueText;
            if (identifierName == "var") return;

            var isWithinClass = IsDescendantOf<ClassDeclarationSyntax>(node);
            var prefix = "";
            if (isWithinClass)
            {
                // Check the fields in classes that this node
                // is a descendant of for the identifier name
                var ancestorClasses = GetAncestors<ClassDeclarationSyntax>(node);
                for (int i = 0; i < ancestorClasses.Length; i++)
                {
                    var ancestorClass = ancestorClasses[i];
                    var fields = ancestorClass.Members.OfType<FieldDeclarationSyntax>();
                    foreach (var field in fields)
                    {
                        var isStatic = HasSyntax(field.Modifiers, SyntaxKind.StaticKeyword);
                        foreach (var declarator in field.Declaration.Variables)
                        {
                            var name = GetName(declarator);
                            if (name != identifierName) continue;
                            prefix = (isStatic ? GetName(ancestorClass) : "self") + ".";
                        }
                    }
                }
            }

            if (prefix == "")
            {
                var parentNamespace = FindFirstAncestor<NamespaceDeclarationSyntax>(node);
                var namespaceIncludesIdentifier = parentNamespace != null && parentNamespace.Members
                    .Where(member => GetNames(member).Contains(identifierName))
                    .Count() > 0;

                var symbol = _semanticModel.GetSymbolInfo(node).Symbol;
                var robloxClassesNamespace = _runtimeLibNamespace.GetNamespaceMembers().FirstOrDefault(ns => ns.Name == "Classes");
                var runtimeNamespaceIncludesIdentifier = symbol != null ? (
                    IsDescendantOfNamespaceSymbol(symbol, _runtimeLibNamespace)
                    || (robloxClassesNamespace != null && IsDescendantOfNamespaceSymbol(symbol, robloxClassesNamespace))
                ) : false;

                var parentAccessExpression = FindFirstAncestor<MemberAccessExpressionSyntax>(node);
                var isLeftSide = parentAccessExpression == null ? true : node == parentAccessExpression.Expression;
                var parentBlocks = GetAncestors<SyntaxNode>(node);
                var localScopeIncludesIdentifier = parentBlocks
                    .Any(block =>
                    {
                        var descendants = block.DescendantNodes();
                        var variableDeclarators = descendants.OfType<VariableDeclaratorSyntax>();
                        var forEachStatements = descendants.OfType<ForEachStatementSyntax>();
                        var forStatements = descendants.OfType<ForStatementSyntax>();
                        var parameters = descendants.OfType<ParameterSyntax>();
                        var checkNamePredicate = (SyntaxNode node) => GetName(node) == identifierName;
                        return variableDeclarators.Where(checkNamePredicate).Count() > 0
                            || parameters.Where(checkNamePredicate).Count() > 0
                            || forEachStatements.Where(checkNamePredicate).Count() > 0
                            || forStatements.Any(forStatement => forStatement.Initializers.Count() > 0);
                    });

                if (isLeftSide && !localScopeIncludesIdentifier && !runtimeNamespaceIncludesIdentifier)
                {
                    if (namespaceIncludesIdentifier)
                    {
                        Write($"namespace[\"$getMember\"](namespace, \"{identifierName}\")");
                    }
                    else
                    {
                        Write($"CS.getAssemblyType(\"{identifierName}\")");
                    }
                }
                else
                {
                    Write(identifierName);
                }
            }
            else
            {
                Write(prefix + identifierName);
            }
        }

        public override void VisitInterpolatedStringText(InterpolatedStringTextSyntax node)
        {
            Write(node.TextToken.Text);
        }

        public override void VisitInterpolation(InterpolationSyntax node)
        {
            Write('{');
            Visit(node.Expression);
            Write('}');
        }

        public override void VisitInterpolatedStringExpression(InterpolatedStringExpressionSyntax node)
        {
            Write('`');
            foreach (var content in node.Contents)
            {
                Visit(content);
            }
            Write('`');
        }

        public override void VisitLiteralExpression(LiteralExpressionSyntax node)
        {
            switch (node.Kind())
            {
                case SyntaxKind.StringLiteralExpression:
                case SyntaxKind.Utf8StringLiteralExpression:
                case SyntaxKind.CharacterLiteralExpression:
                    Write($"\"{node.Token.ValueText}\"");
                    break;

                case SyntaxKind.TrueLiteralExpression:
                case SyntaxKind.FalseLiteralExpression:
                case SyntaxKind.NumericLiteralExpression:
                    Write(node.Token.ValueText);
                    break;

                case SyntaxKind.NullLiteralExpression:
                    Write("nil");
                    break;

                case SyntaxKind.DefaultLiteralExpression:
                    Logger.CodegenError(node, "\"default\" keyword is not supported!");
                    break;
            }

            base.VisitLiteralExpression(node);
        }

        public override void VisitParameter(ParameterSyntax node)
        {
            Write(GetName(node));
        }

        public override void VisitParameterList(ParameterListSyntax node)
        {
            Write('(');
            foreach (var parameter in node.Parameters)
            {
                Visit(parameter);
                if (parameter != node.Parameters.Last())
                {
                    Write(", ");
                }
            }
            WriteLine(')');
            _indent++;

            foreach (var parameter in node.Parameters)
            {
                if (parameter.Default == null) continue;

                var name = GetName(parameter);
                Write(name);
                Write(" = ");
                Write($"if {name} == nil then ");
                Visit(parameter.Default);
                WriteLine($" else {name}");
            }

            _indent--;
        }

        public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
        {
            var isWithinNamespace = IsDescendantOf<NamespaceDeclarationSyntax>(node);
            var allNames = GetNames(node);
            var firstName = allNames.First();
            allNames.Remove(firstName);

            WriteLine($"{(isWithinNamespace ? "namespace:" : "CS.")}namespace(\"{firstName}\", function(namespace)");
            _indent++;

            if (allNames.Count > 0)
            {
                foreach (var name in allNames)
                {
                    WriteLine($"namespace:namespace(\"{name}\", function(namespace)");
                    _indent++;

                    foreach (var member in node.Members)
                    {
                        Visit(member);
                    }

                    _indent--;
                    WriteLine("end)");
                }
            }
            else
            {
                foreach (var member in node.Members)
                {
                    Visit(member);
                }
            }

            _indent--;
            WriteLine("end)");
            WriteLine();
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var parentNamespace = FindFirstAncestor<NamespaceDeclarationSyntax>(node);
            var className = GetName(node);
            WriteLine($"namespace:class(\"{className}\", function(namespace)");
            _indent++;

            WriteLine("local class = {}");
            WriteLine("class.__index = class");
            WriteLine();
            InitializeDefaultFields(
                node.Members
                    .OfType<FieldDeclarationSyntax>()
                    .Where(member => HasSyntax(member.Modifiers, SyntaxKind.StaticKeyword))
            );

            var isStatic = HasSyntax(node.Modifiers, SyntaxKind.StaticKeyword);
            var constructor = node.Members.OfType<ConstructorDeclarationSyntax>().FirstOrDefault();
            if (!isStatic)
            {
                if (constructor == null)
                {
                    CreateDefaultConstructor(node);
                }
                else
                {
                    VisitConstructorDeclaration(constructor);
                }
            }

            var methods = node.Members.OfType<MethodDeclarationSyntax>();
            var staticMethods = methods.Where(method => HasSyntax(method.Modifiers, SyntaxKind.StaticKeyword));
            foreach (var method in staticMethods)
            {
                Visit(method);
            }

            var isEntryPointClass = GetName(node) == _config.CSharpOptions.EntryPointName;
            if (isEntryPointClass)
            {
                var mainMethod = methods
                    .Where(method => GetName(method) == _config.CSharpOptions.MainMethodName)
                    .FirstOrDefault();

                if (mainMethod == null)
                {
                    Logger.CodegenError(node.Identifier, $"No main method \"{_config.CSharpOptions.MainMethodName}\" found in entry point class");
                    return;
                }
                if (!HasSyntax(mainMethod.Modifiers, SyntaxKind.StaticKeyword))
                {
                    Logger.CodegenError(node.Identifier, $"Main method must be static.");
                }

                WriteLine();
                WriteLine("if namespace == nil then");
                _indent++;
                WriteLine($"class.{_config.CSharpOptions.MainMethodName}()");
                _indent--;
                WriteLine("else");
                _indent++;
                WriteLine($"namespace[\"$onLoaded\"](namespace, class.{_config.CSharpOptions.MainMethodName})");
                _indent--;
                Write("end");
            }

            WriteLine();
            WriteLine($"return {(isStatic ? "class" : "setmetatable({}, class)")}");

            _indent--;
            WriteLine("end)");
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var isStatic = HasSyntax(node.Modifiers, SyntaxKind.StaticKeyword);
            var name = GetName(node);
            Write($"function {(isStatic ? "class" : "self")}.{name}");
            Visit(node.ParameterList);
            _indent++;

            Visit(node.Body);

            _indent--;
            WriteLine("end");
        }

        public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            // TODO: struct support?
            Write($"function class.new");
            Visit(node.ParameterList);
            _indent++;

            VisitConstructorBody(FindFirstAncestor<ClassDeclarationSyntax>(node)!, node.Body);

            _indent--;
            WriteLine("end");
        }

        private void VisitConstructorBody(ClassDeclarationSyntax parentClass, BlockSyntax? block)
        {
            WriteLine("local self = setmetatable({}, class)");
            WriteLine();
            var nonStaticFields = parentClass.Members
                .OfType<FieldDeclarationSyntax>()
                .Where(member => !HasSyntax(member.Modifiers, SyntaxKind.StaticKeyword));

            InitializeDefaultFields(nonStaticFields);
            var nonStaticMethods = parentClass.Members
                .OfType<MethodDeclarationSyntax>()
                .Where(member => !HasSyntax(member.Modifiers, SyntaxKind.StaticKeyword));

            if (block != null) Visit(block);
            WriteLine();

            foreach (var method in nonStaticMethods)
            {
                Visit(method);
            }
            WriteLine();
            WriteLine("return self");
        }

        private void CreateDefaultConstructor(ClassDeclarationSyntax node)
        {
            WriteLine($"function class.new()");
            _indent++;

            VisitConstructorBody(node, null);

            _indent--;
            WriteLine("end");
        }

        private void InitializeDefaultFields(IEnumerable<FieldDeclarationSyntax> fields)
        {
            foreach (var field in fields)
            {
                var isStatic = HasSyntax(field.Modifiers, SyntaxKind.StaticKeyword);
                foreach (var declarator in field.Declaration.Variables)
                {
                    if (declarator.Initializer == null) continue;
                    Write($"{(isStatic ? "class" : "self")}.{GetName(declarator)} = ");
                    Visit(declarator.Initializer);
                    WriteLine();
                }
            }
        }

        private void WriteListTable(List<ExpressionSyntax> expressions)
        {
            Write('{');
            foreach (var expression in expressions)
            {
                Visit(expression);
                if (expression != expressions.Last())
                {
                    Write(", ");
                }
            }
            Write('}');
        }

        private void WriteLine()
        {
            WriteLine("");
        }

        private void WriteLine(char text)
        {
            WriteLine(text.ToString());
        }

        private void WriteLine(string text)
        {
            if (text == null)
            {
                _output.AppendLine();
                return;
            }

            WriteTab();
            _output.AppendLine(text);
        }

        private void Write(char text)
        {
            Write(text.ToString());
        }

        private void Write(string text)
        {
            WriteTab();
            _output.Append(text);
        }

        private void WriteTab()
        {
            _output.Append(MatchLastCharacter('\n') ? GetTabString() : "");
        }

        private string GetTabString()
        {
            return string.Concat(Enumerable.Repeat(" ", _indentSize * _indent));
        }

        private bool HasSyntax(SyntaxTokenList tokens, SyntaxKind syntax)
        {
            return tokens.Any(token => token.IsKind(syntax));
        }

        private bool IsDescendantOfNamespaceSymbol(ISymbol symbol, INamespaceSymbol ancestor)
        {
            var namespaceSymbol = symbol.ContainingNamespace;
            while (namespaceSymbol != null)
            {
                if (SymbolEqualityComparer.Default.Equals(namespaceSymbol, ancestor))
                {
                    return true;
                }
                namespaceSymbol = namespaceSymbol.ContainingNamespace;
            }
            return false;
        }

        private bool IsDescendantOf<T>(SyntaxNode node) where T : SyntaxNode
        {
            return FindFirstAncestor<T>(node) != null;
        }

        private T? FindFirstAncestor<T>(SyntaxNode node) where T : SyntaxNode
        {
            return GetAncestors<T>(node).FirstOrDefault();
        }

        private T[] GetAncestors<T>(SyntaxNode node) where T : SyntaxNode
        {
            return node.Ancestors().OfType<T>().ToArray();
        }

        private string GetName(SyntaxNode node)
        {
            return GetNames(node).First();
        }

        private List<string> GetNames(SyntaxNode? node)
        {
            var names = new List<string>();
            if (node == null) return names;

            var identifierProperty = node.GetType().GetProperty("Identifier");
            var identifierValue = identifierProperty?.GetValue(node);
            if (identifierProperty != null && identifierValue != null && identifierValue is SyntaxToken)
            {
                names.Add(((SyntaxToken)identifierValue).Text.Trim());
                return names;
            }

            var childNodes = node.ChildNodes();
            var qualifiedNameNodes = node.IsKind(SyntaxKind.QualifiedName) ? [(QualifiedNameSyntax)node] : childNodes.OfType<QualifiedNameSyntax>();
            var identifierNameNodes = node.IsKind(SyntaxKind.IdentifierName) ? [(IdentifierNameSyntax)node] : childNodes.OfType<IdentifierNameSyntax>();
            foreach (var qualifiedNameNode in qualifiedNameNodes)
            {
                foreach (var name in GetNames(qualifiedNameNode.Left))
                {
                    names.Add(name.Trim());
                }
                foreach (var name in GetNames(qualifiedNameNode.Right))
                {
                    names.Add(name.Trim());
                }
            }

            foreach (var identifierNameNode in identifierNameNodes)
            {
                names.Add(identifierNameNode.Identifier.Text.Trim());
            }

            return names;
        }

        private bool MatchLastCharacter(char character)
        {
            if (_output.Length == 0) return false;
            return _output[_output.Length - 1] == character;
        }
    }
}