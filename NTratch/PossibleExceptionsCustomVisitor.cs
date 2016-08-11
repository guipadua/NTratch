using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NTratch;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace NTratch
{
    /// <summary>
    /// This visitor is focused to be called by a try block.
    /// It will evaluate the existing nodes on the try that are of type: InvocationExpressionSyntax and ObjectCreationExpressionSyntax
    /// </summary>
    class PossibleExceptionsCustomVisitor : CSharpSyntaxWalker
    {
        private INamedTypeSymbol m_exceptionType;
        private Dictionary<SyntaxTree, SemanticModel> m_treeAndModelDic;
        private Compilation m_compilation;

        private Dictionary<string, sbyte> invokedMethodsBinded = new Dictionary<string, sbyte>();

        //private Dictionary<string, Dictionary<string, sbyte>> invokedMethodsHandlerType = new Dictionary<string, Dictionary<string, sbyte>>();
        private Dictionary<string, Dictionary<string, Dictionary<string, sbyte>>> invokedMethodsPossibleExceptions = new Dictionary<string, Dictionary<string, Dictionary<string, sbyte>>>();

        private int numSpecificHandler = 0;
        private int numSubsumptionHandler = 0;
        private int numSupersumptionHandler = 0;
        private int numOtherHandler = 0;
        private int numMethodsNotBinded = 0;

        private int numIsXMLSemantic = 0;
        private int numIsXMLSyntax = 0;
        private int numIsLoop = 0;

        private int m_maxLevel = 0;

        public PossibleExceptionsCustomVisitor(INamedTypeSymbol p_exceptionType, Dictionary<SyntaxTree, SemanticModel> p_treeAndModelDic, Compilation p_compilation)
        {
            m_exceptionType = p_exceptionType;
            m_compilation = p_compilation;
            m_treeAndModelDic = p_treeAndModelDic;
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            processNode(node);
        }

        public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            processNode(node);
        }

        private void processNode(SyntaxNode p_node)
        {
            Dictionary<string, Dictionary<string, sbyte>> nodePossibleExceptions = new Dictionary<string, Dictionary<string, sbyte>>();

            var nodeString = "";

            // Checking for Binding (Semantic) information - BEGIN
            var p_nodeTree = p_node.SyntaxTree;
            
            // // use a single semantic model
            var p_nodeModel = m_treeAndModelDic[p_nodeTree];
            var p_nodeSymbolInfo = p_nodeModel.GetSymbolInfo(p_node);
            var p_nodeSymbol = p_nodeSymbolInfo.Symbol;

            if (p_nodeSymbol == null)
            {   // // recover by using the overall semantic model
                p_nodeModel = m_compilation.GetSemanticModel(p_nodeTree);
                p_nodeSymbolInfo = p_nodeModel.GetSymbolInfo(p_node);
                p_nodeSymbol = p_nodeSymbolInfo.Symbol;
            }
            
            // //Flag if binded or not and get the name of the node that will be used
            if (p_nodeSymbol != null)
            {
                nodeString = p_nodeSymbol.ToString();
                invokedMethodsBinded[nodeString] = 1;                
            }
            else
            {
                nodeString = p_node.ToString();
                invokedMethodsBinded[nodeString] = 0;
                numMethodsNotBinded++;                
            }

            // Checking for Binding (Semantic) information - END

            //Get the declaration for this node (Syntax) - BEGIN

            BaseMethodDeclarationSyntax p_nodemDeclar = null;
            if (CodeAnalyzer.AllMethodDeclarations.ContainsKey(nodeString))
            {
                p_nodemDeclar = CodeAnalyzer.AllMethodDeclarations[nodeString];
            }
            //Get the declaration for this node (Syntax) - BEGIN

            // Obtaining possible exceptions lists using 3 methods - BEGIN

            // // Exceptions from the XML found on the Semantic model - IsXMLSemantic
            var xmlTextSemantic = p_nodeSymbol?.GetDocumentationCommentXml();

            if (xmlTextSemantic == null || xmlTextSemantic == "")
            {   // // recover by using the overall semantic model
                xmlTextSemantic = m_compilation.GetSemanticModel(p_nodeTree).GetSymbolInfo(p_node).Symbol?.GetDocumentationCommentXml();
            }

            if (xmlTextSemantic != null && xmlTextSemantic != "")
            {
                XmlDocument xmlDocFromSemantic = new XmlDocument();
                xmlDocFromSemantic.LoadXml("<comment_root>" + xmlTextSemantic + "</comment_root>");

                XmlNodeList nodeListFromSemantic;
                XmlNode rootFromSemantic = xmlDocFromSemantic.DocumentElement;

                nodeListFromSemantic = rootFromSemantic.SelectNodes("descendant::exception");

                foreach (XmlNode exception in nodeListFromSemantic)
                {
                    var exceptionTypeName = exception.Attributes.GetNamedItem("cref").InnerText.Replace("T:", "");
                    //var exceptionType = m_compilation.GetTypeByMetadataName(exceptionTypeName);

                    if (!nodePossibleExceptions.ContainsKey(exceptionTypeName))
                        nodePossibleExceptions.Add(exceptionTypeName, BaseDicWithHandlerType(exceptionTypeName));

                    nodePossibleExceptions[exceptionTypeName]["IsXMLSemantic"] = 1;
                    numIsXMLSemantic++;
                }
            }
            // // Exceptions from the XML found on the Syntax model - IsXMLSyntax
            var xmlTextSyntax = p_nodemDeclar?.DescendantTrivia().ToList().First(trivia => trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)).ToString();
            
            if (xmlTextSyntax != null)
            {
                XmlDocument xmlDocFromSyntax = new XmlDocument();
                xmlDocFromSyntax.LoadXml("<comment_root>" + xmlTextSyntax + "</comment_root>");

                XmlNodeList nodeListFromSyntax;
                XmlNode root = xmlDocFromSyntax.DocumentElement;

                nodeListFromSyntax = root.SelectNodes("descendant::exception");

                foreach (XmlNode exception in nodeListFromSyntax)
                {
                    var exceptionTypeName = exception.Attributes.GetNamedItem("cref").InnerText.Replace("T:", "");
                    //var exceptionType = m_compilation.GetTypeByMetadataName(exceptionTypeName);

                    if (!nodePossibleExceptions.ContainsKey(exceptionTypeName))
                        nodePossibleExceptions.Add(exceptionTypeName, BaseDicWithHandlerType(exceptionTypeName));

                    nodePossibleExceptions[exceptionTypeName]["IsXMLSyntax"] = 1;
                    numIsXMLSyntax++;
                }
            }
            
                // // Exceptions from throw declarations recursive search - IsLoop

            Dictionary<string, int> allInovkedMethods = new Dictionary<string, int>();
            Dictionary<string, int> allInovkedExcetions = new Dictionary<string, int>();

            // to save a code snippet and its backward level
            Queue<Tuple<SyntaxNode, int>> codeSnippetQueue = new Queue<Tuple<SyntaxNode, int>>();
            
            //Queue the current method declaration if existent
            if(p_nodemDeclar != null)
                codeSnippetQueue.Enqueue(new Tuple<SyntaxNode, int>(p_nodemDeclar, 0));
            
            while (codeSnippetQueue.Any())
            {
                Tuple<SyntaxNode, int> snippetAndLevel = codeSnippetQueue.Dequeue();
                var level = snippetAndLevel.Item2;
                if(level > m_maxLevel)
                    m_maxLevel = level;

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

                            if (symbolQueue != null && CodeAnalyzer.AllMethodDeclarations.ContainsKey(symbolQueue.ToString()))
                            {
                                // find the method declaration (go to definition)
                                var mdeclar = CodeAnalyzer.AllMethodDeclarations[symbolQueue.ToString()];
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

                            if (symbolQueue != null && CodeAnalyzer.AllMethodDeclarations.ContainsKey(symbolQueue.ToString()))
                            {
                                // find the method declaration (go to definition)
                                var mdeclar = CodeAnalyzer.AllMethodDeclarations[symbolQueue.ToString()];
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

                    if (allInovkedExcetions.ContainsKey(exceptionName))
                    {
                        allInovkedExcetions[exceptionName]++;
                    }
                    else
                    {
                        allInovkedExcetions.Add(exceptionName, 1);
                    }

                    if (!nodePossibleExceptions.ContainsKey(exceptionName))
                        nodePossibleExceptions.Add(exceptionName, BaseDicWithHandlerType(exceptionName));

                    nodePossibleExceptions[exceptionName]["IsLoop"] = 1;
                    numIsLoop++;
                }
            }

            // Obtaining possible exceptions lists using 3 methods - END
            invokedMethodsPossibleExceptions[nodeString] = nodePossibleExceptions;
            
        }

        private Dictionary<string, sbyte> BaseDicWithHandlerType (string exceptionTypeName)
        {
            //Default value definition for when coming from XML Semantics
            var dicXmlFromSemantic = new Dictionary<string, sbyte>();
            dicXmlFromSemantic.Add("HandlerTypeCode", -9);
            dicXmlFromSemantic.Add("IsXMLSemantic", 0);
            dicXmlFromSemantic.Add("IsXMLSyntax", 0);
            dicXmlFromSemantic.Add("IsLoop", 0);

            if (m_exceptionType != null)
            {
                INamedTypeSymbol type = m_compilation.GetTypeByMetadataName(exceptionTypeName);
                if(type != null)
                { 
                    //In case is the same type, it's specific handler type - code: 0
                    //In case the catched type is equal a super class of the possible thrown type, it's a subsumption - code: 1
                    //In case the possible thrown type is equal a super class of the catched type, it's a supersumption - code: 2
                    //In case it's none of the above - most likely tree of unrelated exceptions: code: 3
                    if (m_exceptionType.Equals(type))
                    {
                        dicXmlFromSemantic["HandlerTypeCode"] = 0;
                        numSpecificHandler++;
                    }
                    else if (IsSuperType(m_exceptionType, type))
                    {
                        dicXmlFromSemantic["HandlerTypeCode"] = 1;
                        numSubsumptionHandler++;
                    }
                    else if (IsSuperType(type, m_exceptionType))
                    {
                        dicXmlFromSemantic["HandlerTypeCode"] = 2;
                        numSupersumptionHandler++;
                    }
                    else
                    {
                        //it can happen when exceptions are not related on the type tree
                        dicXmlFromSemantic["HandlerTypeCode"] = 3;
                        numOtherHandler++;
                    }
                } else
                    dicXmlFromSemantic["HandlerTypeCode"] = -8;
            }

            return dicXmlFromSemantic;
        }
        
        public string PrintInvokedMethodsPossibleExceptions()
        {
            return PrintDictionary(invokedMethodsPossibleExceptions);
        }

        public string PrintInvokedMethodsBinded()
        {
            return PrintDictionary(invokedMethodsBinded);            
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
            return invokedMethodsPossibleExceptions.Count;
        }
        
        public int getNumDistinctPossibleExceptions()
        {
            int numDistinctPossibleExceptions = 0;

            //invokedMethodsPossibleExceptions.

            foreach (var entry in invokedMethodsPossibleExceptions.Values)
            {
                numDistinctPossibleExceptions += entry.Keys.Count;                
            }
            return numDistinctPossibleExceptions;
        }
        public int getNumSpecificHandler()
        {
            return numSpecificHandler;
        }
        public int getNumSubsumptionHandler()
        {
            return numSubsumptionHandler;
        }
        public int getNumSupersumptionHandler()
        {
            return numSupersumptionHandler;
        }
        public int getNumOtherHandler()
        {
            return numOtherHandler;
        }
        public int getNumMethodsNotBinded()
        {
            return numMethodsNotBinded;
        }
        public int getNumIsXMLSemantic()
        {
            return numIsXMLSemantic;
        }
        public int getNumIsLoop()
        {
            return numIsLoop;
        }
        public int getNumIsXMLSyntax()
        {
            return numIsXMLSyntax;
        }


        internal int getMaxLevel()
        {
            return m_maxLevel;
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
