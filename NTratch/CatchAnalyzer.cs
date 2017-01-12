using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NTratch
{
    /// <summary>
    /// Catch Analyzer represents a collection of CatchBlocks of a given tree
    /// </summary>
    class CatchAnalyzer
    {
        public List<CatchBlock> Catches { get; } = new List<CatchBlock>();
        public List<PossibleExceptionsBlock> PossibleExceptionsList { get; } = new List<PossibleExceptionsBlock>();

        public static TryStatementRemover tryblockremover = new TryStatementRemover();

        Dictionary<SyntaxTree, SemanticModel> TreeAndModelDic { get; set; }
        Compilation Compilation { get; set; }

        public CatchAnalyzer(Dictionary<SyntaxTree, SemanticModel> treeAndModelDic, Compilation compilation)
        {
            TreeAndModelDic = treeAndModelDic;
            Compilation = compilation;
        }

        public void AnalyzeACatchBlock(CatchClauseSyntax catchblock)
        {
            CatchBlock catchBlockInfo = new CatchBlock();

            var tree = catchblock.SyntaxTree;
            var model = TreeAndModelDic[tree];

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
                        catchBlockInfo.OperationFeatures["Kind"] = ASTUtilities.FindKind(exceptionNamedTypeSymbol, Compilation);
                    }
                    else
                        catchBlockInfo.OperationFeatures["Binded"] = 0;
                }
                else
                    catchBlockInfo.ExceptionType = "!NO_NAMED_TYPE!";
            }
            else
                catchBlockInfo.ExceptionType = "!NO_EXCEPTION_DECLARED!";

            //Basic info:
            catchBlockInfo.MetaInfo["ExceptionType"] = catchBlockInfo.ExceptionType;

            //Try info:
            var tryBlock = catchblock.Parent as TryStatementSyntax;
            catchBlockInfo.MetaInfo["TryBlock"] = tryBlock.ToString();
            catchBlockInfo.OperationFeatures["ParentNodeType"] = ASTUtilities.FindParent(tryBlock).RawKind;
            catchBlockInfo.MetaInfo["ParentNodeType"] = ASTUtilities.FindParent(tryBlock).Kind().ToString();

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

            catchBlockInfo.OperationFeatures["CatchStart"] = catchFileLinePositionSpan.StartLinePosition.Line;
            catchBlockInfo.OperationFeatures["CatchLength"] = catchFileLinePositionSpan.EndLinePosition.Line - catchFileLinePositionSpan.StartLinePosition.Line;

            catchBlockInfo.MetaInfo["CatchBlock"] = catchblock.ToString();

            catchBlockInfo.FilePath = tree.FilePath;
            catchBlockInfo.StartLine = catchStartLine;

            catchBlockInfo.MetaInfo["FilePath"] = tree.FilePath;
            catchBlockInfo.MetaInfo["StartLine"] = catchStartLine.ToString();

            //Common Features - parent type
            catchBlockInfo.ParentType = ASTUtilities.FindParentType(tryBlock, model);
            catchBlockInfo.MetaInfo["ParentType"] = catchBlockInfo.ParentType;

            //Common Features - parent method name
            SyntaxNode parentNode = ASTUtilities.FindParentMethod(tryBlock);
            catchBlockInfo.ParentMethod = ASTUtilities.FindParentMethodName(parentNode);
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
                try
                {
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
                catchBlockInfo.OperationFeatures["InnerCatch"] = 1;
                catchBlockInfo.OperationFeatures["ParentTryStartLine"] = tree.GetLineSpan((FindParentCatch(catchblock.Parent).Parent as TryStatementSyntax).Block.Span).StartLinePosition.Line + 1;// tree.getLineNumber(node.getParent().getParent().getParent().getParent().getStartPosition() + 1));
            }

            //Treatment for MethodInvocation
            //Collection of data for statements of: logging, abort, 

            //Logging
            var loggingStatement = FindLoggingIn(updatedCatchBlock);
            if (loggingStatement != null)
            {
                catchBlockInfo.MetaInfo["Logged"] = loggingStatement.ToString();
                catchBlockInfo.OperationFeatures["Logged"] = 1;

                if (CountLoggingIn(updatedCatchBlock) > 1)
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
                if (exceptionNamedTypeSymbol.Equals(Compilation.GetTypeByMetadataName("System.Exception")))
                    catchBlockInfo.OperationFeatures["CatchException"] = 1;
                else
                    catchBlockInfo.OperationFeatures["CatchException"] = 0;

            //ToDo
            if (IsToDo(updatedCatchBlock))
                catchBlockInfo.OperationFeatures["ToDo"] = 1;

            //var variableAndComments = GetVariablesAndComments(tryBlock.Block);
            //var containingMethod = GetContainingMethodName(tryBlock, model);
            //var methodNameList = GetAllInvokedMethodNamesByBFS(tryBlock.Block, treeAndModelDic, compilation);

            var tryPossibleExceptionsCustomVisitor = new PossibleExceptionsCustomVisitor(Compilation, TreeAndModelDic, 0, true, tree.FilePath, catchStartLine, exceptionNamedTypeSymbol);
            tryPossibleExceptionsCustomVisitor.Visit(tryBlock.Block);

            /*
			* Process for possible exceptions
			*/
            getExceptionFlows(this.PossibleExceptionsList, tryPossibleExceptionsCustomVisitor.getClosedExceptionFlows(), Compilation);


            //catchBlockInfo.MetaInfo["TryMethods"] = possibleExceptionsCustomVisitor.PrintInvokedMethodsHandlerType();
            catchBlockInfo.MetaInfo["TryMethodsAndExceptions"] = tryPossibleExceptionsCustomVisitor.PrintInvokedMethodsPossibleExceptions();

            catchBlockInfo.OperationFeatures["NumDistinctMethods"] = tryPossibleExceptionsCustomVisitor.countInvokedMethodsHandlerType();

            catchBlockInfo.MetaInfo["TryMethodsBinded"] = tryPossibleExceptionsCustomVisitor.PrintInvokedMethodsBinded();

            catchBlockInfo.OperationFeatures["NumDistinctMethodsNotBinded"] = tryPossibleExceptionsCustomVisitor.getNumMethodsNotBinded();

            catchBlockInfo.MetaInfo["DistinctExceptions"] = tryPossibleExceptionsCustomVisitor.PrintDistinctPossibleExceptions();
            catchBlockInfo.OperationFeatures["NumDistinctExceptions"] = tryPossibleExceptionsCustomVisitor.getDistinctPossibleExceptions().Count;

            catchBlockInfo.OperationFeatures["NumSpecificHandler"] = tryPossibleExceptionsCustomVisitor.getNumSpecificHandler();
            catchBlockInfo.OperationFeatures["NumSubsumptionHandler"] = tryPossibleExceptionsCustomVisitor.getNumSubsumptionHandler();
            catchBlockInfo.OperationFeatures["NumSupersumptionHandler"] = tryPossibleExceptionsCustomVisitor.getNumSupersumptionHandler();
            catchBlockInfo.OperationFeatures["NumOtherHandler"] = tryPossibleExceptionsCustomVisitor.getNumOtherHandler();

            catchBlockInfo.OperationFeatures["MaxLevel"] = tryPossibleExceptionsCustomVisitor.getChildrenMaxLevel();
            catchBlockInfo.OperationFeatures["NumIsDocSemantic"] = tryPossibleExceptionsCustomVisitor.getNumIsDocSemantic();
            catchBlockInfo.OperationFeatures["NumIsDocSyntax"] = tryPossibleExceptionsCustomVisitor.getNumIsDocSyntax();
            catchBlockInfo.OperationFeatures["NumIsThrow"] = tryPossibleExceptionsCustomVisitor.getNumIsThrow();

            //FinallyThrowing
            FinallyClauseSyntax finallyBlock = tryBlock.Finally;
            if (finallyBlock != null)
            {
                catchBlockInfo.MetaInfo["FinallyBlock"] = finallyBlock.ToString();

                var finallyPossibleExceptionsCustomVisitor = new PossibleExceptionsCustomVisitor(Compilation, TreeAndModelDic, 0, true, tree.FilePath, catchStartLine, exceptionNamedTypeSymbol);
                finallyPossibleExceptionsCustomVisitor.Visit(finallyBlock.Block);

                if (finallyBlock.DescendantNodes().OfType<ThrowStatementSyntax>().Any()
                        || finallyPossibleExceptionsCustomVisitor.getDistinctPossibleExceptions().Count > 0)
                    catchBlockInfo.OperationFeatures["FinallyThrowing"] = 1;
            }

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

            Catches.Add(catchBlockInfo);
        }

        private static void getExceptionFlows(List<PossibleExceptionsBlock> possibleExceptionsList, HashSet<ClosedExceptionFlow> closedExceptionFlows, Compilation compilation)
        {
            foreach (ClosedExceptionFlow flow in closedExceptionFlows)
            {
                PossibleExceptionsBlock possibleExceptionsBlockInfo = new PossibleExceptionsBlock();

                possibleExceptionsBlockInfo.ExceptionType = flow.getThrownTypeName();
                possibleExceptionsBlockInfo.CaughtType = flow.getCaughtTypeName();
                possibleExceptionsBlockInfo.DeclaringMethod = flow.getOriginalMethodBindingKey();
                possibleExceptionsBlockInfo.InvokedMethod = flow.getInvokedMethodKey();
                possibleExceptionsBlockInfo.InvokedMethodLine = flow.getInvokedMethodLine();
                possibleExceptionsBlockInfo.FilePath = flow.getCatchFilePath();
                possibleExceptionsBlockInfo.StartLine = flow.getCatchStartLine();

                possibleExceptionsBlockInfo.MetaInfo["FilePath"] = flow.getCatchFilePath();
                possibleExceptionsBlockInfo.MetaInfo["StartLine"] = flow.getCatchStartLine().ToString();

                int kind;
                if (flow.getThrownType() != null )
                    kind = ASTUtilities.FindKind(flow.getThrownType(), compilation);
                else
                    kind = ASTUtilities.FindKind(flow.getThrownTypeName(), compilation);
                possibleExceptionsBlockInfo.OperationFeatures["Kind"] = kind;

                possibleExceptionsBlockInfo.OperationFeatures["IsDocSemantic"] = flow.getIsDocSemantic() ? 1 : 0;
                possibleExceptionsBlockInfo.OperationFeatures["IsDocSyntax"] = flow.getIsDocSyntax() ? 1 : 0;
                possibleExceptionsBlockInfo.OperationFeatures["IsThrow"] = flow.getIsThrow() ? 1 : 0;
                possibleExceptionsBlockInfo.OperationFeatures["LevelFound"] = (int)flow.getLevelFound();

                possibleExceptionsBlockInfo.OperationFeatures["HandlerTypeCode"] = (int)flow.getHandlerTypeCode();


                //possibleExceptionsBlockInfo.MetaInfo.put("PossibleExceptionsBlock", node.toString());

                possibleExceptionsList.Add(possibleExceptionsBlockInfo);

                Logger.Log("Possible Exceptions block info registered.");
            }
        }

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
                        .OfType<InvocationExpressionSyntax>().First(statement => !IsAbortStatement(statement)
                                                                                   && !IsLoggingStatement(statement)
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

        private static bool IsInnerCatch(SyntaxNode node)
        {
            if (node.IsKind(SyntaxKind.MethodDeclaration) || node.IsKind(SyntaxKind.ConstructorDeclaration) || node.IsKind(SyntaxKind.ClassDeclaration))
                return false;

            if (node.IsKind(SyntaxKind.CatchClause))
                return true;

            return IsInnerCatch(node.Parent);

        }

        public static SyntaxNode FindParentCatch(SyntaxNode node)
        {
            //if reach method, constructor and class stop because went too far
            if (node.IsKind(SyntaxKind.MethodDeclaration) || node.IsKind(SyntaxKind.ConstructorDeclaration) || node.IsKind(SyntaxKind.ClassDeclaration))
                return null;

            if (node.IsKind(SyntaxKind.CatchClause))
                return node;

            return FindParentCatch(node.Parent);

        }

        #endregion CatchClause and binded info analysis
    }
}
