﻿using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RobloxCS
{
    public static class TranspilerUtility
    {
        public static RojoProject? GetRojoProject(string inputDirectory, string projectName)
        {
            if (projectName == "UNIT_TESTING") return null;

            var path = RojoReader.FindProjectPath(inputDirectory, projectName);
            if (path == null)
            {
                Logger.Error($"Failed to find Rojo project file \"{projectName}.project.json\"!");
                return null!;
            }

            return RojoReader.Read(path);
        }

        public static string CleanUpLuaForTests(string luaSource, int? extraLines)
        {
            var lines = luaSource.Split('\n').ToList();
            lines.RemoveRange(0, 2 + (extraLines ?? 0));

            return string.Join('\n', lines).Replace("\r", "").Trim();
        }

        public static string GenerateLua(
            SyntaxTree tree,
            CSharpCompilation compiler,
            MemberCollectionResult members,
            string inputDirectory = "",
            ConfigData? config = null
        )
        {
            config ??= ConfigReader.UnitTestingConfig;
            var rojoProject = GetRojoProject(inputDirectory, config.RojoProjectName);
            var codeGenerator = new CodeGenerator(tree, compiler, rojoProject, members, config, Utility.FixPathSep(inputDirectory));
            return codeGenerator.GenerateLua();
        }

        public static CSharpCompilation GetCompiler(List<SyntaxTree> trees, ConfigData? config = null)
        {
            config ??= ConfigReader.UnitTestingConfig;
            var compilationOptions = new CSharpCompilationOptions(OutputKind.ConsoleApplication);
            var compiler = CSharpCompilation.Create(
                assemblyName: config.CSharpOptions.AssemblyName,
                syntaxTrees: trees,
                references: GetCompilationReferences(),
                options: compilationOptions
            );

            return compiler;
        }

        public static SyntaxTree TransformTree(SyntaxTree cleanTree, HashSet<Func<SyntaxTree, ConfigData, SyntaxTree>> transformMethods, ConfigData? config = null)
        {
            config ??= ConfigReader.UnitTestingConfig;

            var tree = cleanTree;
            foreach (var transform in transformMethods)
            {
                tree = transform(tree, config);
            }
            return tree;
        }

        public static SyntaxTree ParseTree(string source, string sourceFile = "TestFile.client.cs")
        {
            var cleanTree = CSharpSyntaxTree.ParseText(source);
            var compilationUnit = (CompilationUnitSyntax)cleanTree.GetRoot();
            var usingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System"));
            var newRoot = compilationUnit.AddUsings(usingDirective);
            return cleanTree
                .WithRootAndOptions(newRoot, cleanTree.Options)
                .WithFilePath(sourceFile);
        }

        private static List<PortableExecutableReference> GetCompilationReferences()
        {
            var runtimeLibAssemblyPath = string.Join('/', Utility.GetAssemblyDirectory(), Utility.RuntimeAssemblyName + ".dll");
            if (!File.Exists(runtimeLibAssemblyPath))
            {
                var directoryName = Path.GetDirectoryName(runtimeLibAssemblyPath);
                Logger.Error($"Failed to find {Utility.RuntimeAssemblyName}.dll in {(directoryName == null ? "(could not find assembly directory)" : Utility.FixPathSep(directoryName))}");
            }

            var references = new List<PortableExecutableReference>()
            {
                MetadataReference.CreateFromFile(runtimeLibAssemblyPath)
            };

            foreach (var coreLibReference in GetCoreLibReferences())
            {
                references.Add(coreLibReference);
            }
            return references;
        }

        private static HashSet<PortableExecutableReference> GetCoreLibReferences()
        {
            var coreLib = typeof(object).GetTypeInfo().Assembly.Location;
            HashSet<string> coreDlls = ["System.Runtime.dll", "System.Core.dll", "System.Collections.dll"];
            HashSet<PortableExecutableReference> references = [MetadataReference.CreateFromFile(coreLib)];
            
            foreach (var coreDll in coreDlls)
            {
                var dllPath = Path.Combine(Path.GetDirectoryName(coreLib)!, coreDll);
                references.Add(MetadataReference.CreateFromFile(dllPath));
            }
            return references;
        }
    }
}
