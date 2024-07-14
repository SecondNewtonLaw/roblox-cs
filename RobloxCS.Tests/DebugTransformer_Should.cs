namespace RobloxCS.Tests
{
    public class DebugTransformer_Should
    {
        [Theory]
        [InlineData("Console.WriteLine(\"hello baby\")")]
        [InlineData("Console.Write(\"hello baby\")")]
        [InlineData("warn(\"hello baby\")")]
        public void FileInfo_PrependsArgument(string input)
        {
            var cleanTree = TranspilerUtility.ParseTree(input);
            var transformedTree = TranspilerUtility.TransformTree(cleanTree, [TransformFactory.Main(), TransformFactory.Debug()]);
            var cleanRoot = cleanTree.GetRoot();
            var cleanInvocation = cleanRoot.DescendantNodes().OfType<InvocationExpressionSyntax>().First();
            var transformedRoot = transformedTree.GetRoot();
            var transformedInvocation = transformedRoot.DescendantNodes().OfType<InvocationExpressionSyntax>().First();
            var cleanArgs = cleanInvocation.ArgumentList.Arguments;
            var transformedArgs = transformedInvocation.ArgumentList.Arguments;
            var fileInfoArg = transformedArgs.FirstOrDefault();
            Assert.True(cleanArgs.Count == 1);
            Assert.True(transformedArgs.Count == 2);
            Assert.NotNull(fileInfoArg);

            var fileInfoLiteral = (LiteralExpressionSyntax)fileInfoArg.Expression;
            Assert.Equal($"[TestFile.cs:1:13]:", fileInfoLiteral.Token.ValueText);
        }

        [Theory]
        [InlineData("error(\"hello baby\")")]
        public void FileInfo_ConcatenatesLiteral(string input)
        {
            var cleanTree = TranspilerUtility.ParseTree(input);
            var transformedTree = TranspilerUtility.TransformTree(cleanTree, [TransformFactory.Main(), TransformFactory.Debug()]);
            var cleanRoot = cleanTree.GetRoot();
            var cleanInvocation = cleanRoot.DescendantNodes().OfType<InvocationExpressionSyntax>().First();
            var transformedRoot = transformedTree.GetRoot();
            var transformedInvocation = transformedRoot.DescendantNodes().OfType<InvocationExpressionSyntax>().First();
            var cleanArgs = cleanInvocation.ArgumentList.Arguments;
            var transformedArgs = transformedInvocation.ArgumentList.Arguments;
            var fullMessageArg = transformedArgs.FirstOrDefault();
            Assert.True(cleanArgs.Count == 1);
            Assert.True(transformedArgs.Count == 1);
            Assert.NotNull(fullMessageArg);

            var fullMessageBinary= (BinaryExpressionSyntax)fullMessageArg.Expression;
            var fileInfoLiteral = (LiteralExpressionSyntax)fullMessageBinary.Left;
            Assert.Equal("[TestFile.cs:1:13]: ", fileInfoLiteral.Token.ValueText);
        }
    }
}