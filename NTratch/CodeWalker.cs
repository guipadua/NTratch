using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;
using System.Globalization;
using System.Threading;

namespace NTratch
{

    class CodeWalker
    {
        public static List<MetadataReference> appReflist = new List<MetadataReference>();

        public CodeWalker()
        {
            var mscorlib = FromType(typeof(object));            
            appReflist.Add(mscorlib);

            var coreLib = FromGAC("System.Core");
            appReflist.Add(coreLib);

            //mscorlib.
            // Find all the application API dll references files
            IEnumerable<string> appLibFiles = Directory.EnumerateFiles(IOFile.InputFolderPath,
                "*.dll", SearchOption.AllDirectories);
            foreach (var libFile in appLibFiles)
            {   
                // Add application API libs by new MetadataFileReference(libFile) 
                var reference = FromFile(libFile);
                appReflist.Add(reference);
                Logger.Log("Adding DLL reference: " + libFile);
            }
        }

        private static MetadataReference FromType(Type type)
        {
            var path = type.Assembly.Location;
            return MetadataReference.CreateFromFile(path, documentation: GetDocumentationProvider(path));
        }

        private static MetadataReference FromGAC(string libName)
        {
            var assemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location);
            var path = Path.Combine(assemblyPath, libName + ".dll");
            // Add system API libs by MetadataReference.CreateAssemblyReference
            return MetadataReference.CreateFromFile(path, documentation: GetDocumentationProvider(path));            
        }

        private static MetadataReference FromFile(string path)
        {
            return MetadataReference.CreateFromFile(path, documentation: GetDocumentationProvider(path));
        }

        private static string GetReferenceAssembliesPath()
        {
            var programFiles =
                Environment.GetFolderPath(Environment.Is64BitOperatingSystem
                    ? Environment.SpecialFolder.ProgramFilesX86
                    : Environment.SpecialFolder.ProgramFiles);
            var path = Path.Combine(programFiles, @"Reference Assemblies\Microsoft\Framework\.NETFramework");
            if (Directory.Exists(path))
            {
                var directories = Directory.EnumerateDirectories(path).OrderByDescending(Path.GetFileName);
                return directories.FirstOrDefault();
            }
            return null;
        }

        private static DocumentationProvider GetDocumentationProvider(string location)
        {
            var referenceLocation = Path.ChangeExtension(location, "xml");
            if (File.Exists(referenceLocation))
            {
                return GetXmlDocumentationProvider(referenceLocation);
            }
            var referenceAssembliesPath = GetReferenceAssembliesPath();
            if (referenceAssembliesPath != null)
            {
                var fileName = Path.GetFileName(location);
                referenceLocation = Path.ChangeExtension(Path.Combine(referenceAssembliesPath, fileName), "xml");
                if (File.Exists(referenceLocation))
                {
                    return GetXmlDocumentationProvider(referenceLocation);
                }
            }
            return null;
        }

        private static DocumentationProvider GetXmlDocumentationProvider(string location)
        {
            Logger.Log("Adding XML reference: " + location);
            return (DocumentationProvider)Activator.CreateInstance(Type.GetType(
                "Microsoft.CodeAnalysis.FileBasedXmlDocumentationProvider, Microsoft.CodeAnalysis.Workspaces.Desktop"),
                location);
        }

        public void LoadByInputMode(string inputMode, string filePath)
        {
            Logger.Log("Input mode: " + inputMode);
            switch (inputMode)
            {
                case "ByFolder":
                    LoadByFolder(filePath);
                    break;
                case "ByTxtFile":
                    LoadByTxtFile(filePath);
                    break;
                default:
                    Logger.Log("Invalid input mode. (Select ByFolder/ByTxtFile)");
                    Console.ReadKey();
                    return;
            }
        }

