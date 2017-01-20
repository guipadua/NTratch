using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace NTratch
{
	static class CodeAnalyzer
	{
		public static Dictionary<string, MyMethod> AllMyMethods = new Dictionary<string, MyMethod>();
		public static Dictionary<string, InvokedMethod> InvokedMethods = new Dictionary<string, InvokedMethod>();        

		#region Tree Analysis
		/// <summary>
		/// Analyze the code by all trees and semantic models
		/// </summary>
		/// <param name="treeList"></param>
		/// <param name="compilation"></param>
		public static void AnalyzeAllTrees(Dictionary<SyntaxTree, SemanticModel> treeAndModelDic,
			Compilation compilation)
		{
			// statistics
			int numFiles = treeAndModelDic.Count;
			var treeNode = treeAndModelDic.Keys
				.Select(tree => tree.GetRoot().DescendantNodes().Count());

			// analyze every tree simultaneously
			var allMethodDeclarations = treeAndModelDic.Keys.AsParallel()
				.Select(tree => GetAllMethodDeclarations(tree, treeAndModelDic, compilation));
			foreach (var methoddeclar in allMethodDeclarations)
			{
				MergeDic(ref AllMyMethods, methoddeclar);
			}
			Logger.Log("Cached all method declarations.");

			var codeStatsList = treeAndModelDic.Keys.AsParallel()
				.Select(tree => AnalyzeATree(tree, treeAndModelDic, compilation)).ToList();
			CodeStatistics allStats = new CodeStatistics(codeStatsList);
			// Log statistics
			Logger.Log("Num of syntax nodes: " + treeNode.Sum());
			Logger.Log("Num of source files: " + numFiles);

            allStats.CodeStats["NumFiles"] = numFiles;
            allStats.CodeStats["NumDeclaredMethods"] = AllMyMethods.Count;
            allStats.CodeStats["NumInvokedMethods"] = InvokedMethods.Count;
            allStats.CodeStats["NumInvokedMethodsBinded"] = (int)InvokedMethods.Values.Count(method => method.isBinded());
            allStats.CodeStats["NumInvokedMethodsDeclared"] = (int)InvokedMethods.Values.Count(method => method.isDeclared());
            allStats.CodeStats["NumInvokedMethodsExtDocPresent"] = (int)InvokedMethods.Values.Count(method => method.isExternalDocPresent());
            
            allStats.PrintSatistics();

			// Save all the source code into a txt file
			bool saveAllSource = false;
			if (saveAllSource == true)
			{
				var sb = new StringBuilder(treeAndModelDic.Keys.First().Length * numFiles); //initial length
				foreach (var stat in codeStatsList)
				{
					sb.Append(stat.Item1.GetText());
				}
				string txtFilePath = IOFile.CompleteFileNameOutput("AllSource.txt");
				using (StreamWriter sw = new StreamWriter(txtFilePath))
				{
					sw.Write(sb.ToString());
				}
			}
		}

		/// <summary>
		/// Analyze the code statistics of a single AST
		/// </summary>
		/// <param name="tree"></param>
		/// <param name="compilation"></param>
		/// <returns></returns>
		public static Tuple<SyntaxTree, TreeStatistics> AnalyzeATree(SyntaxTree tree,
			Dictionary<SyntaxTree, SemanticModel> treeAndModelDic, Compilation compilation)
		{
			TreeStatistics stats = new TreeStatistics();
			var root = tree.GetRoot();

			// Num of LOC
			stats.CodeStats["NumLOC"] = tree.GetText().Lines.Count;

			//// Num of call sites
			//var callList = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
			//stats.CodeStats["NumCall"] = callList.Count();

			//// Num of logging
			//var loggingList = callList.Where(call => IsLoggingStatement(call));
			//int numLogging = loggingList.Count();
			//stats.CodeStats["NumLogging"] = numLogging;

			//if (numLogging > 0)
			//{
			//    // Num of logged file
			//    stats.CodeStats["NumLoggedFile"] = 1;

			//    // Num of logged LOC
			//    var loggedLines = loggingList.Select(logging => logging.GetText().LineCount);
			//    stats.CodeStats["NumLoggedLOC"] = loggedLines.Sum();
			//}

			//// Num of classes
			//var classList = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
			//int numClass =  classList.Count();
			//stats.CodeStats["NumClass"] = numClass;

			//// Num of methods
			//var methodList = root.DescendantNodes().OfType<BaseMethodDeclarationSyntax>();
			//int numMethod = methodList.Count();
			//stats.CodeStats["NumMethod"] = numMethod;

			// Num of catch blocks
			var catchList = root.DescendantNodes().OfType<CatchClauseSyntax>();
			int numCatchBlock = catchList.Count();
			stats.CodeStats["NumCatchBlock"] = numCatchBlock;

            // Statistics and features of catch blocks
            
            CatchAnalyzer catchAnalyzer = new CatchAnalyzer(treeAndModelDic, compilation);

            foreach (CatchClauseSyntax catchBlock in catchList)
            {
                catchAnalyzer.AnalyzeACatchBlock(catchBlock);                
            }

            stats.CatchBlockList = catchAnalyzer.Catches;
            stats.PossibleExceptionsBlockList = catchAnalyzer.PossibleExceptionsList;

            stats.CodeStats["NumPossibleExceptionBlock"] = catchAnalyzer.PossibleExceptionsList.Count;

            //// Statistics and features of (checked) API calls
            //if (callList.Count() > 0)
            //{
            //    stats.APICallList = callList
            //        .Select(apicall => AnalyzeAnAPICall(apicall, treeAndModelDic,
            //        compilation)).ToList();
            //}

            return new Tuple<SyntaxTree, TreeStatistics>(tree, stats);
		}
        
		public static Dictionary<string, MyMethod> GetAllMethodDeclarations(SyntaxTree tree,
			Dictionary<SyntaxTree, SemanticModel> treeAndModelDic, Compilation compilation)
		{
			var allMethodDeclarations = new Dictionary<string, MyMethod>();

			var root = tree.GetRoot();
			var methodDeclarList = root.DescendantNodes().OfType<BaseMethodDeclarationSyntax>();
			foreach (var method in methodDeclarList)
			{
				var methodName = ASTUtilities.GetNodeDeclaredSymbol(method, tree, treeAndModelDic, compilation).ToString();
				
				if (methodName != null)
				{
					if (!allMethodDeclarations.ContainsKey(methodName))
					{
						allMethodDeclarations.Add(methodName, new MyMethod(methodName, method));
					}
				}
			}
			return allMethodDeclarations;
		}
		#endregion Tree Analysis

		
	   

		#region ETC
		//public static Dictionary<string, int> GetVariablesAndComments(SyntaxNode codeSnippet)
		//{
		//	Dictionary<string, int> variableAndComments = new Dictionary<string, int>();

		//	bool hasTryStatement = codeSnippet.DescendantNodes()
		//		.OfType<TryStatementSyntax>().Any();
		//	if (hasTryStatement == true)
		//	{
		//		codeSnippet = tryblockremover.Visit(codeSnippet);
		//	}

		//	var variableList = codeSnippet.DescendantNodes().OfType<IdentifierNameSyntax>()
		//		.Where(variable => !SyntaxFacts.IsInTypeOnlyContext(variable));
		//	foreach (var variable in variableList)
		//	{
		//		var variableName = IOFile.MethodNameExtraction(variable.ToString());
		//		MergeDic<String>(ref variableAndComments,
		//			new Dictionary<string, int>() { { variableName, 1 } });
		//	}

		//	var commentList = codeSnippet.DescendantTrivia()
		//		.Where(childNode => childNode.IsKind(SyntaxKind.SingleLineCommentTrivia)
		//			|| childNode.IsKind(SyntaxKind.MultiLineCommentTrivia));
		//	foreach (var comment in commentList)
		//	{
		//		string updatedComment = IOFile.DeleteSpace(comment.ToString());
		//		updatedComment = Regex.Replace(updatedComment, "<.*>", "");
		//		updatedComment = Regex.Replace(updatedComment, "{.*}", "");
		//		updatedComment = Regex.Replace(updatedComment, "\\(.*\\)", "");
		//		MergeDic<String>(ref variableAndComments,
		//			new Dictionary<string, int>() { { updatedComment, 1 } });
		//	}
		//	return variableAndComments;
		//}

		//public static string GetContainingMethodName(SyntaxNode codeSnippet, SemanticModel model)
		//{
		//	// Method name
		//	SyntaxNode method = null;
		//	string methodName = null;
		//	try
		//	{
		//		method = codeSnippet.Ancestors().OfType<BaseMethodDeclarationSyntax>().First();
		//	}
		//	catch
		//	{
		//		// Skip method type: e.g., operator method
		//	}

		//	ISymbol methodSymbol;
		//	if (method != null)
		//	{
		//		if (method is MethodDeclarationSyntax)
		//		{
		//			var methodDeclaration = method as MethodDeclarationSyntax;
		//			try
		//			{
		//				methodSymbol = model.GetDeclaredSymbol(methodDeclaration);
		//				methodName = methodSymbol.ToString();
		//			}
		//			catch
		//			{
		//				methodName = methodDeclaration.Identifier.ValueText;
		//			}
		//		}
		//		else if (method is ConstructorDeclarationSyntax)
		//		{
		//			var methodDeclaration = method as ConstructorDeclarationSyntax;
		//			try
		//			{
		//				methodSymbol = model.GetDeclaredSymbol(methodDeclaration);
		//				methodName = methodSymbol.ToString();
		//			}
		//			catch
		//			{
		//				methodName = methodDeclaration.Identifier.ValueText;
		//			}
		//		}
		//	}
		//	return IOFile.MethodNameExtraction(methodName);
		//}

		public static void MergeDic<T>(ref Dictionary<T, int> dic1, Dictionary<T, int> dic2)
		{
			foreach (var key in dic2.Keys)
			{
				if (key == null) continue;
				if (dic1.ContainsKey(key))
				{
					dic1[key] += dic2[key];
				}
				else
				{
					dic1.Add(key, dic2[key]);
				}
			}
		}

		public static void MergeDic<T1, T2>(ref Dictionary<T1, T2> dic1, Dictionary<T1, T2> dic2)
		{
			foreach (var key in dic2.Keys)
			{
				if (key == null) continue;
				if (!dic1.ContainsKey(key))
				{
					dic1.Add(key, dic2[key]);
				}
			}
		}

		public static void MergeDic<T1, T2>(Dictionary<T1, T2> dic1, Dictionary<T1, T2> dic2)
		{
			foreach (var key in dic2.Keys)
			{
				if (key == null) continue;
				if (!dic1.ContainsKey(key))
				{
					dic1.Add(key, dic2[key]);
				}
			}
		}
		#endregion ETC

		
	}
}
