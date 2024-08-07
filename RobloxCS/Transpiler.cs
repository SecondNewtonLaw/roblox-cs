﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RobloxCS
{
    public sealed class Transpiler
    {
        public readonly ConfigData Config;

        private const string _includeFolderName = "Include";
        private List<SyntaxTree> _fileTrees = new List<SyntaxTree>();
        private readonly string _inputDirectory;
        private readonly string _sourceDirectory;
        private readonly string _outDirectory;

        public Transpiler(string inputDirectory)
        {
            Config = ConfigReader.Read(inputDirectory);
            _inputDirectory = inputDirectory;
            _sourceDirectory = inputDirectory + "/" + Config.SourceFolder;
            _outDirectory = inputDirectory + "/" + Config.OutputFolder;
        }

        public void Transpile()
        {
            ParseSource();
            var compiler = CompileASTs();
            CopyIncludedLua();
            WriteLuaOutput(compiler);
        }

        private void ParseSource()
        {
            if (!Directory.Exists(_sourceDirectory))
            {
                Logger.Error($"Source folder \"{Config.SourceFolder}\" does not exist!");
            }

            var sourceFiles = FileManager.GetSourceFiles(_sourceDirectory);
            foreach (var sourceFile in sourceFiles)
            {
                var fileContents = File.ReadAllText(sourceFile);
                var tree = TranspilerUtility.ParseTree(fileContents, sourceFile);
                HashSet<Func<SyntaxTree, ConfigData, SyntaxTree>> transformers = [BuiltInTransformers.Main()];

                foreach (var transformerName in Config.EnabledBuiltInTransformers)
                {
                    transformers.Add(BuiltInTransformers.Get(transformerName));
                }

                var transformedTree = TranspilerUtility.TransformTree(tree, transformers, Config);
                foreach (var diagnostic in transformedTree.GetDiagnostics())
                {
                    Logger.HandleDiagnostic(diagnostic);
                }

                _fileTrees.Add(transformedTree);
            }
        }
        
        private CSharpCompilation CompileASTs()
        {
            var compiler = TranspilerUtility.GetCompiler(_fileTrees, Config);
            foreach (var diagnostic in compiler.GetDiagnostics())
            {
                Logger.HandleDiagnostic(diagnostic);
            }

            return compiler;
        }

        private void CopyIncludedLua()
        {
            var rbxcsDirectory = Utility.GetRbxcsDirectory();
            if (rbxcsDirectory == null)
            {
                Logger.CompilerError("Failed to find RobloxCS directory");
                return;
            }

            var compilerDirectory = Utility.FixPathSep(Path.Combine(rbxcsDirectory, "RobloxCS"));
            var includeDirectory = Utility.FixPathSep(Path.Combine(compilerDirectory, _includeFolderName));
            var destinationIncludeDirectory = includeDirectory
                .Replace(compilerDirectory, _inputDirectory)
                .Replace(_includeFolderName, _includeFolderName.ToLower());

            try
            {
                FileManager.CopyDirectory(includeDirectory, destinationIncludeDirectory);
            }
            catch (Exception e)
            {
                Logger.Error($"Failed to copy included Lua files: {e.Message}");
            }
        }

        private void WriteLuaOutput(CSharpCompilation compiler)
        {
            var compiledFiles = new List<CompiledFile>();
            var memberCollector = new MemberCollector(_fileTrees);
            var members = memberCollector.Collect();
            if (Config.CSharpOptions.EntryPointRequired && _fileTrees.All(tree => !tree.GetRoot().DescendantNodes().Any(node => node is ClassDeclarationSyntax classDeclaration && Utility.GetNamesFromNode(classDeclaration).FirstOrDefault() == Config.CSharpOptions.EntryPointName)))
            {
                Logger.Error($"No entry point class \"{Config.CSharpOptions.EntryPointName}\" found!");
            }

            foreach (var tree in _fileTrees)
            {
                var generatedLua = TranspilerUtility.GenerateLua(tree, compiler, members, _inputDirectory, Config);
                var targetPath = tree.FilePath.Replace(Config.SourceFolder, Config.OutputFolder).Replace(".cs", ".lua");
                compiledFiles.Add(new CompiledFile(targetPath, generatedLua));
            }

            EnsureDirectoriesExist();
            FileManager.WriteCompiledFiles(_outDirectory, compiledFiles);
        }

        private void EnsureDirectoriesExist()
        {
            var subDirectories = Directory.GetDirectories(_sourceDirectory, "*", SearchOption.AllDirectories);
            foreach (string subDirectory in subDirectories)
            {
                Directory.CreateDirectory(subDirectory.Replace(Config.SourceFolder, Config.OutputFolder));
            }
        }
    }
}