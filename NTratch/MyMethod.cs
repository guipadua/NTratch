using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NTratch
{
    class MyMethod
    {
        public string Name { get; set; }
        public BaseMethodDeclarationSyntax Declaration { get; set; }
        public Dictionary<string, Dictionary<string, sbyte>> Exceptions { get; set; } = new Dictionary<string, Dictionary<string, sbyte>>();
        public int ChildrenMaxLevel { get; set; } = 0;
        public bool IsVisited { get; set; } = false;
        
        public MyMethod(string Name, BaseMethodDeclarationSyntax Declaration)
        {
            this.Name = Name;
            this.Declaration = Declaration;            
        }
        
    }
}