        public static void LoadByFolder(string folderPath)
        {
            Logger.Log("Loading from folder: " + folderPath);
            IEnumerable<string> FileNames = Directory.EnumerateFiles(folderPath, "*.cs",
                SearchOption.AllDirectories).Where(file => !file.Contains("Test"));
            
            int numFiles = FileNames.Count();
            Logger.Log("Loading " + numFiles + " *.cs files.");
            // parallelization
            var treeAndModelList = FileNames.AsParallel()
                .Select(fileName => LoadSourceFile(fileName))
                .ToList();

            var treeAndModelDic = new Dictionary<SyntaxTree, SemanticModel>();
            foreach (var treeAndModel in treeAndModelList)
            {
                treeAndModelDic.Add(treeAndModel.Item1, treeAndModel.Item2);
            }
            var compilation = BuildCompilation(treeAndModelDic.Keys.ToList());

            CodeAnalyzer.AnalyzeAllTrees(treeAndModelDic, compilation);
        }

        public static void LoadByTxtFile(string folderPath)
        {
            string txtFilePath = IOFile.CompleteFileNameInput("AllSource.txt");
            Logger.Log("Load from txt file: " + txtFilePath);

            string content = "";
            try
            {
                using (StreamReader sr = new StreamReader(txtFilePath))
                {
                    content = sr.ReadToEnd();
                }
            }
            catch (Exception e)
            {
                Logger.Log("Txt file may not exist.");
                Logger.Log(e);
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }

            var tree = CSharpSyntaxTree.ParseText(content);
            var model = GetSemanticInfo(tree);
            var treeAndModelDic = new Dictionary<SyntaxTree, SemanticModel>();
            treeAndModelDic.Add(tree, model);
            var compilation = BuildCompilation(new List<SyntaxTree> { tree });
            CodeAnalyzer.AnalyzeAllTrees(treeAndModelDic, compilation);
        }

        public static Tuple<SyntaxTree, SemanticModel> LoadSourceFile(string sourceFile)
        {
            Logger.Log("Loading source file: " + sourceFile);
            //            if (InputFileName.Split('\\').Last().Contains("Log"))
            //            {
            //                fileContent = "";
            //            }

            var stream = File.OpenRead(sourceFile);
            var tree = CSharpSyntaxTree.ParseText(SourceText.From(stream), path: sourceFile);
           
            var model = GetSemanticInfo(tree);

            return new Tuple<SyntaxTree, SemanticModel>(tree, model);
        }

        public static SemanticModel GetSemanticInfo(SyntaxTree tree)
        {
            // Collect the system API references from using directives
            List<MetadataReference> reflist = new List<MetadataReference>();
            var root = tree.GetRoot();
            var usingList = root.DescendantNodes().OfType<UsingDirectiveSyntax>();
            
            List<string> allLibNames = new List<string>();
            foreach (var usingLib in usingList)
            {
                string libName = usingLib.Name.ToString();
                MetadataReference reference = null;
                while (libName != "" && reference == null)
                {
                    if (allLibNames.Contains(libName)) break;

                    allLibNames.Add(libName);

                    try
                    {
                        // Add system API libs by MetadataReference.CreateAssemblyReference
                        reference = FromGAC(libName);
                    }
                    catch (Exception)
                    {
                        // handle cases that "libName.dll" does not exist
                        int idx = libName.LastIndexOf('.');
                        if (idx == -1)
                        {
                            libName = "";
                            break;
                        }
                        libName = libName.Substring(0, idx);                     
                    }
                }

                if (reference != null)
                {
                    Logger.Log("Adding reference: " + libName + ".dll");
                    reflist.Add(reference);
                }
            }

            reflist.AddRange(appReflist);
            var compilationOptions = new CSharpCompilationOptions(outputKind: OutputKind.WindowsApplication);
            var compilation = CSharpCompilation.Create(
                assemblyName: "ACompilation",
                options: compilationOptions,
                syntaxTrees: new[] { tree },
                references: reflist);

            var model = compilation.GetSemanticModel(tree);

            return model;
        }

