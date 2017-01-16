using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NTratch;
using System;
using System.Collections.Generic;

class ASTUtilities
{
    public static SyntaxNode FindParent(SyntaxNode node)
    {
        SyntaxNode parentNode = node.Parent;

        if (!(parentNode.IsKind(SyntaxKind.Block)))
            return parentNode;

        return FindParent(parentNode);
    }

    public static SyntaxNode FindParentMethod(SyntaxNode node)
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

    public static string FindParentType(SyntaxNode node, SemanticModel model)
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

    //public static string FindParentMethodName(SyntaxNode parentNode)
    //{
    //    string parentMethodName;
    //    if (parentNode.IsKind(SyntaxKind.MethodDeclaration))
    //    {
    //        MethodDeclarationSyntax parentMethod = parentNode as MethodDeclarationSyntax;

    //        parentMethodName = '"' + parentMethod.Identifier.ToString();
    //        parentMethodName += "(";

    //        foreach (var param in parentMethod.ParameterList.Parameters)
    //        {
    //            parentMethodName += param.Type.ToString() + ";";
    //        }
    //        parentMethodName += ")" + '"';

    //        parentMethodName = parentMethodName.Replace(";)", ")");
    //    }
    //    else if (parentNode.IsKind(SyntaxKind.ConstructorDeclaration))
    //    {
    //        ConstructorDeclarationSyntax parentConstructor = parentNode as ConstructorDeclarationSyntax;

    //        parentMethodName = '"' + parentConstructor.Identifier.ToString();
    //        parentMethodName += "(";

    //        foreach (var param in parentConstructor.ParameterList.Parameters)
    //        {
    //            parentMethodName += param.Type.ToString() + ";";
    //        }
    //        parentMethodName += ")" + '"';

    //        parentMethodName = parentMethodName.Replace(";)", ")");
    //    }
    //    else
    //        parentMethodName = "!UNEXPECTED_KIND!"; //there might be other cases like the initializer one for java

    //    return parentMethodName;
    //}

    public static string GetMethodName(SyntaxNode node,
            Dictionary<SyntaxTree, SemanticModel> treeAndModelDic, Compilation compilation)
    {
        string methodName;
        if (node.IsKind(SyntaxKind.MethodDeclaration) || node.IsKind(SyntaxKind.ConstructorDeclaration))
        {
            BaseMethodDeclarationSyntax method = (BaseMethodDeclarationSyntax) node;
            string methodNameFromSymbol = GetNodeDeclaredSymbol(method, node.SyntaxTree, treeAndModelDic, compilation).ToString();
            
            if (methodNameFromSymbol != null)
                methodName = methodNameFromSymbol;
            else
            {
                methodName = GetMethodNameWithoutBinding(method, GetMethodIdentifier(node));
            }
        }
        else if (node.IsKind(SyntaxKind.ClassDeclaration))
        {
            methodName = "!USE_PARENT!"; //name not applicable
        }
        else
            methodName = "!UNEXPECTED_KIND!";

        return methodName;
    }

    public static ISymbol GetNodeDeclaredSymbol(BaseMethodDeclarationSyntax node, SyntaxTree tree,
            Dictionary<SyntaxTree, SemanticModel> treeAndModelDic, Compilation compilation)
    {
        var model = treeAndModelDic[tree];
        
        ISymbol methodSymbol = null;
        try
        {
            methodSymbol = model.GetDeclaredSymbol(node);
            if (methodSymbol == null)
            {
                model = compilation.GetSemanticModel(tree);
                methodSymbol = model.GetDeclaredSymbol(node);
            }
        }
        catch
        {
            try
            {
                model = compilation.GetSemanticModel(tree);
                methodSymbol = model.GetDeclaredSymbol(node);
            }
            catch {
                Logger.Log("WARN - symbol not found for node: " + node.ToString());
            }
        }

            return methodSymbol;        
    }

