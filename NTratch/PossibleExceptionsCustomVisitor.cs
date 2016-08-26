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
        private INamedTypeSymbol m_exceptionType;
        private Dictionary<SyntaxTree, SemanticModel> m_treeAndModelDic;
        private Compilation m_compilation;

        private Dictionary<string, sbyte> m_invokedMethodsBinded = new Dictionary<string, sbyte>();

        //private Dictionary<string, Dictionary<string, sbyte>> invokedMethodsHandlerType = new Dictionary<string, Dictionary<string, sbyte>>();
        private Dictionary<string, Dictionary<string, Dictionary<string, sbyte>>> m_invokedMethodsPossibleExceptions = new Dictionary<string, Dictionary<string, Dictionary<string, sbyte>>>();

        private Dictionary<string, Dictionary<string, sbyte>> m_possibleExceptions = new Dictionary<string, Dictionary<string, sbyte>>();

        private int m_nodeMaxLevel = 0;

        private int m_myLevel = 0;
        private Dictionary<string, int> m_ChildrenNodesLevel = new Dictionary<string, int>();

        public bool m_isForAnalysis { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="p_isForAnalysis">This is know if the found exceptions should be evaluated against the parent try-catch block, if any.</param>
        public PossibleExceptionsCustomVisitor(ref INamedTypeSymbol p_exceptionType, ref Dictionary<SyntaxTree, SemanticModel> p_treeAndModelDic, ref Compilation p_compilation, bool p_isForAnalysis, int p_level)
        {
            m_exceptionType = p_exceptionType;
            m_compilation = p_compilation;
            m_treeAndModelDic = p_treeAndModelDic;
            m_isForAnalysis = p_isForAnalysis;
            m_myLevel = p_level;            
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            processExpressionNodeAndVisit(node);
        }

        public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            processExpressionNodeAndVisit(node);
        }

        public override void VisitThrowStatement(ThrowStatementSyntax node)
        {
            //processNode(node);
            string exceptionName = "";

            if (node.Expression != null)
            {
                var treeThrow = node.SyntaxTree;
                var modelThrow = m_treeAndModelDic[treeThrow];
                var symbolInfoThrow = modelThrow.GetSymbolInfo(node.Expression);
                var symbolThrow = symbolInfoThrow.Symbol;

                if (symbolThrow == null)
                {   // recover by using the overall semantic model
                    modelThrow = m_compilation.GetSemanticModel(treeThrow);
                    symbolInfoThrow = modelThrow.GetSymbolInfo(node.Expression);
                    symbolThrow = symbolInfoThrow.Symbol;
                }

                //var symbol = semanticModel.GetSymbolInfo(recoverStatement).Symbol as LocalSymbol;
                //String typeName = symbol.Type.ToString();

                if (symbolThrow != null)
                {
                    exceptionName = symbolThrow.ContainingType.ToString();
                }
            } else
            {
                exceptionName = "!NO_EXCEPTION_DECLARED!";
            }

            var possibleException = new Dictionary<string, Dictionary<string, sbyte>>();

            AddPossibleExceptions(exceptionName, ref possibleException, "IsThrow");

            //check if inside a Try
            if (!m_isForAnalysis)
                MergePossibleExceptionsDic(ref m_possibleExceptions, getValidPossibleExceptions(node, possibleException));
            else
                MergePossibleExceptionsDic(ref m_possibleExceptions, possibleException);

        }

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

        private Dictionary<string, Dictionary<string, sbyte>> getValidPossibleExceptions(SyntaxNode node, Dictionary<string, Dictionary<string, sbyte>> p_possibleExceptions)
        {

            Dictionary<string, Dictionary<string, sbyte>> validPossibleExceptions = new Dictionary<string, Dictionary<string, sbyte>>();

            var parentTry = CodeAnalyzer.FindParentTry(node) as TryStatementSyntax;

            if (parentTry == null)
            {
                return p_possibleExceptions;
            }
            else
            {
                foreach (var catchblock in parentTry.Catches)
                {
                    var tree = catchblock.SyntaxTree;
                    var model = m_treeAndModelDic[tree];

                    string exceptionType;

                    TypeSyntax exceptionTypeSyntax = null;
                    INamedTypeSymbol exceptionNamedTypeSymbol = null;

                    if (catchblock.Declaration != null)
                    {
                        exceptionTypeSyntax = catchblock.Declaration.Type;
                        exceptionNamedTypeSymbol = model.GetTypeInfo(exceptionTypeSyntax).ConvertedType as INamedTypeSymbol;

                        if (exceptionNamedTypeSymbol != null)
                        {
                            exceptionType = exceptionNamedTypeSymbol.ToString();

                            foreach (var possibleThrow in p_possibleExceptions)
                            {
                                sbyte handlerTypeCode;
                                handlerTypeCode = GetHandlerTypeCode(exceptionNamedTypeSymbol, possibleThrow.Key);

                                //0: SPECIFIC, 1: SUBSUMPTION - the only two possible ways to really catch
                                if (!(handlerTypeCode == 0 || handlerTypeCode == 1))
                                {
                                    if (!validPossibleExceptions.ContainsKey(possibleThrow.Key))
                                        validPossibleExceptions.Add(possibleThrow.Key, possibleThrow.Value);
                                }
                            }
                        }
                        else
                            exceptionType = "!NO_NAMED_TYPE!";
                    }
                    else
                        exceptionType = "!NO_EXCEPTION_DECLARED!";
                }
            }

            return validPossibleExceptions;
        }

        private void processExpressionNodeAndVisit(SyntaxNode node)
        {
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
        }

        private ISymbol GetNodeSymbol(SyntaxNode p_node)
        {
            // Checking for Binding (Semantic) information - BEGIN
            var nodeTree = p_node.SyntaxTree;

            // // use a single semantic model
            var nodeModel = m_treeAndModelDic[nodeTree];
            var nodeSymbolInfo = nodeModel.GetSymbolInfo(p_node);
            var nodeSymbol = nodeSymbolInfo.Symbol;

            // // recover by using the overall semantic model
            if (nodeSymbol == null)
            {
                nodeModel = m_compilation.GetSemanticModel(nodeTree);
                nodeSymbolInfo = nodeModel.GetSymbolInfo(p_node);
                nodeSymbol = nodeSymbolInfo.Symbol;
            }
            
            return nodeSymbol;
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
        
        private void processExpressionNode(SyntaxNode p_node, ref Dictionary<string, Dictionary<string, sbyte>> p_nodePossibleExceptions, ref BaseMethodDeclarationSyntax p_nodemDeclar)
        {
           
            // Obtaining possible exceptions lists using 3 methods - BEGIN

          

            // // Exceptions from throw declarations recursive search - IsLoop

            //processIsLoop(ref p_nodemDeclar, ref p_nodePossibleExceptions);

            // Obtaining possible exceptions lists using 3 methods - END
           
        }

        private void processNodeForXMLSemantic(SyntaxNode p_node, ref ISymbol p_nodeSymbol,  ref Dictionary<string, Dictionary<string, sbyte>> p_nodePossibleExceptions)
        {
            // // Exceptions from the XML found on the Semantic model - IsXMLSemantic
            var xmlTextSemantic = p_nodeSymbol.GetDocumentationCommentXml();
            if (xmlTextSemantic == null || xmlTextSemantic == "")
            {   // // recover by using the overall semantic model
                xmlTextSemantic = m_compilation.GetSemanticModel(p_node.SyntaxTree).GetSymbolInfo(p_node).Symbol?.GetDocumentationCommentXml();
            }
            NodeFindExceptionsInXML(xmlTextSemantic, ref p_nodePossibleExceptions, "IsXMLSemantic");            
        }

        private Dictionary<string, Dictionary<string, sbyte>> GetExceptionsFromXMLSyntax(ref BaseMethodDeclarationSyntax p_nodemDeclar)
        {
            Dictionary<string, Dictionary<string, sbyte>> nodePossibleExceptions = new Dictionary<string, Dictionary<string, sbyte>>();

            // // Exceptions from the XML found on the Syntax model - IsXMLSyntax
            var xmlTextSyntax = p_nodemDeclar
                                    .DescendantTrivia()
                                    .ToList()
                                    .FirstOrDefault(trivia => trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia))
                                    .ToString();
            NodeFindExceptionsInXML(xmlTextSyntax, ref nodePossibleExceptions, "IsXMLSyntax");

            return nodePossibleExceptions;

        }

        public void NodeFindExceptionsInXML(string xml, ref Dictionary<string, Dictionary<string, sbyte>> p_nodePossibleExceptions, string p_originKey)
        {
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

                    //var exceptionType = m_compilation.GetTypeByMetadataName(exceptionTypeName);

                    AddPossibleExceptions(exceptionTypeName, ref p_nodePossibleExceptions, p_originKey);                    
                }
            }
        }

        static string RemoveInvalidXmlChars(string text)
        {
            var validXmlChars = text.Where(ch => XmlConvert.IsXmlChar(ch)).ToArray();

            var validXmlString = new string(validXmlChars);

            return validXmlString.Replace("&", "");
        }

        public void AddPossibleExceptions(string p_exceptionTypeName, ref Dictionary<string, Dictionary<string, sbyte>> p_PossibleExceptions, string p_originKey)
        {
            if (!p_PossibleExceptions.ContainsKey(p_exceptionTypeName))
                p_PossibleExceptions.Add(p_exceptionTypeName, BaseDicWithHandlerType(p_exceptionTypeName));

            p_PossibleExceptions[p_exceptionTypeName][p_originKey] = 1;

            int previousDeepestLevel = Convert.ToInt16(p_PossibleExceptions[p_exceptionTypeName]["DeepestLevelFound"]);
            int currentDeepestLevel = Convert.ToInt16(m_myLevel);

            if (currentDeepestLevel > previousDeepestLevel)
                p_PossibleExceptions[p_exceptionTypeName]["DeepestLevelFound"] = Convert.ToSByte(currentDeepestLevel);

        }

        private Dictionary<string, sbyte> BaseDicWithHandlerType (string exceptionTypeName)
        {
            //Default value definition for when coming from XML Semantics
            var dicXmlFromSemantic = new Dictionary<string, sbyte>();
            dicXmlFromSemantic.Add("HandlerTypeCode", -9);
            dicXmlFromSemantic.Add("IsXMLSemantic", 0);
            dicXmlFromSemantic.Add("IsXMLSyntax", 0);
            //dicXmlFromSemantic.Add("IsLoop", 0);
            dicXmlFromSemantic.Add("IsThrow", 0);
            dicXmlFromSemantic.Add("DeepestLevelFound", 0);
            
            dicXmlFromSemantic["HandlerTypeCode"] = GetHandlerTypeCode(m_exceptionType, exceptionTypeName);

            return dicXmlFromSemantic;
        }

        public sbyte GetHandlerTypeCode(INamedTypeSymbol catchedType, string possibleThrowType)
        {
            sbyte handlerTypeCode = -9;
            if (catchedType != null)
            {
                INamedTypeSymbol type = m_compilation.GetTypeByMetadataName(possibleThrowType);
                if (type != null)
                {
                    //In case is the same type, it's specific handler type - code: 0
                    //In case the catched type is equal a super class of the possible thrown type, it's a subsumption - code: 1
                    //In case the possible thrown type is equal a super class of the catched type, it's a supersumption - code: 2
                    //In case it's none of the above - most likely tree of unrelated exceptions: code: 3
                    if (catchedType.Equals(type))
                    {
                        handlerTypeCode = 0;                        
                    }
                    else if (IsSuperType(catchedType, type))
                    {
                        handlerTypeCode = 1;                        
                    }
                    else if (IsSuperType(type, catchedType))
                    {
                        handlerTypeCode = 2;                        
                    }
                    else
                    {
                        //it can happen when exceptions are not related on the type tree
                        handlerTypeCode = 3;                        
                    }
                }
                else
                    handlerTypeCode = -8;
            }
            return handlerTypeCode;
        }

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

        public string PrintInvokedMethodsPossibleExceptions()
        {
            return PrintDictionary(m_invokedMethodsPossibleExceptions);
        }

        public string PrintInvokedMethodsBinded()
        {
            return PrintDictionary(m_invokedMethodsBinded);            
        }

        public string PrintDictionary(Dictionary<string, Dictionary<string, Dictionary<string, sbyte>>> entrySet)
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

        public string PrintDictionary(Dictionary<string, Dictionary<string, sbyte>> entrySet)
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
                
        /// <summary>
        /// To check whether an invocation is a logging statement
        /// </summary>

        /**
         * Recursively find if the given subtype is a supertype of the reference type.
         *  
         * @param subtype type to evaluate
         * @param referenceType initial tracing reference to detect the super type
         */
        private Boolean IsSuperType(INamedTypeSymbol subType, INamedTypeSymbol referenceType)
        {

            if (subType == null || referenceType == null || referenceType.Equals(m_compilation.GetTypeByMetadataName("System.Object")))
                return false;

            if (subType.Equals(referenceType.BaseType))
                return true;

            return IsSuperType(subType, referenceType.BaseType);

        }

        public int countInvokedMethodsHandlerType()
        {
            return m_invokedMethodsPossibleExceptions.Count;
        }
        
        public int getNumDistinctPossibleExceptions()
        {
            return m_possibleExceptions.Count;
        }

        public string PrintDistinctPossibleExceptions()
        {
            return PrintDictionary(m_possibleExceptions);
        }

        public int CountMetricsForExceptions(string strKey, sbyte sbyteCode)
        {
            return m_possibleExceptions.Sum(exception => exception.Value.Count(key => (key.Key == strKey && key.Value == sbyteCode)));
        }
        public int getNumSpecificHandler()
        {
            return CountMetricsForExceptions("HandlerTypeCode", 0);
        }
        public int getNumSubsumptionHandler()
        {
            return CountMetricsForExceptions("HandlerTypeCode", 1);
        }
        public int getNumSupersumptionHandler()
        {
            return CountMetricsForExceptions("HandlerTypeCode", 2);
        }
        public int getNumOtherHandler()
        {
            return CountMetricsForExceptions("HandlerTypeCode", 3);
        }
        public int getNumMethodsNotBinded()
        {
            return m_invokedMethodsBinded.Count(entry => entry.Value == 0);
        }
        public int getNumIsXMLSemantic()
        {
            return CountMetricsForExceptions("IsXMLSemantic", 1);
        }
        public int getNumIsLoop()
        {
            return CountMetricsForExceptions("IsLoop", 1);
        }
        public int getNumIsXMLSyntax()
        {
            return CountMetricsForExceptions("IsXMLSyntax", 1);
        }
        public int getNumIsThrow()
        {
            return CountMetricsForExceptions("IsThrow", 1);
        }


        internal int getChildrenMaxLevel()
        {
            return (m_ChildrenNodesLevel.Values.Count > 0) ? m_ChildrenNodesLevel.Values.Max() : 0;
        }



        #region Possible exceptions
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
