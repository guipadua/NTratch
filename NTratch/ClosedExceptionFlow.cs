using Microsoft.CodeAnalysis;
using System;

namespace NTratch
{
    public class ClosedExceptionFlow : ExceptionFlow
    {
        //Catch info
        private string caughtTypeName = "";
        private INamedTypeSymbol caughtType = null;
        private sbyte handlerTypeCode = -9;

        private string catchFilePath = "";
        private int catchStartLine = 0;
        private string declaringNodeKey = "";

        private string invokedMethodKey = "";
        private int invokedMethodLine = 0;


        public ClosedExceptionFlow(string exceptionName, string originType, string originalMethodBindingKey, byte levelFound) : 
            base(exceptionName, originType, originalMethodBindingKey, levelFound)
        { }
       
        public ClosedExceptionFlow(INamedTypeSymbol thrownType, string originType, string originalMethodBindingKey, byte levelFound) :
            base(thrownType, originType, originalMethodBindingKey, levelFound)
        { }

        public ClosedExceptionFlow(ExceptionFlow exception) :
            base(exception)
        { }

        public INamedTypeSymbol getCaughtType()
        {
            return caughtType;
        }

        public void setCaughtType(INamedTypeSymbol caughtType)
        {
            this.caughtType = caughtType;
            if (caughtType != null)
                setCaughtTypeName(caughtType.ToString());
        }

        public string getDeclaringNodeKey()
        {
            return declaringNodeKey;
        }

        public void setDeclaringNodeKey(string declaringNodeKey)
        {
            this.declaringNodeKey = declaringNodeKey;
        }

        public string getInvokedMethodKey()
        {
            return invokedMethodKey;
        }

        public void setInvokedMethodKey(string invokedMethodKey)
        {
            this.invokedMethodKey = invokedMethodKey;
        }

        public int getInvokedMethodLine()
        {
            return invokedMethodLine;
        }

        public void setInvokedMethodLine(int invokedMethodLine)
        {
            this.invokedMethodLine = invokedMethodLine;
        }

        public string getCatchFilePath()
        {
            return catchFilePath;
        }

        public void setCatchFilePath(string catchFilePath)
        {
            this.catchFilePath = catchFilePath;
        }

        public int getCatchStartLine()
        {
            return catchStartLine;
        }

        public void setCatchStartLine(int catchStartLine)
        {
            this.catchStartLine = catchStartLine;
        }

        public sbyte getHandlerTypeCode()
        {
            return handlerTypeCode;
        }

        //should be calculated using calculateHandlerTypeCode
        public void setHandlerTypeCode(sbyte handlerTypeCode)
        {
            this.handlerTypeCode = handlerTypeCode;
        }

        public string getCaughtTypeName()
        {
            return caughtTypeName;
        }

        public void setCaughtTypeName(string caughtTypeName)
        {
            this.caughtTypeName = caughtTypeName;
        }

        //TODO: method that calculate handlerType even if no binding
        public static sbyte calculateHandlerTypeCode(INamedTypeSymbol caughtType, INamedTypeSymbol thrownType)
        {
            sbyte handlerTypeCode = -9;

            if (caughtType != null)
            {
                if (thrownType != null)
                {
                    //In case is the same type, it's specific handler type - code: 0
                    //In case the caught type is equal a super class of the possible thrown type, it's a subsumption - code: 1
                    //In case the possible thrown type is equal a super class of the caught type, it's a supersumption - code: 2
                    //In case it's none of the above - most likely tree of unrelated exceptions: code: 3
                    if (caughtType.Equals(thrownType) ||
                            (caughtType.BaseType != null && thrownType.BaseType != null && 
                                caughtType.BaseType.Equals(thrownType.BaseType) && 
                                caughtType.MetadataName.Equals(thrownType.MetadataName)
                             )
                        )
                    {
                        handlerTypeCode = 0;
                    }
                    else if (ASTUtilities.IsSuperType(caughtType, thrownType))
                    {
                        handlerTypeCode = 1;
                    }
                    else if (ASTUtilities.IsSuperType(thrownType, caughtType))
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

        public void closeExceptionFlow(INamedTypeSymbol caughtType, INamedTypeSymbol thrownType,
                string catchFilePath, int catchStartLine, string invokedMethodKey, int invokedMethodLine)
        {
            if (getCaughtTypeName() != null && getCaughtTypeName() != "") { return; }

            sbyte handlerTypeCodeToEvaluate = calculateHandlerTypeCode(caughtType, thrownType);

            setHandlerTypeCode(handlerTypeCodeToEvaluate);
            setCaughtType(caughtType);
            setCatchFilePath(catchFilePath);
            setCatchStartLine(catchStartLine);
            setInvokedMethodKey(invokedMethodKey);
            setInvokedMethodLine(invokedMethodLine);            
        }

        public void closeExceptionFlow(INamedTypeSymbol caughtType, INamedTypeSymbol thrownType, string declaringNodeKey)
        {
            if ((getDeclaringNodeKey() != null && getDeclaringNodeKey() != "")) { return; }

            sbyte handlerTypeCodeToEvaluate = calculateHandlerTypeCode(caughtType, thrownType);

            setHandlerTypeCode(handlerTypeCodeToEvaluate);
            setCaughtType(caughtType);
            setDeclaringNodeKey(declaringNodeKey);            
        }

        public static bool IsCloseableExceptionFlow(INamedTypeSymbol caughtType, INamedTypeSymbol thrownType)
        {
            sbyte handlerTypeCodeToEvaluate = calculateHandlerTypeCode(caughtType, thrownType);

            //0: SPECIFIC, 1: SUBSUMPTION - the only two possible ways to really catch and could close the flow
            if ((handlerTypeCodeToEvaluate == 0 || handlerTypeCodeToEvaluate == 1))
                return true;
            else
                return false;
        }
    }
}