    public static ISymbol GetNodeSymbol(SyntaxNode node, 
        Dictionary<SyntaxTree, SemanticModel> treeAndModelDic, Compilation compilation)
    {
        var tree = node.SyntaxTree;
        var model = treeAndModelDic[tree];

        ISymbol nodeSymbol = null;
        try
        {
            nodeSymbol = model.GetSymbolInfo(node).Symbol;
            if (nodeSymbol == null)
            {
                model = compilation.GetSemanticModel(tree);
                nodeSymbol = model.GetSymbolInfo(node).Symbol;
            }
        }
        catch
        {
            try
            {
                model = compilation.GetSemanticModel(tree);
                nodeSymbol = model.GetSymbolInfo(node).Symbol;
            }
            catch {
                Logger.Log("WARN - symbol not found for node: " + node.ToString());
            }
        }

        return nodeSymbol;
    }

    public static string GetMethodIdentifier(SyntaxNode node)
    {
        string identifier = ""; ;
        if (node.IsKind(SyntaxKind.MethodDeclaration))
        {
            MethodDeclarationSyntax nodeMethod = node as MethodDeclarationSyntax;
            identifier = nodeMethod.Identifier.ToString();
        }
        else if (node.IsKind(SyntaxKind.MethodDeclaration))
        {
            ConstructorDeclarationSyntax nodeConstructor = node as ConstructorDeclarationSyntax;
            identifier = nodeConstructor.Identifier.ToString();
        }
        else if (node.IsKind(SyntaxKind.MethodDeclaration))
        {
            ClassDeclarationSyntax nodeClass = node as ClassDeclarationSyntax;
            identifier = nodeClass.Identifier.ToString();
        }
        return identifier;
    }

    public static string GetMethodNameWithoutBinding(BaseMethodDeclarationSyntax method, string identifier)
    {
        string methodName;

        methodName = identifier;
        methodName += "(";

        foreach (var param in method.ParameterList.Parameters)
        {
            methodName += param.Type.ToString() + ";";
        }
        methodName += ")";

        methodName = methodName.Replace(";)", ")");
        
        return methodName;
    }
    /**
     * Recursively find if the given subtype is a supertype of the reference type.
     *  
     * @param subtype type to evaluate
     * @param referenceType initial tracing reference to detect the super type
     */
    public static bool IsSuperType(INamedTypeSymbol subType, INamedTypeSymbol referenceType)
    {

        if (subType == null || referenceType == null || referenceType.SpecialType.ToString() == "System_Object")
            return false;

        if (subType.Equals(referenceType.BaseType))
            return true;

        return IsSuperType(subType, referenceType.BaseType);

    }

    public static SyntaxNode FindParentTry(SyntaxNode node)
    {
        //if reach method, constructor and class stop because went too far
        if (node.IsKind(SyntaxKind.MethodDeclaration) || 
            node.IsKind(SyntaxKind.ConstructorDeclaration) || 
            node.IsKind(SyntaxKind.ClassDeclaration))
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

    public static int FindKind(string exceptionTypeName, Compilation compilation)
    {
        INamedTypeSymbol exceptionType = compilation.GetTypeByMetadataName(exceptionTypeName);

        return FindKind(exceptionType, compilation);        
    }

    public static int FindKind(INamedTypeSymbol exceptionType, Compilation compilation)
    {
        if (exceptionType == null) { return -2; }
        if (exceptionType.Equals(compilation.GetTypeByMetadataName("System.SystemException")) || 
            exceptionType.Equals(compilation.GetTypeByMetadataName("System.ApplicationException")))
        {
            return 0;
        }
        else if (exceptionType.Equals(compilation.GetTypeByMetadataName("System.Exception")))
        {
            return 1;
        }
        else if (exceptionType.Equals(compilation.GetTypeByMetadataName("System.Object")) ||
           (exceptionType.SpecialType.ToString() != "None" &&
                (exceptionType.SpecialType.Equals(compilation.GetTypeByMetadataName("System.Object").SpecialType))
             ))
        {
            return -1;
        }
        else
        {
            return FindKind(exceptionType.BaseType, compilation);
        }
    }
}