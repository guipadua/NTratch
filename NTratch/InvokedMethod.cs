using System;
using System.Collections.Generic;

namespace NTratch
{
    public class InvokedMethod
    {
        private string Key;
        private HashSet<ExceptionFlow> ExceptionFlowSet  = new HashSet<ExceptionFlow>();
        private int ChildrenMaxLevel = 0;
        private bool Binded = false;
        private bool Declared = false;
        private bool ExternalDocPresent = false;

        public InvokedMethod(string key, bool binded) : base()
        {
            Key = key;
            Binded = binded;
        }

        public string getKey()
        {
            return Key;
        }

        public void setKey(string key)
        {
            Key = key;
        }

        public HashSet<ExceptionFlow> getExceptionFlowSet()
        {
            return ExceptionFlowSet;
        }

        public HashSet<ExceptionFlow> getExceptionFlowSetByType()
        {
            HashSet<ExceptionFlow> combinedExceptionsSet = new HashSet<ExceptionFlow>();
            Dictionary<string, ExceptionFlow> combinedExceptionsTemp = new Dictionary<string, ExceptionFlow>();

            foreach (ExceptionFlow exception in ExceptionFlowSet)
            {
                if (!combinedExceptionsTemp.ContainsKey(exception.getThrownTypeName()))
                    combinedExceptionsTemp.Add(exception.getThrownTypeName(), exception);
                else
                {
                    ExceptionFlow combinedException = combinedExceptionsTemp[exception.getThrownTypeName()];

                    //take original method info from the one that is identified as throw
                    if (exception.getIsThrow())
                        combinedException.setOriginalMethodBindingKey(exception.getOriginalMethodBindingKey());

                    //bool flags - do an OR to be true if any is true
                    combinedException.setIsDocSemantic(combinedException.getIsDocSemantic() || exception.getIsDocSemantic());
                    combinedException.setIsDocSyntax(combinedException.getIsDocSyntax() || exception.getIsDocSyntax());
                    combinedException.setIsThrow(combinedException.getIsThrow() || exception.getIsThrow());

                    //take deepest level found
                    if (exception.getLevelFound() > combinedException.getLevelFound())
                        combinedException.setLevelFound(exception.getLevelFound());

                    //take type that is not null
                    if (combinedException.getThrownType() == null && exception.getThrownType() != null)
                        combinedException.setThrownType(exception.getThrownType());
                }
            }

            combinedExceptionsSet.UnionWith(combinedExceptionsTemp.Values);

            return combinedExceptionsSet;
        }

        public void setExceptionFlowSet(HashSet<ExceptionFlow> exceptionFlowSet)
        {
            ExceptionFlowSet = exceptionFlowSet;
        }

        public int getChildrenMaxLevel()
        {
            return ChildrenMaxLevel;
        }

        public void setChildrenMaxLevel(int childrenMaxLevel)
        {
            ChildrenMaxLevel = childrenMaxLevel;
        }

        public bool isBinded()
        {
            return Binded;
        }
        
        public void setBinded(bool binded)
        {
            Binded = binded;
        }

        public void setDeclared(bool declared)
        {
            Declared = declared;
        }

        public bool isDeclared()
        {
            return Declared;
        }

        public bool isExternalDocPresent()
        {
            return ExternalDocPresent;
        }

        public void setExternalDocPresent(bool externalDocPresent)
        {
            ExternalDocPresent = externalDocPresent;
        }
    }    
}