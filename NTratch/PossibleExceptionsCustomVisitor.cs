using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace NTratch
{
    /// <summary>
    /// This visitor is focused to be called by a declaration, either try block or a method (constructor or not).
    /// It will evaluate the existing nodes on the try that are of type: InvocationExpressionSyntax ObjectCreationExpressionSyntax
    /// </summary>
    class PossibleExceptionsCustomVisitor : CSharpSyntaxWalker
    {
        private INamedTypeSymbol m_catchType;
        private string m_catchFilePath;
        private int m_catchStartLine;

        private Dictionary<SyntaxTree, SemanticModel> m_treeAndModelDic;
        private Compilation m_compilation;

        private Dictionary<string, sbyte> m_invokedMethodsBinded = new Dictionary<string, sbyte>();

        //private Dictionary<string, Dictionary<string, sbyte>> invokedMethodsHandlerType = new Dictionary<string, Dictionary<string, sbyte>>();
        private Dictionary<string, HashSet<ExceptionFlow>> m_invokedMethodsPossibleExceptions = new Dictionary<string, HashSet<ExceptionFlow>>();

        private HashSet<ExceptionFlow> m_possibleExceptions = new HashSet<ExceptionFlow>();
        private HashSet<ClosedExceptionFlow> closedExceptionFlows = new HashSet<ClosedExceptionFlow>();

        private int m_nodeMaxLevel = 0;

        private int m_myLevel = 0;
        private Dictionary<string, int> m_ChildrenNodesLevel = new Dictionary<string, int>();

        public bool m_isForAnalysis { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="p_isForAnalysis">This is know if the found exceptions should be evaluated against the parent try-catch block, if any.</param>
        public PossibleExceptionsCustomVisitor(Compilation p_compilation, Dictionary<SyntaxTree, SemanticModel> p_treeAndModelDic,
                                                int p_level, bool p_isForAnalysis,
                                                string catchFilePath, int catchStartLine, INamedTypeSymbol p_exceptionType)
        {
            m_compilation = p_compilation;
            m_treeAndModelDic = p_treeAndModelDic;
            m_myLevel = p_level;
            m_isForAnalysis = p_isForAnalysis;

            m_catchFilePath = catchFilePath;
            m_catchStartLine = catchStartLine;
            m_catchType = p_exceptionType;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="p_isForAnalysis">This is know if the found exceptions should be evaluated against the parent try-catch block, if any.</param>
        public PossibleExceptionsCustomVisitor(Compilation p_compilation, Dictionary<SyntaxTree, SemanticModel> p_treeAndModelDic,
                                                int p_level, bool p_isForAnalysis,
                                                string declaringNodeKey)
        {
            m_compilation = p_compilation;
            m_treeAndModelDic = p_treeAndModelDic;
            m_myLevel = p_level;
            m_isForAnalysis = p_isForAnalysis;

            //m_declaringNodeKey = declaringNodeKey;
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            processExpressionNodeAndVisit(node);
            base.VisitInvocationExpression(node);
        }

        public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            processExpressionNodeAndVisit(node);
            base.VisitObjectCreationExpression(node);
        }

        public override void VisitThrowStatement(ThrowStatementSyntax node)
        {
            //Common Features - parent method
            string parentMethodName;
            SyntaxNode parentMethod;
            INamedTypeSymbol exceptionType = null;
            ExceptionFlow flow;
            HashSet<ExceptionFlow> possibleException;
            HashSet<ExceptionFlow> validNodePossibleExceptions = new HashSet<ExceptionFlow>();

            parentMethod = ASTUtilities.FindParentMethod(node);
            parentMethodName = ASTUtilities.GetMethodName(parentMethod, m_treeAndModelDic, m_compilation);

            if (node.Expression != null)
            {
                var symbolThrow = ASTUtilities.GetNodeSymbol(node.Expression, m_treeAndModelDic, m_compilation);

                //var symbol = semanticModel.GetSymbolInfo(recoverStatement).Symbol as LocalSymbol;
                //String typeName = symbol.Type.ToString();

                //m_compilation.GetTypeByMetadataName

                if (symbolThrow != null)
                {
                    exceptionType = symbolThrow.ContainingType;
                }
            }
                
            if (exceptionType != null)
                flow = new ExceptionFlow(exceptionType, ExceptionFlow.THROW, parentMethodName, m_myLevel);
            else
            {
                string exceptionName;

                if (node.Expression != null)
                {
                    exceptionName = node.Expression.ToString();
                    Logger.Log("Warn - Expression was found, but not understood: " + node.Expression.ToString());
                }                    
                else
                    exceptionName = "!NO_EXCEPTION_DECLARED!";
                flow = new ExceptionFlow(exceptionName, ExceptionFlow.THROW, parentMethodName, m_myLevel);
            }
            possibleException = new HashSet<ExceptionFlow>();
            possibleException.Add(flow);

            //save only throws that escapes the containing method.
            validNodePossibleExceptions = GetValidPossibleExceptions(node, possibleException);
            ClosePossibleExceptionFlows(validNodePossibleExceptions, "", 0);
            m_possibleExceptions.UnionWith(validNodePossibleExceptions);
        }
                
        /// <summary>
        /// Validate exceptions and return only valid exceptions.
        /// A valid exception means exceptions that can escape its containing method.
        /// Here we try to close the exception, if it closes the exception doesn't escape the method.
        /// </summary>
        /// <param name="node">the current node that throws the possible exceptions</param>
        /// <param name="possibleException"> the possible exceptions being thrown</param>
        private HashSet<ExceptionFlow> GetValidPossibleExceptions(SyntaxNode node, HashSet<ExceptionFlow> possibleExceptions)
        {
            if (m_isForAnalysis)
                return possibleExceptions;
            else
            {
                TryStatementSyntax parentTry = (TryStatementSyntax) ASTUtilities.FindParentTry(node);

                if (parentTry == null)
                    return possibleExceptions;
                else
                {
                    HashSet<ExceptionFlow> validPossibleExceptions = new HashSet<ExceptionFlow>();
                    
                    foreach (CatchClauseSyntax catchBlock in parentTry.Catches)
                    {
                        INamedTypeSymbol thrownExceptionType;
                        INamedTypeSymbol caughtExceptionType;

                        var model = m_treeAndModelDic[catchBlock.SyntaxTree];
                        if (catchBlock.Declaration != null)
                        {
                            caughtExceptionType = model.GetTypeInfo(catchBlock.Declaration.Type).ConvertedType as INamedTypeSymbol;

                            foreach (ExceptionFlow exception in possibleExceptions)
                            {
                                thrownExceptionType = exception.getThrownType();

                                if (!ClosedExceptionFlow.IsCloseableExceptionFlow(caughtExceptionType, thrownExceptionType))
                                    validPossibleExceptions.Add(exception);
                            }
                        }
                            
                    }
                    return validPossibleExceptions;
                }
            }
        }

        private void processExpressionNodeAndVisit(SyntaxNode node)
        {
            int startLine;
            ISymbol nodeBindingInfo;
            bool binded;
            string nodeString;
            //nodePossibleExceptions store all possible exceptions from the node being visited
            //these will be validated (and added to validNodePossibleExceptions) before being added to the invoked methods list
            HashSet<ExceptionFlow> nodePossibleExceptions = new HashSet<ExceptionFlow>();
            HashSet<ExceptionFlow> validNodePossibleExceptions = new HashSet<ExceptionFlow>();
            Dictionary<string, HashSet<ExceptionFlow>> nodeAndNodePossibleExceptions = new Dictionary<string, HashSet<ExceptionFlow>>();

            var nodeStartLinePositionSpan = node.SyntaxTree.GetLineSpan(node.Span);
            startLine = nodeStartLinePositionSpan.StartLinePosition.Line + 1;            
            nodeBindingInfo = ASTUtilities.GetNodeSymbol(node, m_treeAndModelDic, m_compilation);
            binded = (nodeBindingInfo != null) ? true : false;
            nodeString = binded ? nodeBindingInfo.ToString() : node.ToString();

            initializeLocalVisitInfo(nodeString, binded);

            nodePossibleExceptions.UnionWith(GetCachedPossibleExceptions(nodeString, binded, nodeBindingInfo));

            m_ChildrenNodesLevel[nodeString] = m_ChildrenNodesLevel[nodeString] + CodeAnalyzer.InvokedMethods[nodeString].getChildrenMaxLevel();
            
            //TODO combine exceptions that are actually from a same origin

            validNodePossibleExceptions = GetValidPossibleExceptions(node, nodePossibleExceptions);
            ClosePossibleExceptionFlows(validNodePossibleExceptions, nodeString, startLine);
            m_possibleExceptions.UnionWith(validNodePossibleExceptions);
            nodeAndNodePossibleExceptions.Add(nodeString, validNodePossibleExceptions);
            CodeAnalyzer.MergeDic(m_invokedMethodsPossibleExceptions, nodeAndNodePossibleExceptions);

            #region previous_version
            /*
             * 
             * 
            var nodeString = "";
            var nodePossibleExceptions = new Dictionary<string, Dictionary<string, sbyte>>();
            BaseMethodDeclarationSyntax nodemDeclar = null;
            
            //Try to get symbol from semantic model
            var nodeSymbol = GetNodeSymbol(node);

            //NodeSymbol is not empty it means it's binded and we have semantic info - define nodeString
            if (nodeSymbol != null)
            {
                nodeString = nodeSymbol.ToString();
                m_invokedMethodsBinded[nodeString] = 1;

                if (!m_ChildrenNodesLevel.ContainsKey(nodeString))
                {
                    m_ChildrenNodesLevel.Add(nodeString, 1);
                    processNodeForXMLSemantic(node, ref nodeSymbol, ref nodePossibleExceptions);
                }
            }
            else
            {
                nodeString = node.ToString();
                m_invokedMethodsBinded[nodeString] = 0;
                
                if (!m_ChildrenNodesLevel.ContainsKey(nodeString))
                {
                    m_ChildrenNodesLevel.Add(nodeString, 1);                    
                }
            }

            nodemDeclar = GetNodeDeclaration(nodeString);
            
            //var nodeString = processExpressionNode(node, ref nodePossibleExceptions, ref nodemDeclar);
            
            //Go get exceptions based on visiting declarations - recursive way - if not yet visited
            if (nodemDeclar != null && !CodeAnalyzer.AllMyMethods[nodeString].IsVisited)
            {
                CodeAnalyzer.AllMyMethods[nodeString].IsVisited = true;

                var possibleExceptionsCustomVisitor = new PossibleExceptionsCustomVisitor(ref m_exceptionType, ref m_treeAndModelDic, ref m_compilation, false, m_myLevel + 1);
                possibleExceptionsCustomVisitor.Visit(nodemDeclar);

                CodeAnalyzer.MergeDic(ref m_invokedMethodsBinded, possibleExceptionsCustomVisitor.m_invokedMethodsBinded);

                var nodeDeclarExceptions = new Dictionary<string, Dictionary<string, sbyte>>();
                MergePossibleExceptionsDic(ref nodeDeclarExceptions, GetExceptionsFromXMLSyntax(ref nodemDeclar));
                MergePossibleExceptionsDic(ref nodeDeclarExceptions, possibleExceptionsCustomVisitor.m_possibleExceptions);

                CodeAnalyzer.AllMyMethods[nodeString].Exceptions = nodeDeclarExceptions;
                CodeAnalyzer.AllMyMethods[nodeString].ChildrenMaxLevel = possibleExceptionsCustomVisitor.getChildrenMaxLevel();
            }
            //check my methods to get the previously stored exceptions
            if (CodeAnalyzer.AllMyMethods.ContainsKey(nodeString))
            {
                MergePossibleExceptionsDic(ref nodePossibleExceptions, CodeAnalyzer.AllMyMethods[nodeString].Exceptions);
                m_ChildrenNodesLevel[nodeString] = m_ChildrenNodesLevel[nodeString] + CodeAnalyzer.AllMyMethods[nodeString].ChildrenMaxLevel;
            }
            //If this is not in the level to expose the exceptions for analysis, validate if they are really coming out or not
            var nodeAndNodePossibleExceptions = new Dictionary<string, Dictionary<string, Dictionary<string, sbyte>>>();
            var validPossibleExceptions = new Dictionary<string, Dictionary<string, sbyte>>();

            if (!m_isForAnalysis)
                validPossibleExceptions = getValidPossibleExceptions(node, nodePossibleExceptions);
            else
                validPossibleExceptions = nodePossibleExceptions;

            MergePossibleExceptionsDic(ref m_possibleExceptions, validPossibleExceptions);
            nodeAndNodePossibleExceptions.Add(nodeString, validPossibleExceptions);
            CodeAnalyzer.MergeDic(ref m_invokedMethodsPossibleExceptions, nodeAndNodePossibleExceptions);
             * 
             * */
            #endregion previous_version

        }

        private IEnumerable<ExceptionFlow> GetCachedPossibleExceptions(string nodeString, bool binded, ISymbol nodeBindingInfo)
        {
            //Go grab data if method not yet known
            if (!CodeAnalyzer.InvokedMethods.ContainsKey(nodeString))
            {
                if (binded)
                {
                    CodeAnalyzer.InvokedMethods.Add(nodeString, new InvokedMethod(nodeString, true));
                    collectBindedInvokedMethodDataFromDeclaration(CodeAnalyzer.InvokedMethods[nodeString], nodeString);
                    collectBindedInvokedMethodDataFromSemanticModel(CodeAnalyzer.InvokedMethods[nodeString], nodeBindingInfo, nodeString);
                }
                else
                    CodeAnalyzer.InvokedMethods.Add(nodeString, new InvokedMethod(nodeString, false));
            }
            //TODO: if already known, what to do?        
            return CodeAnalyzer.InvokedMethods[nodeString].getExceptionFlowSetByType();
        }

        private void collectBindedInvokedMethodDataFromDeclaration(InvokedMethod invokedMethod, string nodeString)
        {
            {
                BaseMethodDeclarationSyntax nodemDeclar;
                
                nodemDeclar = GetNodeDeclaration(nodeString);

                if (nodemDeclar != null)
                {
                    invokedMethod.setVisited(true);
                    PossibleExceptionsCustomVisitor possibleExceptionsCustomVisitor = new PossibleExceptionsCustomVisitor(m_compilation, m_treeAndModelDic, (byte)(m_myLevel + 1), false, nodeString);
                    possibleExceptionsCustomVisitor.Visit(nodemDeclar);

                    CodeAnalyzer.MergeDic(m_invokedMethodsBinded, possibleExceptionsCustomVisitor.m_invokedMethodsBinded);

                    invokedMethod.getExceptionFlowSet().UnionWith(possibleExceptionsCustomVisitor.m_possibleExceptions);
                    invokedMethod.setChildrenMaxLevel(possibleExceptionsCustomVisitor.getChildrenMaxLevel());
                    invokedMethod.getExceptionFlowSet().UnionWith(GetExceptionsFromXMLSyntax(nodemDeclar, nodeString));
                }
            }
        }

        public Dictionary<string, HashSet<ExceptionFlow>> getInvokedMethodsPossibleExceptions()
        {
            return m_invokedMethodsPossibleExceptions;
        }

        private void collectBindedInvokedMethodDataFromSemanticModel(InvokedMethod invokedMethod, ISymbol nodeBindingInfo, string nodeString)
        {
            invokedMethod.getExceptionFlowSet().UnionWith(GetExceptionsFromXMLSemantic(nodeBindingInfo, nodeString));
        }

        private void initializeLocalVisitInfo(string nodeString, bool binded)
        {
            ///update this declaration (scope being visited) metrics
            m_invokedMethodsBinded[nodeString] = (sbyte)(binded ? 1 : 0);
            if (!m_ChildrenNodesLevel.ContainsKey(nodeString))
                m_ChildrenNodesLevel.Add(nodeString, 1);
        }

        public BaseMethodDeclarationSyntax GetNodeDeclaration(string p_nodeString)
        {
            //Get the declaration for this node (Syntax) - BEGIN
            if (CodeAnalyzer.AllMyMethods.ContainsKey(p_nodeString))
            {
                return CodeAnalyzer.AllMyMethods[p_nodeString].Declaration;
            }
            else
                return null;
            
        }
        
        private HashSet<ExceptionFlow> GetExceptionsFromXMLSemantic(ISymbol p_nodeSymbol, string originalNode)
        {
            // // Exceptions from the XML found on the Semantic model - IsXMLSemantic
            var xmlTextSemantic = p_nodeSymbol.GetDocumentationCommentXml();
            if (xmlTextSemantic == null || xmlTextSemantic == "")
            {   // // recover by using the overall semantic model
                //xmlTextSemantic = m_compilation.GetSemanticModel(p_node.SyntaxTree).GetSymbolInfo(p_node).Symbol?.GetDocumentationCommentXml();
                Logger.Log("WARN - empty xml: " + originalNode);
            }
            return NodeFindExceptionsInXML(xmlTextSemantic, ExceptionFlow.DOC_SEMANTIC, originalNode);            
        }

        private HashSet<ExceptionFlow> GetExceptionsFromXMLSyntax(BaseMethodDeclarationSyntax p_nodemDeclar, string originalNode)
        {
            // // Exceptions from the XML found on the Syntax model - IsXMLSyntax
            SyntaxTrivia xmlTextSyntax = p_nodemDeclar
                                    .DescendantTrivia()
                                    .ToList()
                                    .FirstOrDefault(trivia => trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia));

            return NodeFindExceptionsInXML(xmlTextSyntax.ToString(), ExceptionFlow.DOC_SYNTAX, originalNode);
        }

        public HashSet<ExceptionFlow> NodeFindExceptionsInXML(string xml, string p_originKey, string originalNode)
        {
            HashSet<ExceptionFlow> exceptions = new HashSet<ExceptionFlow>();

            if (xml != null && xml != "")
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.LoadXml("<comment_root>" + RemoveInvalidXmlChars(xml) + "</comment_root>");

                XmlNodeList nodeList;
                XmlNode root = xmlDoc.DocumentElement;

                nodeList = root.SelectNodes("descendant::exception");

                foreach (XmlNode exception in nodeList)
                {
                    var exceptionTypeName = exception.Attributes.GetNamedItem("cref")?.InnerText.Replace("T:", "");

                    if(exceptionTypeName == null)
                        exceptionTypeName = exception.Attributes.GetNamedItem("type")?.InnerText.Replace("T:", "");

                    if (exceptionTypeName == null)
                        exceptionTypeName = "!XML_EXCEPTION_NOT_IDENTIFIED!";

                    var exceptionType = m_compilation.GetTypeByMetadataName(exceptionTypeName);

                    if (exceptionType != null)
                    {
                        ExceptionFlow flow = new ExceptionFlow(exceptionType, p_originKey, originalNode, m_myLevel);
                        exceptions.Add(flow);
                    }               
                }
            }
            return exceptions;
        }

        static string RemoveInvalidXmlChars(string text)
        {
            var validXmlChars = text.Where(ch => XmlConvert.IsXmlChar(ch)).ToArray();

            var validXmlString = new string(validXmlChars);

            return validXmlString.Replace("&", "");
        }

        //public void AddPossibleExceptions(string p_exceptionTypeName, ref Dictionary<string, Dictionary<string, sbyte>> p_PossibleExceptions, string p_originKey)
        //{
        //    if (!p_PossibleExceptions.ContainsKey(p_exceptionTypeName))
        //        p_PossibleExceptions.Add(p_exceptionTypeName, BaseDicWithHandlerType(p_exceptionTypeName));

        //    p_PossibleExceptions[p_exceptionTypeName][p_originKey] = 1;

        //    int previousDeepestLevel = Convert.ToInt16(p_PossibleExceptions[p_exceptionTypeName]["DeepestLevelFound"]);
        //    int currentDeepestLevel = Convert.ToInt16(m_myLevel);

        //    if (currentDeepestLevel > previousDeepestLevel)
        //        p_PossibleExceptions[p_exceptionTypeName]["DeepestLevelFound"] = Convert.ToSByte(currentDeepestLevel);

        //}

        //private Dictionary<string, sbyte> BaseDicWithHandlerType (string exceptionTypeName)
        //{
        //    //Default value definition for when coming from XML Semantics
        //    var dicXmlFromSemantic = new Dictionary<string, sbyte>();
        //    dicXmlFromSemantic.Add("HandlerTypeCode", -9);
        //    dicXmlFromSemantic.Add("IsXMLSemantic", 0);
        //    dicXmlFromSemantic.Add("IsXMLSyntax", 0);
        //    dicXmlFromSemantic.Add("IsThrow", 0);
        //    dicXmlFromSemantic.Add("DeepestLevelFound", 0);

        //    dicXmlFromSemantic["HandlerTypeCode"] = GetHandlerTypeCode(m_exceptionType, exceptionTypeName);

        //    return dicXmlFromSemantic;
        //}

        //public sbyte GetHandlerTypeCode(INamedTypeSymbol catchedType, string possibleThrowType)
        //{
        //    sbyte handlerTypeCode = -9;
        //    if (catchedType != null)
        //    {
        //        INamedTypeSymbol type = m_compilation.GetTypeByMetadataName(possibleThrowType);
        //        if (type != null)
        //        {
        //            //In case is the same type, it's specific handler type - code: 0
        //            //In case the catched type is equal a super class of the possible thrown type, it's a subsumption - code: 1
        //            //In case the possible thrown type is equal a super class of the catched type, it's a supersumption - code: 2
        //            //In case it's none of the above - most likely tree of unrelated exceptions: code: 3
        //            if (catchedType.Equals(type))
        //            {
        //                handlerTypeCode = 0;
        //            }
        //            else if (IsSuperType(catchedType, type))
        //            {
        //                handlerTypeCode = 1;
        //            }
        //            else if (IsSuperType(type, catchedType))
        //            {
        //                handlerTypeCode = 2;
        //            }
        //            else
        //            {
        //                //it can happen when exceptions are not related on the type tree
        //                handlerTypeCode = 3;
        //            }
        //        }
        //        else
        //            handlerTypeCode = -8;
        //    }
        //    return handlerTypeCode;
        //}

        public void MergePossibleExceptionsDic(ref Dictionary<string, Dictionary<string, sbyte>> dicReceiver, Dictionary<string, Dictionary<string, sbyte>> dicToAppend)
        {
            foreach (var exceptionValues in dicToAppend)
            {
                if (exceptionValues.Key == null) continue;
                if (!dicReceiver.ContainsKey(exceptionValues.Key))
                {
                    dicReceiver.Add(exceptionValues.Key, exceptionValues.Value);
                }
                else
                {
                    foreach (var value in exceptionValues.Value.ToList())
                    {
                        int previousValue = Convert.ToInt16(dicReceiver[exceptionValues.Key][value.Key]);
                        int currentValue = Convert.ToInt16(value.Value);
                        int result = -99;

                        if (value.Key != "HandlerTypeCode" && value.Key != "DeepestLevelFound")
                        {
                            result = previousValue + currentValue;

                            if (result > 1)
                                result = 1;
                        }
                        if (value.Key == "DeepestLevelFound")
                        {
                            if (currentValue > previousValue)
                                result = currentValue;
                        }
                        if (result != -99)
                            dicReceiver[exceptionValues.Key][value.Key] = Convert.ToSByte(result);
                    }
                }
            }
        }
        private void ClosePossibleExceptionFlows(HashSet<ExceptionFlow> exceptions, string invokedMethodKey, int invokedMethodLine)
        {
            //A visit FOR analysis will let caught exceptions escape the validation because those are the possible exception we want to see for each catch block.
            //A visit NOT for analysis will go through validation that won't allow exceptions that are caught in a inner scope pass to the outside.
            //When FOR analysis we also need to close the flows based on the given catch block.
            if (m_isForAnalysis)
            {
                foreach (ExceptionFlow exception in exceptions)
                {
                    ClosedExceptionFlow closedExceptionFlow = new ClosedExceptionFlow(exception);
                    closedExceptionFlow.closeExceptionFlow(m_catchType, exception.getThrownType(),
                            m_catchFilePath, m_catchStartLine, invokedMethodKey, invokedMethodLine);
                    closedExceptionFlows.Add(closedExceptionFlow);
                }
            }
        }
        
        public string PrintInvokedMethodsPossibleExceptions()
        {
            return PrintDictionary(m_invokedMethodsPossibleExceptions);
        }

        public string PrintInvokedMethodsBinded()
        {
            return PrintDictionary(m_invokedMethodsBinded);            
        }

        public string PrintDictionary(Dictionary<string, HashSet<ExceptionFlow>> entrySet)
        {
            var collectionStart = "{";
            var keySep = "=";
            var collectionEnd = "}";
            var pairSep = ", ";

            StringBuilder sb = new StringBuilder();
            sb.Append(collectionStart);
            if (entrySet != null)
            {
                foreach (var entry in entrySet)
                {
                    sb.Append(entry.Key);
                    sb.Append(keySep);
                    sb.Append(PrintDictionary(entry.Value));
                    sb.Append(pairSep);
                }
            }
            sb.Append(collectionEnd);
            sb.Replace(pairSep + collectionEnd, collectionEnd);

            return sb.ToString();
        }

        public string PrintDictionary(HashSet<ExceptionFlow> entrySet)
        {
            var collectionStart = "{";
            var keySep = "=";
            var collectionEnd = "}";
            var pairSep = ", ";

            StringBuilder sb = new StringBuilder();
            sb.Append(collectionStart);
            if (entrySet != null)
            {
                foreach (var entry in entrySet)
                {
                    sb.Append(entry.ToString());
                    //sb.Append(keySep);
                    //sb.Append(PrintDictionary(entry.Value));
                    sb.Append(pairSep);
                }
            }
            sb.Append(collectionEnd);
            sb.Replace(pairSep + collectionEnd, collectionEnd);

            return sb.ToString();
        }

        public string PrintDictionary(Dictionary<string, sbyte> entrySet)
        {
            var collectionStart = "{";
            var keySep = "=";
            var collectionEnd = "}";
            var pairSep = ", ";

            StringBuilder sb = new StringBuilder();
            sb.Append(collectionStart);
            if (entrySet != null)
            {
                foreach (var entry in entrySet)
                {
                    sb.Append(entry.Key);
                    sb.Append(keySep);
                    sb.Append(entry.Value);
                    sb.Append(pairSep);
                }
            }
            sb.Append(collectionEnd);
            sb.Replace(pairSep + collectionEnd, collectionEnd);

            return sb.ToString();
        }

        public int countInvokedMethodsHandlerType()
        {
            return m_invokedMethodsPossibleExceptions.Count;
        }
        
        public HashSet<ExceptionFlow> getDistinctPossibleExceptions()
        {
            return m_possibleExceptions;
        }

        public HashSet<ClosedExceptionFlow> getClosedExceptionFlows()
        {
            return closedExceptionFlows;
        }

        public string PrintDistinctPossibleExceptions()
        {
            return PrintDictionary(m_possibleExceptions);
        }

        //public int CountMetricsForExceptions(string strKey, sbyte sbyteCode)
        //{
        //    return m_possibleExceptions.Sum(exception => exception.Value.Count(key => (key.Key == strKey && key.Value == sbyteCode)));
        //}
        public int getNumSpecificHandler()
        {
            return getClosedExceptionFlows().Count(flow => flow.getHandlerTypeCode() == 0);
        }
                
        public int getNumSubsumptionHandler()
        {
            return getClosedExceptionFlows().Count(flow => flow.getHandlerTypeCode() == 1);
        }
        public int getNumSupersumptionHandler()
        {
            return getClosedExceptionFlows().Count(flow => flow.getHandlerTypeCode() == 2);
        }
        public int getNumOtherHandler()
        {
            return getClosedExceptionFlows().Count(flow => flow.getHandlerTypeCode() == 3);
        }
        public int getNumMethodsNotBinded()
        {
            return m_invokedMethodsBinded.Count(entry => entry.Value == 0);
        }
        public int getNumIsDocSemantic()
        {
            return getClosedExceptionFlows().Count(flow => flow.getIsDocSemantic());
        }
        public int getNumIsDocSyntax()
        {
            return getClosedExceptionFlows().Count(flow => flow.getIsDocSyntax());
        }
        public int getNumIsThrow()
        {
            return getClosedExceptionFlows().Count(flow => flow.getIsThrow());
        }


        internal int getChildrenMaxLevel()
        {
            return (m_ChildrenNodesLevel.Values.Count > 0) ? m_ChildrenNodesLevel.Values.Max() : 0;
        }



        #region Possible exceptions

        /*
         public void processIsLoop(ref BaseMethodDeclarationSyntax p_nodemDeclar, ref Dictionary<string, Dictionary<string, sbyte>> p_nodePossibleExceptions)
        {
            Dictionary<string, int> allInovkedMethods = new Dictionary<string, int>();
            //Dictionary<string, int> allInovkedExcetions = new Dictionary<string, int>();

            // to save a code snippet and its backward level
            Queue<Tuple<SyntaxNode, int>> codeSnippetQueue = new Queue<Tuple<SyntaxNode, int>>();

            //Queue the current method declaration if existent
            if (p_nodemDeclar != null)
                codeSnippetQueue.Enqueue(new Tuple<SyntaxNode, int>(p_nodemDeclar, 0));

            while (codeSnippetQueue.Any())
            {
                Tuple<SyntaxNode, int> snippetAndLevel = codeSnippetQueue.Dequeue();
                var level = snippetAndLevel.Item2;
                if (level > m_nodeMaxLevel)
                    m_nodeMaxLevel = level;

                var snippet = snippetAndLevel.Item1;
                var treeQueue = snippet.SyntaxTree;

                //Get the invocations, object creation and throws inside the declaration
                List<InvocationExpressionSyntax> methodList = GetInvokedMethodsInACodeSnippet(snippet);
                List<ObjectCreationExpressionSyntax> objectCreationList = GetObjectCreationInACodeSnippet(snippet);
                List<ThrowStatementSyntax> throwList = GetInvokedThrowsInACodeSnippet(snippet);

                //Based on the invocations found, queue its declarations
                foreach (var invocation in methodList)
                {
                    string methodName = IOFile.MethodNameExtraction(invocation.ToString());
                    try
                    {
                        // use a single semantic model
                        var modelQueue = m_treeAndModelDic[treeQueue];
                        var symbolInfoQueue = modelQueue.GetSymbolInfo(invocation);
                        var symbolQueue = symbolInfoQueue.Symbol;

                        if (symbolQueue == null)
                        {   // recover by using the overall semantic model
                            modelQueue = m_compilation.GetSemanticModel(treeQueue);
                            symbolInfoQueue = modelQueue.GetSymbolInfo(invocation);
                            symbolQueue = symbolInfoQueue.Symbol;
                        }
                        if (symbolQueue != null)
                        {
                            methodName = IOFile.MethodNameExtraction(symbolQueue.ToString());
                        }
                        if (allInovkedMethods.ContainsKey(methodName))
                        {
                            allInovkedMethods[methodName]++;
                        }
                        else
                        {
                            allInovkedMethods.Add(methodName, 1);
                            //if (level > 3) continue; // only go backward to 3 levels
                            if (methodName.StartsWith("System")) continue; // System API

                            if (symbolQueue != null && CodeAnalyzer.AllMyMethods.ContainsKey(symbolQueue.ToString()))
                            {
                                // find the method declaration (go to definition)
                                var mdeclar = CodeAnalyzer.AllMyMethods[symbolQueue.ToString()].Declaration;
                                codeSnippetQueue.Enqueue(new Tuple<SyntaxNode, int>(mdeclar, level + 1));
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        CodeAnalyzer.MergeDic(ref allInovkedMethods,
                                new Dictionary<string, int>() { { methodName, 1 } });
                        Logger.Log(treeQueue.FilePath);
                        Logger.Log(snippet.ToFullString());
                        Logger.Log(invocation.ToFullString());
                        Logger.Log(e);
                        Logger.Log(e.StackTrace);
                    }
                }

                //Based on the object creations found, queue its declarations
                foreach (var objectCreation in objectCreationList)
                {
                    string methodName = IOFile.MethodNameExtraction(objectCreation.ToString());
                    try
                    {
                        // use a single semantic model
                        var modelQueue = m_treeAndModelDic[treeQueue];
                        var symbolInfoQueue = modelQueue.GetSymbolInfo(objectCreation);
                        var symbolQueue = symbolInfoQueue.Symbol;

                        if (symbolQueue == null)
                        {   // recover by using the overall semantic model
                            modelQueue = m_compilation.GetSemanticModel(treeQueue);
                            symbolInfoQueue = modelQueue.GetSymbolInfo(objectCreation);
                            symbolQueue = symbolInfoQueue.Symbol;
                        }
                        if (symbolQueue != null)
                        {
                            methodName = IOFile.MethodNameExtraction(symbolQueue.ToString());
                        }
                        if (allInovkedMethods.ContainsKey(methodName))
                        {
                            allInovkedMethods[methodName]++;
                        }
                        else
                        {
                            allInovkedMethods.Add(methodName, 1);
                            //if (level > 3) continue; // only go backward to 3 levels
                            if (methodName.StartsWith("System")) continue; // System API

                            if (symbolQueue != null && CodeAnalyzer.AllMyMethods.ContainsKey(symbolQueue.ToString()))
                            {
                                // find the method declaration (go to definition)
                                var mdeclar = CodeAnalyzer.AllMyMethods[symbolQueue.ToString()].Declaration;
                                codeSnippetQueue.Enqueue(new Tuple<SyntaxNode, int>(mdeclar, level + 1));
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        CodeAnalyzer.MergeDic(ref allInovkedMethods,
                                new Dictionary<string, int>() { { methodName, 1 } });
                        Logger.Log(treeQueue.FilePath);
                        Logger.Log(snippet.ToFullString());
                        Logger.Log(objectCreation.ToFullString());
                        Logger.Log(e);
                        Logger.Log(e.StackTrace);
                    }
                }

                //Based on the throws found, do the accounting
                foreach (var throwStatement in throwList)
                {
                    string exceptionName = "";

                    var modelQueue = m_treeAndModelDic[treeQueue];
                    var symbolInfoQueue = modelQueue.GetSymbolInfo(throwStatement.Expression);
                    var symbolQueue = symbolInfoQueue.Symbol;

                    if (symbolQueue == null)
                    {   // recover by using the overall semantic model
                        modelQueue = m_compilation.GetSemanticModel(treeQueue);
                        symbolInfoQueue = modelQueue.GetSymbolInfo(throwStatement.Expression);
                        symbolQueue = symbolInfoQueue.Symbol;
                    }

                    //var symbol = semanticModel.GetSymbolInfo(recoverStatement).Symbol as LocalSymbol;
                    //String typeName = symbol.Type.ToString();

                    if (symbolQueue != null)
                    {
                        exceptionName = symbolQueue.ContainingType.ToString();
                    }

                    //if (allInovkedExcetions.ContainsKey(exceptionName))
                    //{
                    //    allInovkedExcetions[exceptionName]++;
                    //}
                    //else
                    //{
                    //    allInovkedExcetions.Add(exceptionName, 1);
                    //}

                    AddPossibleExceptions(exceptionName, ref p_nodePossibleExceptions, "IsLoop");
                }
            }
        }

             */

        //public static Dictionary<string, int> GetAllInvokedMethodNamesByBFS(SyntaxNode inputSnippet,
        //    Dictionary<SyntaxTree, SemanticModel> treeAndModelDic, Compilation compilation)
        //{
        //    Dictionary<string, int> allInovkedMethods = new Dictionary<string, int>();
        //    // to save a code snippet and its backward level
        //    Queue<Tuple<SyntaxNode, int>> codeSnippetQueue = new Queue<Tuple<SyntaxNode, int>>();

        //    codeSnippetQueue.Enqueue(new Tuple<SyntaxNode, int>(inputSnippet, 0));

        //    while (codeSnippetQueue.Any())
        //    {
        //        Tuple<SyntaxNode, int> snippetAndLevel = codeSnippetQueue.Dequeue();
        //        var level = snippetAndLevel.Item2;
        //        var snippet = snippetAndLevel.Item1;
        //        var tree = snippet.SyntaxTree;
        //        List<InvocationExpressionSyntax> methodList = GetInvokedMethodsInACodeSnippet(snippet);

        //        foreach (var invocation in methodList)
        //        {
        //            string methodName = IOFile.MethodNameExtraction(invocation.ToString());
        //            try
        //            {
        //                // use a single semantic model
        //                var model = treeAndModelDic[tree];
        //                var symbolInfo = model.GetSymbolInfo(invocation);
        //                var symbol = symbolInfo.Symbol;

        //                if (symbol == null)
        //                {   // recover by using the overall semantic model
        //                    model = compilation.GetSemanticModel(tree);
        //                    symbolInfo = model.GetSymbolInfo(invocation);
        //                    symbol = symbolInfo.Symbol;
        //                }
        //                if (symbol != null)
        //                {
        //                    methodName = IOFile.MethodNameExtraction(symbol.ToString());
        //                }
        //                if (allInovkedMethods.ContainsKey(methodName))
        //                {
        //                    allInovkedMethods[methodName]++;
        //                }
        //                else
        //                {
        //                    allInovkedMethods.Add(methodName, 1);
        //                    if (level > 3) continue; // only go backward to 3 levels
        //                    if (methodName.StartsWith("System")) continue; // System API

        //                    if (symbol != null && AllMethodDeclarations.ContainsKey(symbol.ToString()))
        //                    {
        //                        // find the method declaration (go to definition)
        //                        var mdeclar = AllMethodDeclarations[symbol.ToString()];
        //                        codeSnippetQueue.Enqueue(new Tuple<SyntaxNode, int>(mdeclar, level + 1));
        //                    }
        //                }
        //            }
        //            catch (Exception e)
        //            {
        //                MergeDic<String>(ref allInovkedMethods,
        //                        new Dictionary<string, int>() { { methodName, 1 } });
        //                Logger.Log(tree.FilePath);
        //                Logger.Log(snippet.ToFullString());
        //                Logger.Log(invocation.ToFullString());
        //                Logger.Log(e);
        //                Logger.Log(e.StackTrace);
        //            }
        //        }
        //    }

        //    return allInovkedMethods;
        //}

        //public static Dictionary<string, int>[] GetAllInvokedMethodNamesAndExceptionsByBFS(SyntaxNode inputSnippet,
        //    Dictionary<SyntaxTree, SemanticModel> treeAndModelDic, Compilation compilation)
        //{
        //    Dictionary<string, int> allInovkedMethodsAndObjectCreation = new Dictionary<string, int>();
        //    Dictionary<string, int> allInovkedExcetions = new Dictionary<string, int>();

        //    Dictionary<string, int>[] allMethodsAndExceptions = new Dictionary<string, int>[2];
        //    // to save a code snippet and its backward level
        //    Queue<Tuple<SyntaxNode, int>> codeSnippetQueue = new Queue<Tuple<SyntaxNode, int>>();

        //    codeSnippetQueue.Enqueue(new Tuple<SyntaxNode, int>(inputSnippet, 0));

        //    while (codeSnippetQueue.Any())
        //    {
        //        Tuple<SyntaxNode, int> snippetAndLevel = codeSnippetQueue.Dequeue();
        //        var level = snippetAndLevel.Item2;
        //        var snippet = snippetAndLevel.Item1;
        //        var tree = snippet.SyntaxTree;

        //        List<InvocationExpressionSyntax> methodList = GetInvokedMethodsInACodeSnippet(snippet);
        //        List<ObjectCreationExpressionSyntax> objectCreationList = GetObjectCreationInACodeSnippet(snippet);
        //        List<ThrowStatementSyntax> throwList = GetInvokedThrowsInACodeSnippet(snippet);

        //        foreach (var invocation in methodList)
        //        {
        //            string methodName = IOFile.MethodNameExtraction(invocation.ToString());
        //            try
        //            {
        //                // use a single semantic model
        //                var model = treeAndModelDic[tree];
        //                var symbolInfo = model.GetSymbolInfo(invocation);
        //                var symbol = symbolInfo.Symbol;

        //                if (symbol == null)
        //                {   // recover by using the overall semantic model
        //                    model = compilation.GetSemanticModel(tree);
        //                    symbolInfo = model.GetSymbolInfo(invocation);
        //                    symbol = symbolInfo.Symbol;
        //                }
        //                if (symbol != null)
        //                {
        //                    methodName = IOFile.MethodNameExtraction(symbol.ToString());
        //                }
        //                if (allInovkedMethodsAndObjectCreation.ContainsKey(methodName))
        //                {
        //                    allInovkedMethodsAndObjectCreation[methodName]++;
        //                }
        //                else
        //                {
        //                    allInovkedMethodsAndObjectCreation.Add(methodName, 1);
        //                    if (level > 3) continue; // only go backward to 3 levels
        //                    //if (methodName.StartsWith("System")) continue; // System API

        //                    if (symbol != null /*&& AllMethodDeclarations.ContainsKey(symbol.ToString())*/)
        //                    {
        //                        // find the method declaration (go to definition)
        //                        var mdeclar = AllMethodDeclarations[symbol.ToString()];
        //                        codeSnippetQueue.Enqueue(new Tuple<SyntaxNode, int>(mdeclar, level + 1));
        //                    }
        //                }
        //            }
        //            catch (Exception e)
        //            {
        //                MergeDic<String>(ref allInovkedMethodsAndObjectCreation,
        //                        new Dictionary<string, int>() { { methodName, 1 } });
        //                Logger.Log(tree.FilePath);
        //                Logger.Log(snippet.ToFullString());
        //                Logger.Log(invocation.ToFullString());
        //                Logger.Log(e);
        //                Logger.Log(e.StackTrace);
        //            }
        //        }
        //        foreach (var objectCreation in objectCreationList)
        //        {
        //            string methodName = IOFile.MethodNameExtraction(objectCreation.ToString());
        //            try
        //            {
        //                // use a single semantic model
        //                var model = treeAndModelDic[tree];
        //                var symbolInfo = model.GetSymbolInfo(objectCreation);
        //                var symbol = symbolInfo.Symbol;

        //                if (symbol == null)
        //                {   // recover by using the overall semantic model
        //                    model = compilation.GetSemanticModel(tree);
        //                    symbolInfo = model.GetSymbolInfo(objectCreation);
        //                    symbol = symbolInfo.Symbol;
        //                }
        //                if (symbol != null)
        //                {
        //                    methodName = IOFile.MethodNameExtraction(symbol.ToString());
        //                }
        //                if (allInovkedMethodsAndObjectCreation.ContainsKey(methodName))
        //                {
        //                    allInovkedMethodsAndObjectCreation[methodName]++;
        //                }
        //                else
        //                {
        //                    allInovkedMethodsAndObjectCreation.Add(methodName, 1);
        //                    if (level > 3) continue; // only go backward to 3 levels
        //                    if (methodName.StartsWith("System")) continue; // System API

        //                    if (symbol != null && AllMethodDeclarations.ContainsKey(symbol.ToString()))
        //                    {
        //                        // find the method declaration (go to definition)
        //                        var mdeclar = AllMethodDeclarations[symbol.ToString()];
        //                        codeSnippetQueue.Enqueue(new Tuple<SyntaxNode, int>(mdeclar, level + 1));
        //                    }
        //                }
        //            }
        //            catch (Exception e)
        //            {
        //                MergeDic<String>(ref allInovkedMethodsAndObjectCreation,
        //                        new Dictionary<string, int>() { { methodName, 1 } });
        //                Logger.Log(tree.FilePath);
        //                Logger.Log(snippet.ToFullString());
        //                Logger.Log(objectCreation.ToFullString());
        //                Logger.Log(e);
        //                Logger.Log(e.StackTrace);
        //            }
        //        }
        //        foreach (var throwStatement in throwList)
        //        {
        //            string exceptionName = "";

        //            var model = treeAndModelDic[tree];
        //            var symbolInfo = model.GetSymbolInfo(throwStatement.Expression);
        //            var symbol = symbolInfo.Symbol;

        //            //var symbol = semanticModel.GetSymbolInfo(recoverStatement).Symbol as LocalSymbol;
        //            //String typeName = symbol.Type.ToString();

        //            if (symbol == null)
        //            {   // recover by using the overall semantic model
        //                model = compilation.GetSemanticModel(tree);
        //                symbolInfo = model.GetSymbolInfo(throwStatement.Expression);
        //                symbol = symbolInfo.Symbol;
        //            }
        //            if (symbol != null)
        //            {
        //                exceptionName = symbol.ContainingType.ToString();
        //            }

        //            if (allInovkedExcetions.ContainsKey(exceptionName))
        //            {
        //                allInovkedExcetions[exceptionName]++;
        //            }
        //            else
        //            {
        //                allInovkedExcetions.Add(exceptionName, 1);
        //            }
        //        }
        //    }

        //    allMethodsAndExceptions[0] = allInovkedMethodsAndObjectCreation;
        //    allMethodsAndExceptions[1] = allInovkedExcetions;

        //    return allMethodsAndExceptions;
        //}

        public static List<InvocationExpressionSyntax> GetInvokedMethodsInACodeSnippet(SyntaxNode codeSnippet)
        {
            List<InvocationExpressionSyntax> methodList;

            bool hasTryStatement = codeSnippet.DescendantNodes()
                    .OfType<TryStatementSyntax>().Any();

            if (hasTryStatement == true)
            {
                TryStatementSkipper tryblockskipper = new TryStatementSkipper();
                tryblockskipper.Visit(codeSnippet);
                methodList = tryblockskipper.invokedMethods;
            }
            else // has no try statement inside
            {
                methodList = codeSnippet.DescendantNodes().OfType<InvocationExpressionSyntax>().ToList();
            }

            return methodList;

            //var updatedMethodList = methodList.Where(method => !IsLoggingStatement(method)).ToList();
            //return updatedMethodList;
        }
        public static List<ObjectCreationExpressionSyntax> GetObjectCreationInACodeSnippet(SyntaxNode codeSnippet)
        {
            List<ObjectCreationExpressionSyntax> objectCreationList;

            bool hasTryStatement = codeSnippet.DescendantNodes()
                    .OfType<TryStatementSyntax>().Any();

            if (hasTryStatement == true)
            {
                TryStatementSkipper tryblockskipper = new TryStatementSkipper();
                tryblockskipper.Visit(codeSnippet);
                objectCreationList = tryblockskipper.objectCreationExpressions;
            }
            else // has no try statement inside
            {
                objectCreationList = codeSnippet.DescendantNodes().OfType<ObjectCreationExpressionSyntax>().ToList();
            }

            return objectCreationList;

            //var updatedMethodList = methodList.Where(method => !IsLoggingStatement(method)).ToList();
            //return updatedMethodList;
        }
        public static List<ThrowStatementSyntax> GetInvokedThrowsInACodeSnippet(SyntaxNode codeSnippet)
        {
            List<ThrowStatementSyntax> throwList;

            bool hasTryStatement = codeSnippet.DescendantNodes()
                    .OfType<TryStatementSyntax>().Any();

            if (hasTryStatement == true)
            {
                TryStatementSkipper tryblockskipper = new TryStatementSkipper();
                tryblockskipper.Visit(codeSnippet);
                throwList = tryblockskipper.invokedThrows;


            }
            else // has no try statement inside
            {
                throwList = codeSnippet.DescendantNodes().OfType<ThrowStatementSyntax>().ToList();
            }

            var updatedMethodList = throwList; //.Where(method => !IsLoggingStatement(method)).ToList();
            return updatedMethodList;
        }
        #endregion Possible exceptions

    }
}