        public static Compilation BuildCompilation(List<SyntaxTree> treelist)
        {
            List<MetadataReference> reflist = new List<MetadataReference>();

            // Collect the system API references from using directives
            var totalUsings = treelist.AsParallel().Select(
                    tree => tree.GetRoot().DescendantNodes().OfType<UsingDirectiveSyntax>());
            // transfer to a list
            var totalUsingList = totalUsings.SelectMany(x => x).ToList();
            // create metareference  
            List<string> allLibNames = new List<string>();
            foreach (var usingLib in totalUsingList)
            {
                string libName = usingLib.Name.ToString();
                MetadataReference reference = null;
                while (libName != "" && reference == null)
                {
                    if (allLibNames.Contains(libName)) break;

                    allLibNames.Add(libName);  

                    try
                    {
                        // Add system API libs by MetadataReference.CreateAssemblyReference
                        reference = MetadataReference.CreateFromFile(libName);
                    }
                    catch (Exception)
                    {
                        // handle cases that "libName.dll" does not exist
                        int idx = libName.LastIndexOf('.');
                        if (idx == -1)
                        {
                            libName = "";
                            break;
                        }
                        libName = libName.Substring(0, idx);
                    }
                }

                if (reference != null)
                {
                    Logger.Log("Adding reference: " + libName + ".dll");
                    reflist.Add(reference);
                }
            }

            reflist.AddRange(appReflist);
            var compilationOptions = new CSharpCompilationOptions(outputKind: OutputKind.WindowsApplication);
            var compilation = CSharpCompilation.Create(
                assemblyName: "AllCompilation",
                options: compilationOptions,
                syntaxTrees: treelist,
                references: reflist);

            return compilation;
        }

    }

    /// <summary>
    /// Remove the trivia (comments, white spaces, etc) of a code snippet
    /// </summary>
    public class CommentTriviaRemover : CSharpSyntaxRewriter
    {
        public override SyntaxTrivia VisitTrivia(SyntaxTrivia trivia)
        {
            var newTrivia = trivia;
            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
            {
                newTrivia = default(SyntaxTrivia);
            }
            return newTrivia;
        }
    }

    public class CommentAndRedudantTrailingWhitespaceRemover : CSharpSyntaxRewriter
    {
        public override SyntaxToken VisitToken(SyntaxToken token)
        {
            return token
                .WithLeadingTrivia(RemoveRedundantTrailingWhitespace(token.LeadingTrivia))
                .WithTrailingTrivia(RemoveRedundantTrailingWhitespace(token.TrailingTrivia));
        }

        private SyntaxTriviaList RemoveRedundantTrailingWhitespace(SyntaxTriviaList oldTriviaList)
        {
            var newTriviaList = new List<SyntaxTrivia>();
            var outSyntaxTriviaList = new SyntaxTriviaList();

            for (var i = 0; i < oldTriviaList.Count; ++i)
            {
                var isRedundantWhitespace =
                    i + 1 < oldTriviaList.Count &&
                    oldTriviaList[i].IsKind(SyntaxKind.WhitespaceTrivia) &&
                    oldTriviaList[i + 1].IsKind(SyntaxKind.EndOfLineTrivia);

                if (!isRedundantWhitespace)
                {
                    newTriviaList.Add(oldTriviaList[i]);                    
                }
            }
            outSyntaxTriviaList = outSyntaxTriviaList.AddRange(newTriviaList);
            return outSyntaxTriviaList;
        }
    }

    /// <summary>
    /// Remove the try-catch block of a code snippet
    /// </summary>
    public class TryStatementRemover : CSharpSyntaxRewriter
    {
        public override SyntaxNode VisitTryStatement(TryStatementSyntax node)
        {
            // rewrite to null
            return null;
        }
    }

    class TryStatementSkipper : CSharpSyntaxWalker
    {
        public readonly List<InvocationExpressionSyntax> invokedMethods = new List<InvocationExpressionSyntax>();
        public readonly List<ThrowStatementSyntax> invokedThrows = new List<ThrowStatementSyntax>();
        public readonly List<ObjectCreationExpressionSyntax> objectCreationExpressions = new List<ObjectCreationExpressionSyntax>();

        public override void VisitTryStatement(TryStatementSyntax node)
        {
            // skip over
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            this.invokedMethods.Add(node);
        }

        public override void VisitThrowStatement(ThrowStatementSyntax node)
        {
            this.invokedThrows.Add(node);
        }

        public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            this.objectCreationExpressions.Add(node);
        }
    }

}


