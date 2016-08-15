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
        public static TryStatementRemover tryblockremover = new TryStatementRemover();
        public static Dictionary<string, MyMethod> AllMyMethods =
            new Dictionary<string, MyMethod>();
        

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
                string txtFilePath = IOFile.CompleteFileName("AllSource.txt");
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

            //// Logging statistics
            //if (numLogging > 0)
            //{
            //    //var loggedClasses = new Dictionary<ClassDeclarationSyntax, int>();
            //    //var loggedMethods = new Dictionary<BaseMethodDeclarationSyntax, int>();
            //    var loggedCatchBlocks = new Dictionary<CatchClauseSyntax, int>();
            //    foreach (var logging in loggingList)    
            //    {
            //        //// Num of logged classes
            //        //if (numClass > 0)
            //        //{
            //        //    try
            //        //    {
            //        //        var classNode = logging.Ancestors().OfType<ClassDeclarationSyntax>().First();
            //        //        MergeDic<ClassDeclarationSyntax>(ref loggedClasses, 
            //        //            new Dictionary<ClassDeclarationSyntax, int>(){{classNode, 1}});
            //        //    }
            //        //    catch (Exception)
            //        //    { 
            //        //        // ignore.
            //        //    }
            //        //}

            //        // Num of logged methods
            //        if (numMethod > 0)
            //        {
            //            try
            //            {
            //                var method = logging.Ancestors().OfType<BaseMethodDeclarationSyntax>().First();
            //                MergeDic<BaseMethodDeclarationSyntax>(ref loggedMethods,
            //                    new Dictionary<BaseMethodDeclarationSyntax, int>() { { method, 1 } });
            //            }
            //            catch (Exception)
            //            {
            //                // ignore.
            //            }
            //        }

            //    }

            //    stats.CodeStats["NumLoggedClass"] = loggedClasses.Count;
            //    stats.CodeStats["NumLoggedMethod"] = loggedMethods.Count;
            //}

            // Statistics and features of catch blocks
            stats.CatchBlockList = catchList
                .Select(catchblock => AnalyzeACatchBlock(catchblock, treeAndModelDic,
                compilation)).ToList();

            //// Statistics and features of (checked) API calls
            //if (callList.Count() > 0)
            //{
            //    stats.APICallList = callList
            //        .Select(apicall => AnalyzeAnAPICall(apicall, treeAndModelDic,
            //        compilation)).ToList();
            //}

            return new Tuple<SyntaxTree, TreeStatistics>(tree, stats);
        }

        public static CatchBlock AnalyzeACatchBlock(CatchClauseSyntax catchblock,
                Dictionary<SyntaxTree, SemanticModel> treeAndModelDic, Compilation compilation)
        {
            CatchBlock catchBlockInfo = new CatchBlock();
            var tree = catchblock.SyntaxTree;
            var model = treeAndModelDic[tree];

            TypeSyntax exceptionTypeSyntax = null;
            INamedTypeSymbol exceptionNamedTypeSymbol = null;

            if (catchblock.Declaration != null)
            {
                exceptionTypeSyntax = catchblock.Declaration.Type;
                exceptionNamedTypeSymbol = model.GetTypeInfo(exceptionTypeSyntax).ConvertedType as INamedTypeSymbol;

                if (exceptionNamedTypeSymbol != null)
                {
                    catchBlockInfo.ExceptionType = exceptionNamedTypeSymbol.ToString();
                        
                    //Binding info:
                    if (exceptionNamedTypeSymbol.BaseType != null)
                    {
                        catchBlockInfo.OperationFeatures["Binded"] = 1;
                        catchBlockInfo.OperationFeatures["RecoveredBinding"] = model.IsSpeculativeSemanticModel ? 1 : 0;
                        catchBlockInfo.OperationFeatures["Kind"] = FindKind(exceptionNamedTypeSymbol, compilation);
                    }
                    else
                        catchBlockInfo.OperationFeatures["Binded"] = 0;
                }
                else
                    catchBlockInfo.ExceptionType = "!NO_NAMED_TYPE!";
            } else
                catchBlockInfo.ExceptionType = "!NO_EXCEPTION_DECLARED!";
            
            //Basic info:
            catchBlockInfo.MetaInfo["ExceptionType"] = catchBlockInfo.ExceptionType;
            
            //Try info:
            var tryBlock = catchblock.Parent as TryStatementSyntax;
            catchBlockInfo.MetaInfo["TryBlock"] = tryBlock.ToString();
            catchBlockInfo.OperationFeatures["ParentNodeType"] = FindParent(tryBlock).RawKind;
            catchBlockInfo.MetaInfo["ParentNodeType"] = FindParent(tryBlock).Kind().ToString();

            //Common Features - try/catch block
            var tryFileLinePositionSpan = tree.GetLineSpan(tryBlock.Block.Span);
            var tryStartLine = tryFileLinePositionSpan.StartLinePosition.Line + 1;
            var tryEndLine = tryFileLinePositionSpan.EndLinePosition.Line + 1;
            
            catchBlockInfo.OperationFeatures["TryLine"] = tryStartLine;
            catchBlockInfo.MetaInfo["TryLine"] = tryStartLine.ToString();
            catchBlockInfo.OperationFeatures["TryLOC"] = tryEndLine - tryStartLine + 1;

            var catchFileLinePositionSpan = tree.GetLineSpan(catchblock.Block.Span);
            var catchStartLine = catchFileLinePositionSpan.StartLinePosition.Line + 1;
            var catchEndLine = catchFileLinePositionSpan.EndLinePosition.Line + 1;

            catchBlockInfo.OperationFeatures["CatchLine"] = catchStartLine;
            catchBlockInfo.OperationFeatures["CatchLOC"] = catchEndLine - catchStartLine + 1;

            catchBlockInfo.MetaInfo["CatchBlock"] = catchblock.ToString();
            
            catchBlockInfo.FilePath = tree.FilePath;
            catchBlockInfo.MetaInfo["FilePath"] = tree.FilePath;

            //Common Features - parent type
            catchBlockInfo.ParentType = FindParentType(tryBlock, model);
            catchBlockInfo.MetaInfo["ParentType"] = catchBlockInfo.ParentType;

            //Common Features - parent method name
            SyntaxNode parentNode = FindParentMethod(tryBlock);
            catchBlockInfo.ParentMethod = FindParentMethodName(parentNode);
            catchBlockInfo.MetaInfo["ParentMethod"] = catchBlockInfo.ParentMethod;

            //Common Features
            var parentMethodFileLinePositionSpan = tree.GetLineSpan(parentNode.Span);
            var parentMethodStartLine = parentMethodFileLinePositionSpan.StartLinePosition.Line + 1;
            var parentMethodEndLine = parentMethodFileLinePositionSpan.EndLinePosition.Line + 1;

            catchBlockInfo.OperationFeatures["MethodLine"] = parentMethodStartLine;
            catchBlockInfo.OperationFeatures["MethodLOC"] = parentMethodEndLine - parentMethodStartLine + 1;


            //Treatment for TryStatement
            bool hasTryStatement = catchblock.DescendantNodesAndSelf()
                      .OfType<TryStatementSyntax>().Any();
            SyntaxNode updatedCatchBlock = catchblock;
            if (hasTryStatement == true)
            {
                try {
                    // remove try-catch-finally block inside
                    updatedCatchBlock = tryblockremover.Visit(catchblock);
                }
                catch (System.ArgumentNullException e)
                {
                    // ignore the ArgumentNullException 
                }
            }

            //Treatment for TryStatement
            //RecoverFlag - (based on inner try blocks)
            var recoverStatement = FindRecoverStatement(catchblock, model);
            if (recoverStatement != null)
            {
                catchBlockInfo.MetaInfo["RecoverFlag"] = recoverStatement.ToString();
                catchBlockInfo.OperationFeatures["RecoverFlag"] = 1;
            }
            /*
             * Flagging inner catch
             * CatchClause is a child of a TryStatement, which is a child of a Block, which we wanna know the parent.
             * If CatchClause it's the parent, then it's an inner catch. Get the line of the parent try of that.
             */
            if (IsInnerCatch(catchblock.Parent))
            {
                catchBlockInfo.OperationFeatures["InnerCatch"] =  1;
                catchBlockInfo.OperationFeatures["ParentTryStartLine"] = tree.GetLineSpan((catchblock.Parent.Parent.Parent.Parent as TryStatementSyntax).Block.Span).StartLinePosition.Line + 1;// tree.getLineNumber(node.getParent().getParent().getParent().getParent().getStartPosition() + 1));
            }

            //Treatment for MethodInvocation
            //Collection of data for statements of: logging, abort, 
            
            //Logging
            var loggingStatement = FindLoggingIn(updatedCatchBlock);
            if (loggingStatement != null)
            {
                catchBlockInfo.MetaInfo["Logged"] = loggingStatement.ToString();
                catchBlockInfo.OperationFeatures["Logged"] = 1;

                if(CountLoggingIn(updatedCatchBlock) > 1)
                    catchBlockInfo.OperationFeatures["MultiLog"] = 1;
            }

            //Abort
            var abortStatement = FindAbortIn(updatedCatchBlock);
            if (abortStatement != null)
            {
                catchBlockInfo.MetaInfo["Abort"] = abortStatement.ToString();
                catchBlockInfo.OperationFeatures["Abort"] = 1;
            }

            //GetCause - C# is inner exception
            var getCauseStatement = FindGetCauseIn(updatedCatchBlock);
            if (getCauseStatement != null)
            {
                catchBlockInfo.MetaInfo["GetCause"] = getCauseStatement.ToString();
                catchBlockInfo.OperationFeatures["GetCause"] = 1;
            }

            //Other - Other INVOCATION
            var otherStatement = FindOtherIn(updatedCatchBlock);
            if (otherStatement != null)
            {
                catchBlockInfo.MetaInfo["OtherInvocation"] = otherStatement.ToString();
                catchBlockInfo.OperationFeatures["OtherInvocation"] = 1;
            }


            //Treatment for ThrowStatement
            //Collection of data for statements of: throw 
            var throwStatement = FindThrowIn(updatedCatchBlock);
            if (throwStatement != null)
            {
                catchBlockInfo.MetaInfo["Thrown"] = throwStatement.ToString();
                catchBlockInfo.OperationFeatures["NumThrown"] = CountThrowIn(updatedCatchBlock);
                catchBlockInfo.OperationFeatures["NumThrowNew"] = CountThrowNewIn(updatedCatchBlock);
                catchBlockInfo.OperationFeatures["NumThrowWrapCurrentException"] = CountThrowWrapIn(updatedCatchBlock, catchblock.Declaration?.Identifier.ToString());
            }

            //Treatment for ReturnStatement
            var returnStatement = FindReturnIn(updatedCatchBlock);
            if (returnStatement != null)
            {
                catchBlockInfo.MetaInfo["Return"] = returnStatement.ToString();
                catchBlockInfo.OperationFeatures["Return"] = 1;
            }

            //Treatment for ContinueStatement
            var continueStatement = FindContinueIn(updatedCatchBlock);
            if (continueStatement != null)
            {
                catchBlockInfo.MetaInfo["Continue"] = continueStatement.ToString();
                catchBlockInfo.OperationFeatures["Continue"] = 1;
            }

            //var setLogicFlag = FindSetLogicFlagIn(updatedCatchBlock);
            //if (setLogicFlag != null)
            //{
            //    catchBlockInfo.MetaInfo["SetLogicFlag"] = setLogicFlag.ToString();
            //    catchBlockInfo.OperationFeatures["SetLogicFlag"] = 1;
            //}

            //var otherOperation = HasOtherOperation(updatedCatchBlock, model);
            //if (otherOperation != null)
            //{
            //    catchBlockInfo.MetaInfo["OtherOperation"] = otherOperation.ToString();
            //    catchBlockInfo.OperationFeatures["OtherOperation"] = 1;
            //}

            //EmptyBlock
            if (IsEmptyBlock(updatedCatchBlock))
                catchBlockInfo.OperationFeatures["EmptyBlock"] = 1;

            //CatchException
            if (exceptionNamedTypeSymbol != null) 
                if (exceptionNamedTypeSymbol.Equals(compilation.GetTypeByMetadataName("System.Exception")))
                    catchBlockInfo.OperationFeatures["CatchException"] = 1;
                else
                    catchBlockInfo.OperationFeatures["CatchException"] = 0;
            
            //ToDo
            if (IsToDo(updatedCatchBlock))
                catchBlockInfo.OperationFeatures["ToDo"] = 1;

            //FinallyThrowing
            FinallyClauseSyntax finallyBlock = tryBlock.Finally;
            if(finallyBlock != null)
            {
                catchBlockInfo.MetaInfo["FinallyBlock"] = finallyBlock.ToString();
                if (finallyBlock.DescendantNodes().OfType<ThrowStatementSyntax>().Any())
                    catchBlockInfo.OperationFeatures["FinallyThrowing"] = 1;
            }
            
            //var variableAndComments = GetVariablesAndComments(tryBlock.Block);
            //var containingMethod = GetContainingMethodName(tryBlock, model);
            //var methodNameList = GetAllInvokedMethodNamesByBFS(tryBlock.Block, treeAndModelDic, compilation);

            var possibleExceptionsCustomVisitor = new PossibleExceptionsCustomVisitor(ref exceptionNamedTypeSymbol, ref treeAndModelDic, ref compilation, true, 0);
            possibleExceptionsCustomVisitor.Visit(tryBlock.Block);

            //catchBlockInfo.MetaInfo["TryMethods"] = possibleExceptionsCustomVisitor.PrintInvokedMethodsHandlerType();
            catchBlockInfo.MetaInfo["TryMethodsAndExceptions"] = possibleExceptionsCustomVisitor.PrintInvokedMethodsPossibleExceptions();
            
            catchBlockInfo.OperationFeatures["NumMethod"] = possibleExceptionsCustomVisitor.countInvokedMethodsHandlerType();

            catchBlockInfo.MetaInfo["TryMethodsBinded"] = possibleExceptionsCustomVisitor.PrintInvokedMethodsBinded();

            catchBlockInfo.OperationFeatures["NumMethodsNotBinded"] = possibleExceptionsCustomVisitor.getNumMethodsNotBinded();

            catchBlockInfo.MetaInfo["DistinctExceptions"] = possibleExceptionsCustomVisitor.PrintDistinctPossibleExceptions();
            catchBlockInfo.OperationFeatures["NumDistinctExceptions"] = possibleExceptionsCustomVisitor.getNumDistinctPossibleExceptions();

            catchBlockInfo.OperationFeatures["NumSpecificHandler"] = possibleExceptionsCustomVisitor.getNumSpecificHandler();
            catchBlockInfo.OperationFeatures["NumSubsumptionHandler"] = possibleExceptionsCustomVisitor.getNumSubsumptionHandler();
            catchBlockInfo.OperationFeatures["NumSupersumptionHandler"] = possibleExceptionsCustomVisitor.getNumSupersumptionHandler();
            catchBlockInfo.OperationFeatures["NumOtherHandler"] = possibleExceptionsCustomVisitor.getNumOtherHandler();

            catchBlockInfo.OperationFeatures["MaxLevel"] = possibleExceptionsCustomVisitor.getChildrenMaxLevel();
            catchBlockInfo.OperationFeatures["IsXMLSemantic"] = possibleExceptionsCustomVisitor.getNumIsXMLSemantic();
            catchBlockInfo.OperationFeatures["IsXMLSyntax"] = possibleExceptionsCustomVisitor.getNumIsXMLSyntax();
            //catchBlockInfo.OperationFeatures["IsLoop"] = possibleExceptionsCustomVisitor.getNumIsLoop();
            catchBlockInfo.OperationFeatures["IsThrow"] = possibleExceptionsCustomVisitor.getNumIsThrow();

            //var methodAndExceptionList = GetAllInvokedMethodNamesAndExceptionsByBFS(tryBlock.Block, treeAndModelDic, compilation);

            //catchBlockInfo.OperationFeatures["NumMethod"] = methodAndExceptionList[0].Count;
            //catchBlockInfo.OperationFeatures["NumExceptions"] = methodAndExceptionList[1].Count;
            //catchBlockInfo.TextFeatures = methodAndExceptionList[0];
            //if (containingMethod != null)
            //{
            //    MergeDic<string>(ref catchBlockInfo.TextFeatures,
            //        new Dictionary<string, int>() { { containingMethod, 1 } });
            //}
            //MergeDic<string>(ref catchBlockInfo.TextFeatures,
            //        new Dictionary<string, int>() { { "##spliter##", 0 } }); // to seperate methods and variables
            //MergeDic<string>(ref catchBlockInfo.TextFeatures, variableAndComments);

            return catchBlockInfo;
        }

        public static Dictionary<string, MyMethod> GetAllMethodDeclarations(SyntaxTree tree,
            Dictionary<SyntaxTree, SemanticModel> treeAndModelDic, Compilation compilation)
        {
            var allMethodDeclarations = new Dictionary<string, MyMethod>();

            var root = tree.GetRoot();
            var methodDeclarList = root.DescendantNodes().OfType<BaseMethodDeclarationSyntax>();
            var model = treeAndModelDic[tree];
            var modelBackup = compilation.GetSemanticModel(tree);
            foreach (var method in methodDeclarList)
            {
                ISymbol methodSymbol = null;
                try
                {
                    methodSymbol = model.GetDeclaredSymbol(method);
                }
                catch
                {
                    try
                    {
                        methodSymbol = modelBackup.GetDeclaredSymbol(method);
                    }
                    catch { }
                }
                if (methodSymbol != null)
                {
                    var methodDeclaration = methodSymbol.ToString();
                    if (methodDeclaration != null && !allMethodDeclarations.ContainsKey(methodDeclaration))
                    {
                        allMethodDeclarations.Add(methodDeclaration, new MyMethod(methodDeclaration, method));
                    }
                }
            }
            return allMethodDeclarations;
        }
        #endregion Tree Analysis

        #region Invocation Statement checking
        /// <summary>
        /// To check whether an invocation is a logging statement
        /// </summary>
        static public bool IsLoggingStatement(SyntaxNode statement)
        {
            string logging = IOFile.MethodNameExtraction(statement.ToString());
            if (logging == null) return false;

            foreach (string notlogmethod in Config.NotLogMethods)
            {
                if (notlogmethod == "") break;
                if (logging.IndexOf(notlogmethod) > -1)
                {
                    return false;
                }
            }
            foreach (string logmethod in Config.LogMethods)
            {
                if (logging.IndexOf(logmethod) > -1)
                {
                    return true;
                }
            }
            return false;
        }

        static public InvocationExpressionSyntax FindLoggingIn(SyntaxNode codeSnippet)
        {
            InvocationExpressionSyntax loggingStatement;
            try
            {
                loggingStatement = codeSnippet.DescendantNodesAndSelf()
                        .OfType<InvocationExpressionSyntax>().First(IsLoggingStatement);
                return loggingStatement;
            }
            catch
            {
                // has no InvocationExpressionSyntax
                return null;
            }
        }

        static public int CountLoggingIn(SyntaxNode codeSnippet)
        {
            int loggingStatementCount;
            try
            {
                loggingStatementCount = codeSnippet.DescendantNodesAndSelf()
                        .OfType<InvocationExpressionSyntax>().Count(IsLoggingStatement);
                return loggingStatementCount;
            }
            catch
            {
                // has no InvocationExpressionSyntax
                return 0;
            }
        }

        public static bool IsLogOnly(SyntaxNode codeSnippet, SemanticModel semanticModel)
        {
            //if (codeSnippet is CatchClauseSyntax)
            //{
            //    codeSnippet = (codeSnippet as CatchClauseSyntax).Block;
            //}
            
            var statementNodes = codeSnippet.DescendantNodes().OfType<StatementSyntax>();
            int statementCount = 0;
            int logStatementCount = 0;
            int nonLogStatementCount = 0;

            //Is there any statement that is different than log?
            foreach (var statement in statementNodes)
            {
                if (!statement.DescendantNodes().OfType<StatementSyntax>().Any() 
                    && !statement.IsKind(SyntaxKind.Block)) //get each leaf statement node
                {
                    statementCount++;
                    //Console.WriteLine(statement.ToString());
                    if (IsLoggingStatement(statement))
                        logStatementCount++;
                        
                    if ((IsRecoverStatement(statement, semanticModel))
                        || (statement is ReturnStatementSyntax) || IsSetLogicFlagStatement(statement)
                        || IsThrow(statement))
                    {
                        nonLogStatementCount++;
                    }
                }
            }
            //warning to detect statements that are not covered:
            if (logStatementCount + nonLogStatementCount != statementCount)
                Logger.Log("Warning: Potential statement type not covered! LogStatements: " + logStatementCount + ". NonLogStatements: " + nonLogStatementCount + ". Total statements: " + statementCount);
            if (nonLogStatementCount == 0 && logStatementCount > 0)
                return true;
            else
                return false;

            //// All return true when content is empty. Added check to make sure is not empty.
            //// 
            //bool bIsLogOnly = (codeSnippet.DescendantNodes().OfType<Syntax Node>().All(IsLoggingStatement)) 
            //                    && (codeSnippet.DescendantNodes().OfType<SyntaxNode>().Any());
            //return bIsLogOnly;
        }

        /// <summary>
        /// To check whether an invocation is a logging statement
        /// </summary>
        static public bool IsAbortStatement(SyntaxNode statement)
        {
            string aborting = IOFile.MethodNameExtraction(statement.ToString());
            if (aborting == null) return false;

            string[] abortMethods = { "Exit", "Abort" };

            foreach (string abortMethod in abortMethods)
            {
                if (aborting.IndexOf(abortMethod) > -1)
                {
                    return true;
                }
            }
            return false;
        }

        static public InvocationExpressionSyntax FindAbortIn(SyntaxNode codeSnippet)
        {
            InvocationExpressionSyntax abortStatement;
            try
            {
                abortStatement = codeSnippet.DescendantNodesAndSelf()
                        .OfType<InvocationExpressionSyntax>().First(IsAbortStatement);
                return abortStatement;
            }
            catch
            {
                // has no InvocationExpressionSyntax
                return null;
            }
        }

        static public bool IsGetCauseStatement(SyntaxNode statement)
        {
            string getCause = IOFile.MethodNameExtraction(statement.ToString());
            if (getCause == null) return false;

            string[] getCauseMethods = { "InnerException" };

            foreach (string getCauseMethod in getCauseMethods)
            {
                if (getCause.IndexOf(getCauseMethod) > -1)
                {
                    return true;
                }
            }
            return false;
        }

        static public MemberAccessExpressionSyntax FindGetCauseIn(SyntaxNode codeSnippet)
        {
            MemberAccessExpressionSyntax getCauseStatement;
            try
            {
                getCauseStatement = codeSnippet.DescendantNodesAndSelf()
                        .OfType<MemberAccessExpressionSyntax>().First(IsGetCauseStatement);
                return getCauseStatement;
            }
            catch
            {
                // has no InvocationExpressionSyntax
                return null;
            }
        }

        static public InvocationExpressionSyntax FindOtherIn(SyntaxNode codeSnippet)
        {

            InvocationExpressionSyntax otherStatement;
            try
            {
                otherStatement = codeSnippet.DescendantNodesAndSelf()
                        .OfType<InvocationExpressionSyntax>().First( statement =>   !IsAbortStatement(statement) 
                                                                                    && !IsLoggingStatement (statement)
                                                                                    && !IsGetCauseStatement(statement)
                                                                    );
                return otherStatement;
            }
            catch
            {
                // has no InvocationExpressionSyntax
                return null;
            }
            
        }
        #endregion Statement checking

       

        #region ETC
        public static Dictionary<string, int> GetVariablesAndComments(SyntaxNode codeSnippet)
        {
            Dictionary<string, int> variableAndComments = new Dictionary<string, int>();

            bool hasTryStatement = codeSnippet.DescendantNodes()
                .OfType<TryStatementSyntax>().Any();
            if (hasTryStatement == true)
            {
                codeSnippet = tryblockremover.Visit(codeSnippet);
            }

            var variableList = codeSnippet.DescendantNodes().OfType<IdentifierNameSyntax>()
                .Where(variable => !SyntaxFacts.IsInTypeOnlyContext(variable));
            foreach (var variable in variableList)
            {
                var variableName = IOFile.MethodNameExtraction(variable.ToString());
                MergeDic<String>(ref variableAndComments,
                    new Dictionary<string, int>() { { variableName, 1 } });
            }

            var commentList = codeSnippet.DescendantTrivia()
                .Where(childNode => childNode.IsKind(SyntaxKind.SingleLineCommentTrivia)
                    || childNode.IsKind(SyntaxKind.MultiLineCommentTrivia));
            foreach (var comment in commentList)
            {
                string updatedComment = IOFile.DeleteSpace(comment.ToString());
                updatedComment = Regex.Replace(updatedComment, "<.*>", "");
                updatedComment = Regex.Replace(updatedComment, "{.*}", "");
                updatedComment = Regex.Replace(updatedComment, "\\(.*\\)", "");
                MergeDic<String>(ref variableAndComments,
                    new Dictionary<string, int>() { { updatedComment, 1 } });
            }
            return variableAndComments;
        }

        public static string GetContainingMethodName(SyntaxNode codeSnippet, SemanticModel model)
        {
            // Method name
            SyntaxNode method = null;
            string methodName = null;
            try
            {
                method = codeSnippet.Ancestors().OfType<BaseMethodDeclarationSyntax>().First();
            }
            catch
            {
                // Skip method type: e.g., operator method
            }

            ISymbol methodSymbol;
            if (method != null)
            {
                if (method is MethodDeclarationSyntax)
                {
                    var methodDeclaration = method as MethodDeclarationSyntax;
                    try
                    {
                        methodSymbol = model.GetDeclaredSymbol(methodDeclaration);
                        methodName = methodSymbol.ToString();
                    }
                    catch
                    {
                        methodName = methodDeclaration.Identifier.ValueText;
                    }
                }
                else if (method is ConstructorDeclarationSyntax)
                {
                    var methodDeclaration = method as ConstructorDeclarationSyntax;
                    try
                    {
                        methodSymbol = model.GetDeclaredSymbol(methodDeclaration);
                        methodName = methodSymbol.ToString();
                    }
                    catch
                    {
                        methodName = methodDeclaration.Identifier.ValueText;
                    }
                }
            }
            return IOFile.MethodNameExtraction(methodName);
        }

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
        #endregion ETC

        #region Other Statement checking

        public static bool IsThrow(SyntaxNode statement)
        {
            if (statement is ThrowStatementSyntax) return true;
            try
            {
                var invocation = statement.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().First();
                Regex regex = new Regex(@"(?i)Throw.*Exception");
                if (regex.Match(invocation.ToString()).Success)
                {
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
            return false;
        }

        static public ThrowStatementSyntax FindThrowIn(SyntaxNode codeSnippet)
        {
            ThrowStatementSyntax throwStatement;
            try
            {
                throwStatement = codeSnippet.DescendantNodes().OfType<ThrowStatementSyntax>().First();
                return throwStatement;
            }
            catch
            {
                return null;
            }
        }
        static public int CountThrowIn(SyntaxNode codeSnippet)
        {
            int countThrowStatement;
            try
            {
                countThrowStatement = codeSnippet.DescendantNodes().OfType<ThrowStatementSyntax>().Count();
                return countThrowStatement;
            }
            catch
            {
                return 0;
            }
        }
        static public int CountThrowNewIn(SyntaxNode codeSnippet)
        {
            int countThrowStatement = 0;
            try
            {
                countThrowStatement = codeSnippet.DescendantNodes().OfType<ThrowStatementSyntax>().Sum(node => node.DescendantNodes().OfType<ObjectCreationExpressionSyntax>().Count());
                return countThrowStatement;
            }
            catch
            {
                return 0;
            }
        }
        static public int CountThrowWrapIn(SyntaxNode codeSnippet, string exceptionIdentifier)
        {
            int countThrowStatement = 0;
            try
            {
                countThrowStatement = codeSnippet.DescendantNodes().OfType<ThrowStatementSyntax>().Sum
                                        (
                                            node => node.DescendantNodes().OfType<IdentifierNameSyntax>().Count
                                            (
                                                id => id.ToString().Equals(exceptionIdentifier)
                                            )
                                         );
                return countThrowStatement;
            }
            catch
            {
                return 0;
            }
        }
        
        static public bool IsSetLogicFlagStatement(SyntaxNode statement)
        {
            try 
            {
                var expression = statement.DescendantNodesAndSelf().OfType<BinaryExpressionSyntax>().First();

                if (expression.IsKind(SyntaxKind.SimpleAssignmentExpression))
                {
                    var node = expression.Right;
                    if (node.IsKind(SyntaxKind.SimpleMemberAccessExpression) 
                        || node.IsKind(SyntaxKind.FalseLiteralExpression)
                        || node.IsKind(SyntaxKind.TrueLiteralExpression)
                        || node.IsKind(SyntaxKind.NullLiteralExpression))
                    {
                        //Console.WriteLine(node.ToString());
                        return true;
                    }
                }                  
                return false;
            }
            catch
            {
                return false;
            }
        }

        static public BinaryExpressionSyntax FindSetLogicFlagIn(SyntaxNode codeSnippet)
        {
            BinaryExpressionSyntax setLogicFlagStatement;
            try
            {
                setLogicFlagStatement = codeSnippet.DescendantNodes().OfType<BinaryExpressionSyntax>()
                    .First(IsSetLogicFlagStatement);    
                return setLogicFlagStatement;
            }
            catch
            {
                return null;
            }
        }

        public static ReturnStatementSyntax FindReturnIn(SyntaxNode codeSnippet)
        {
            ReturnStatementSyntax returnStatement;
            try
            {
                returnStatement = codeSnippet.DescendantNodes().OfType<ReturnStatementSyntax>().First();
                return returnStatement;
            }
            catch
            {
                return null;
            }
        }
        public static ContinueStatementSyntax FindContinueIn(SyntaxNode codeSnippet)
        {
            ContinueStatementSyntax continueStatement;
            try
            {
                continueStatement = codeSnippet.DescendantNodes().OfType<ContinueStatementSyntax>().First();
                return continueStatement;
            }
            catch
            {
                return null;
            }
        }

        public static bool IsRecoverStatement(SyntaxNode statement, SemanticModel semanticModel)               
        {
            if (!IsLoggingStatement(statement) && !IsSetLogicFlagStatement(statement) && !IsThrow(statement))
            {
                var recoverStatementSet = statement.DescendantNodes().OfType<IdentifierNameSyntax>();
                foreach (var recoverStatement in recoverStatementSet)
                {
                    try
                    {
                        var symbol = semanticModel.GetSymbolInfo(recoverStatement).Symbol as ILocalSymbol;
                        string typeName = symbol.Type.ToString();
                        if (typeName.Contains("Exception"))
                        {
                            // To check
                            return true;
                        }
                    }
                    catch { }
                }
            }
            return false;
        }

        public static StatementSyntax FindRecoverStatement(SyntaxNode codeSnippet, SemanticModel semanticModel)
        {
            StatementSyntax recoverStatement;
            try
            {
                try
                {
                    recoverStatement = codeSnippet.DescendantNodesAndSelf()
                            .OfType<TryStatementSyntax>().First();
                    return recoverStatement;
                }
                catch
                {
                    // has no try statement inside
                }

                recoverStatement = codeSnippet.DescendantNodes().OfType<StatementSyntax>()
                    .First(statement => IsRecoverStatement(statement, semanticModel)
                    && !statement.IsKind(SyntaxKind.Block));
                return recoverStatement;
            }
            catch
            {
                return null;
            }
        }

        //static public StatementSyntax HasOtherOperation(SyntaxNode codeSnippet, SemanticModel semanticModel)
        //{
        //    var statementNodes = codeSnippet.DescendantNodes().OfType<StatementSyntax>();
        //    foreach (var statement in statementNodes)
        //    {
        //        if (!statement.DescendantNodes().OfType<StatementSyntax>().Any() 
        //            && !statement.IsKind(SyntaxKind.Block)) //get each leaf statement node
        //        {
        //            //Console.WriteLine(statement.ToString());
        //            if (!IsLoggingStatement(statement) && !(IsRecoverStatement(statement, semanticModel))
        //                && !(statement is ReturnStatementSyntax) && !IsSetLogicFlagStatement(statement)
        //                && !IsThrow(statement))
        //            {
        //                return statement;
        //            }
        //        }
        //    }
        //    return null;
        //}
        #endregion Other Statement checking

        #region CatchClause and binded info analysis
        public static bool IsEmptyBlock(SyntaxNode codeSnippet)
        {
            if (codeSnippet is CatchClauseSyntax)
            {
                codeSnippet = (codeSnippet as CatchClauseSyntax).Block;
            }
            bool isEmpty = !codeSnippet.DescendantNodes().OfType<SyntaxNode>().Any();
            return isEmpty;
        }

        public static bool IsToDo(SyntaxNode codeSnippet)
        {
            if (codeSnippet is CatchClauseSyntax)
            {
                codeSnippet = (codeSnippet as CatchClauseSyntax).Block;
            }
            bool bIsToDo = false;

            var commentList = codeSnippet.DescendantTrivia()
                .Where(childNode => childNode.IsKind(SyntaxKind.SingleLineCommentTrivia)
                    || childNode.IsKind(SyntaxKind.MultiLineCommentTrivia));
            foreach (var comment in commentList)
            {
                string updatedComment = IOFile.DeleteSpace(comment.ToString());
                updatedComment = Regex.Replace(updatedComment, "<.*>", "");
                updatedComment = Regex.Replace(updatedComment, "{.*}", "");
                updatedComment = Regex.Replace(updatedComment, "\\(.*\\)", "");
                updatedComment = updatedComment.ToUpper();

                bIsToDo = updatedComment.Contains("TODO") || updatedComment.Contains("FIXME");

            }

            return bIsToDo;
        }

        private static int FindKind(INamedTypeSymbol exceptionType, Compilation compilation)
        {
            if (exceptionType.Equals(compilation.GetTypeByMetadataName("System.SystemException")) || exceptionType.Equals(compilation.GetTypeByMetadataName("System.ApplicationException")))
            {
                return 0;
            }
            else if (exceptionType.Equals(compilation.GetTypeByMetadataName("System.Exception")))
            {
                return 1;
            }
            else if (exceptionType.Equals(compilation.GetTypeByMetadataName("System.Object")))
            {
                return -1;
            }
            else
                return FindKind(exceptionType.BaseType, compilation);
        }

        private static SyntaxNode FindParent(SyntaxNode node)
        {
            SyntaxNode parentNode = node.Parent;

            if (!(parentNode.IsKind(SyntaxKind.Block)))
                return parentNode;

            return FindParent(parentNode);
        }

        private static SyntaxNode FindParentMethod(SyntaxNode node)
        {
            SyntaxNode parentNode = node.Parent;

            if (parentNode.IsKind(SyntaxKind.MethodDeclaration))
                return parentNode;
            if (parentNode.IsKind(SyntaxKind.ConstructorDeclaration))
                return parentNode;
            if (parentNode.IsKind(SyntaxKind.ClassDeclaration))
                return parentNode;

            return FindParentMethod(parentNode);
        }

        private static string FindParentMethodName(SyntaxNode parentNode)
        {
            string parentMethodName;
            if (parentNode.IsKind(SyntaxKind.MethodDeclaration))
            {
                MethodDeclarationSyntax parentMethod = parentNode as MethodDeclarationSyntax;

                parentMethodName = '"' + parentMethod.Identifier.ToString();
                parentMethodName += "(";

                foreach (var param in parentMethod.ParameterList.Parameters)
                {
                    parentMethodName += param.Type.ToString() + ";";
                }
                parentMethodName += ")" + '"';

                parentMethodName = parentMethodName.Replace(";)", ")");
            }
            else if (parentNode.IsKind(SyntaxKind.ConstructorDeclaration))
            {
                ConstructorDeclarationSyntax parentConstructor = parentNode as ConstructorDeclarationSyntax;

                parentMethodName = '"' + parentConstructor.Identifier.ToString();
                parentMethodName += "(";

                foreach (var param in parentConstructor.ParameterList.Parameters)
                {
                    parentMethodName += param.Type.ToString() + ";";
                }
                parentMethodName += ")" + '"';

                parentMethodName = parentMethodName.Replace(";)", ")");
            }
            else
                parentMethodName = "!UNEXPECTED_KIND!"; //there might be other cases like the initializer one for java

            return parentMethodName;
        }

        private static string FindParentType(SyntaxNode node, SemanticModel model)
        {
            SyntaxNode parentNode = node.Parent;

            if (parentNode.IsKind(SyntaxKind.ClassDeclaration))
            {
                ClassDeclarationSyntax type = parentNode as ClassDeclarationSyntax;
                if (model.GetDeclaredSymbol(type) != null)
                    return model.GetDeclaredSymbol(type).ToString();
                else
                    return ((NamespaceDeclarationSyntax)parentNode.Parent).Name.ToString() + "." + type.Identifier.ToString();
            }

            return FindParentType(parentNode, model);
        }

        private static bool IsInnerCatch(SyntaxNode node)
        {
            if (node.IsKind(SyntaxKind.MethodDeclaration) || node.IsKind(SyntaxKind.ConstructorDeclaration) || node.IsKind(SyntaxKind.ClassDeclaration))
                return false;

            if (node.IsKind(SyntaxKind.CatchClause))
                return true;

            return IsInnerCatch(node.Parent);
            
        }

        public static SyntaxNode FindParentTry(SyntaxNode node)
        {
            //if reach method, constructor and class stop because went too far
            if (node.IsKind(SyntaxKind.MethodDeclaration) || node.IsKind(SyntaxKind.ConstructorDeclaration) || node.IsKind(SyntaxKind.ClassDeclaration))
                return null;
            
            //if reached catch clause means it can still pop out of the try statement. A catch clause is also a child node of a try statement =(
            //null here so that it doesnt accuse as parent try
            //check if there are catch blocks with throw statements
            if (node.IsKind(SyntaxKind.CatchClause))
                return null;

            if (node.IsKind(SyntaxKind.TryStatement))
                return node;

            return FindParentTry(node.Parent);

        }
        #endregion CatchClause and binded info analysis
    }
}
