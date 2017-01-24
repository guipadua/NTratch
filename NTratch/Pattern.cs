using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Microsoft.CodeAnalysis;

namespace NTratch
{
    class TreeStatistics
    {
        public Dictionary<string, int> CodeStats;
        public List<CatchBlock> CatchBlockList;
        //public List<APICall> APICallList;
        public List<PossibleExceptionsBlock> PossibleExceptionsBlockList;

        public TreeStatistics()
        {
            CodeStats = new Dictionary<string, int>();

            CodeStats.Add("NumFiles", 0);

            CodeStats.Add("NumLOC", 0);
            CodeStats.Add("NumCatchBlock", 0);

            CodeStats.Add("NumBinded", 0);
            CodeStats.Add("NumNoDeclaration", 0);
            CodeStats.Add("NumMethodsNotBinded", 0);
            CodeStats.Add("NumLoggedCatchBlock", 0);
            CodeStats.Add("NumDistinctExceptionTypeCatch", 0);
            CodeStats.Add("NumRecoveredBinding", 0);

            //From process compilation
            CodeStats.Add("NumPossibleExceptionBlock", 0);
            
                   //From collection of possible exceptions blocks per AST - roll-up
            CodeStats.Add("NumDistinctPossibleExceptions", 0);
            
                    //From CodeAnalyzer - global method storages:
            CodeStats.Add("NumDeclaredMethods", 0);
            CodeStats.Add("NumInvokedMethods", 0);
            CodeStats.Add("NumInvokedMethodsBinded", 0);
            CodeStats.Add("NumInvokedMethodsDeclared", 0);
            CodeStats.Add("NumInvokedMethodsExtDocPresent", 0);

            //From process compilation
            CodeStats.Add("NumThrowsBlock", 0);
            
            //From collection of possible exceptions blocks per AST - roll-up
            CodeStats.Add("NumDistinctExceptionTypeThrows", 0);

            //CodeStats.Add("NumLoggedLOC", 0);
            //CodeStats.Add("NumCall", 0);
            //CodeStats.Add("NumLogging", 0);
            //CodeStats.Add("NumClass", 0);
            //CodeStats.Add("NumLoggedClass", 0);
            //CodeStats.Add("NumMethod", 0);
            //CodeStats.Add("NumExceptions", 0);
            //CodeStats.Add("NumLoggedMethod", 0);
            //CodeStats.Add("NumLoggedFile", 0);
            //CodeStats.Add("NumCallType", 0);
            //CodeStats.Add("NumAPICall", 0);
            //CodeStats.Add("NumLoggedAPICall", 0);
        }

        public static void Add<T>(ref Dictionary<T, int> dic1, Dictionary<T, int> dic2)
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

    }

    class CodeStatistics : TreeStatistics
    {
        public List<Tuple<SyntaxTree, TreeStatistics>> TreeStats;
        public CatchDic CatchBlocks;
        //public CallDic APICalls;
        public PossibleExceptionsDic PossibleExceptionsBlocks;

        public CodeStatistics(List<Tuple<SyntaxTree, TreeStatistics>> codeStatsList)
        {
            TreeStats = codeStatsList;
            CatchBlocks = new CatchDic();
            //APICalls = new CallDic();
            PossibleExceptionsBlocks = new PossibleExceptionsDic();
            CodeStats = new Dictionary<string, int>();
            foreach (var treetuple in codeStatsList)
            {
                if (treetuple == null) continue;
                if (treetuple.Item2.CatchBlockList != null)
                {
                    CatchBlocks.Add(treetuple.Item2.CatchBlockList);
                }
                //if (treetuple.Item2.APICallList != null)
                //{
                //    APICalls.Add(treetuple.Item2.APICallList);
                //}
                if (treetuple.Item2.PossibleExceptionsBlockList != null)
                {
                    PossibleExceptionsBlocks.Add(treetuple.Item2.PossibleExceptionsBlockList);
                }
                if (treetuple.Item2.CodeStats != null)
                {
                    CodeAnalyzer.MergeDic<String>(ref CodeStats, treetuple.Item2.CodeStats);
                }
            }

            if (CatchBlocks.Count > 0)
            {
                CodeStats["NumBinded"] = CatchBlocks.NumBinded;
                CodeStats["NumMethodsNotBinded"] = CatchBlocks.NumMethodsNotBinded;
                CodeStats["NumLoggedCatchBlock"] = CatchBlocks.NumLogged;
                CodeStats["NumDistinctExceptionTypeCatch"] = CatchBlocks.Count;
                CodeStats["NumRecoveredBinding"] = CatchBlocks.NumRecoveredBinding;
                CodeStats["NumNoDeclaration"] = CatchBlocks.NumNoDeclaration;
            }

            if (PossibleExceptionsBlocks.Count > 0)
            {
                CodeStats["NumDistinctPossibleExceptions"] = PossibleExceptionsBlocks.Count;
            }

            //CodeStats["NumCallType"] = APICalls.Count;
            //CodeStats["NumAPICall"] = APICalls.NumAPICall;
            //CodeStats["NumLoggedAPICall"] = APICalls.NumLogged;
        }

        public void PrintSatistics()
        {
            string header = "";
            string content = "";

            foreach (var stat in CodeStats.Keys)
            {
                header += stat + "\t";
                content += CodeStats[stat] + "\t";

                //Logger.Log(stat + ": " + CodeStats[stat]);
            }

            Logger.Log(header);
            Logger.Log(content);

            if (CatchBlocks.Count > 0)
            {
                CatchBlocks.PrintToFileCSV();
            }
            //APICalls.PrintToFile();
            if (PossibleExceptionsBlocks.Count > 0)
            {
                PossibleExceptionsBlocks.PrintToFileCSV();
            }
        }
    }

    public class CommonFeature
    {
        public Dictionary<string, int> OperationFeatures;
        //public Dictionary<String, int> TextFeatures; //TextFeatures is only on the API call
        public string FilePath;
        public int StartLine;
        public string ExceptionType;
        public string ParentType;
        public string ParentMethod;

        public Dictionary<string, string> MetaInfo;

        public const string Splitter = "\t";

        public CommonFeature()
        {
            OperationFeatures = new Dictionary<string, int>();
            MetaInfo = new Dictionary<string, string>();

            OperationFeatures.Add("TryStartLine", 0);
            OperationFeatures.Add("TryEndLine", 0);
            OperationFeatures.Add("TryLOC", 0);
            OperationFeatures.Add("CatchStartLine", 0);
            OperationFeatures.Add("CatchEndLine", 0);
            OperationFeatures.Add("CatchLOC", 0);
            OperationFeatures.Add("CatchStart", 0);
            OperationFeatures.Add("CatchLength", 0);
            OperationFeatures.Add("MethodStartLine", 0);
            OperationFeatures.Add("MethodEndLine", 0);
            OperationFeatures.Add("MethodLOC", 0);

            MetaInfo.Add("FilePath", "'-filepath");
            MetaInfo.Add("StartLine", "'-startLine");
            MetaInfo.Add("ExceptionType", "'-exceptiontype");
            MetaInfo.Add("ParentType", "'-parenttype");
            MetaInfo.Add("ParentMethod", "'-parentmethod");

            MetaInfo.Add("TryLine", "'-tryline");
        }

        public string PrintFeatures()
        {
            string features = null;
            foreach (var key in OperationFeatures.Keys)
            {
                features += (key + ":" + OperationFeatures[key] + Splitter);
            }
            features += (ExceptionType + Splitter);
            features += (ParentMethod + Splitter);
            features += (ParentType + Splitter);
            features += (FilePath + Splitter);
            features += (StartLine + Splitter);

            return features;
        }

        public string PrintMetaInfo()
        {
            string metaInfo = null;
            foreach (var key in MetaInfo.Keys)
            {
                metaInfo += (IOFile.DeleteSpace(MetaInfo[key]) + Splitter);
            }
            return metaInfo;
        }

        public virtual string PrintFeaturesCSV()
        {
            string csv = "";

            foreach (var key in OperationFeatures.Keys)
            {
                csv += (OperationFeatures[key] + ",");
            }
            csv += (CleanQuotesCSV(ExceptionType) + ",");
            csv += (CleanQuotesCSV(ParentMethod) + ",");
            csv += (CleanQuotesCSV(ParentType) + ",");
            csv += (CleanQuotesCSV(FilePath) + ",");
            csv += (StartLine);

            return csv;
        }

        public string PrintMetaInfoCSV()
        {
            String csv = "";

            foreach (var key in MetaInfo.Keys)
            {
                csv += (CleanQuotesCSV(MetaInfo[key]) + ",");
            }
            //        csv += (ExceptionType + ",");
            //        csv += (ParentMethod + ",");
            //        csv += (ParentType + ",");
            //        csv += (FilePath + ",");
            //        csv += (StartLine);

            return csv;
        }

        public static string CleanQuotesCSV (string csv)
        {
            return '"' + csv.Replace(("" + '"'), "") + '"';
        }

    }

    class CatchBlock : CommonFeature
    {
        public static List<string> MetaKeys;
        public static List<string> OpFeaturesKeys;
        
        public CatchBlock() : base() 
        {
            //Binding info and binding based:
            OperationFeatures.Add("Binded", -9);
            OperationFeatures.Add("RecoveredBinding", -9);
            OperationFeatures.Add("Kind", -9);
            OperationFeatures.Add("Checked", 0);

            //Try info
            MetaInfo.Add("TryBlock", "'-tryblock");
            OperationFeatures.Add("ParentNodeType", 0);
            MetaInfo.Add("ParentNodeType", "'-parentnodetype");

            //Try Visitor items:
            OperationFeatures.Add("RecoverFlag", 0);
            MetaInfo.Add("RecoverFlag", "'-recoverflag");
            OperationFeatures.Add("InnerCatch", 0);
            OperationFeatures.Add("ParentTryStartLine", 0);

            //Method invocation Visitor on the Catch block:
            OperationFeatures.Add("Logged", 0);
            OperationFeatures.Add("MultiLog", 0);
            OperationFeatures.Add("Abort", 0);
            OperationFeatures.Add("Default", 0);
            OperationFeatures.Add("GetCause", 0);
            OperationFeatures.Add("OtherInvocation", 0);

            MetaInfo.Add("Logged", "'-logged");
            MetaInfo.Add("Abort", "'-abort");
            MetaInfo.Add("Default", "'-default");
            MetaInfo.Add("GetCause", "'-getcause");
            MetaInfo.Add("OtherInvocation", "'-otherinvocation");

            //Throw visitor
            OperationFeatures.Add("NumThrown", 0);
            MetaInfo.Add("Thrown", "'-thrown");
            OperationFeatures.Add("NumThrowNew", 0);
            OperationFeatures.Add("NumThrowWrapCurrentException", 0);

            //Other specific visitors:
            OperationFeatures.Add("Return", 0);
            OperationFeatures.Add("Continue", 0);
            MetaInfo.Add("Return", "'-return");
            MetaInfo.Add("Continue", "'-continue");

            //Some catch block info
            OperationFeatures.Add("EmptyBlock", 0);
            OperationFeatures.Add("CatchException", -9);

            //Finally block items, if existing
            MetaInfo.Add("FinallyBlock", "'-finallyblock");
            OperationFeatures.Add("FinallyThrowing", 0);

            //Binding based info:
            //MetaInfo.Add("TryMethods", "'-trymethods");
            MetaInfo.Add("TryMethodsAndExceptions", "'-trymethodsandexceptions");

            OperationFeatures.Add("NumDistinctMethods", 0);
            MetaInfo.Add("TryMethodsBinded", "'-trymethodsbinded");
            OperationFeatures.Add("NumDistinctMethodsNotBinded", 0);

            MetaInfo.Add("DistinctExceptions", "'DistinctExceptions");
            OperationFeatures.Add("NumDistinctExceptions", 0);

            OperationFeatures.Add("NumSpecificHandler", 0);
            OperationFeatures.Add("NumSubsumptionHandler", 0);
            OperationFeatures.Add("NumSupersumptionHandler", 0);
            OperationFeatures.Add("NumOtherHandler", 0);

            OperationFeatures.Add("MaxLevel", 0);
            OperationFeatures.Add("NumIsDocSemantic", 0);
            OperationFeatures.Add("NumIsDocSyntax", 0);
            OperationFeatures.Add("NumIsThrow", 0);

            //Comments info - not in the Catch Visitor
            OperationFeatures.Add("ToDo", 0);
            MetaInfo.Add("CatchBlock", "'-catchblock");


            /* // Not in Use right now:
            OperationFeatures.Add("SetLogicFlag", 0);
            MetaInfo.Add("SetLogicFlag", "'-setlogicflag");
            OperationFeatures.Add("OtherOperation", 0);
            MetaInfo.Add("OtherOperation", "'-otheroperation");
            */

            MetaKeys = MetaInfo.Keys.ToList();
            OpFeaturesKeys = OperationFeatures.Keys.ToList();
        }        
    }

    class CatchList : List<CatchBlock>
    {
        public int NumLogged = 0;
        public int NumThrown = 0;
        public int NumLoggedAndThrown = 0;
        public int NumLoggedNotThrown = 0;

        public int NumBinded = 0;
        public int NumNoDeclaration = 0;
        public int NumRecoveredBinding = 0;
        public int NumMethodsNotBinded = 0;
    }

    class CatchDic : Dictionary<string, CatchList>
    {
        public int NumCatch = 0;
        public int NumBinded = 0;
        public int NumNoDeclaration = 0;
        public int NumRecoveredBinding = 0;
        public int NumMethodsNotBinded = 0;
        public int NumLogged = 0;
        public int NumThrown = 0;
        public int NumLoggedAndThrown = 0;
        public int NumLoggedNotThrown = 0;

        public void Add(List<CatchBlock> catchList)
        {
            foreach (var catchBlock in catchList)
            {
                if (catchBlock == null) continue;
                NumCatch++;
                string exception = catchBlock.ExceptionType;
                if (this.ContainsKey(exception))
                {
                    this[exception].Add(catchBlock);
                }
                else
                {
                    //Create a new list for this type.
                    this.Add(exception, new CatchList());
                    this[exception].Add(catchBlock);
                }

                //Update Statistics
                if (catchBlock.OperationFeatures["Logged"] == 1)
                {
                    this[exception].NumLogged++;
                    NumLogged++;
                    if (catchBlock.OperationFeatures["NumThrown"] > 1)
                    {
                        this[exception].NumLoggedAndThrown++;
                        NumLoggedAndThrown++;
                    }
                    else
                    {
                        this[exception].NumLoggedNotThrown++;
                        NumLoggedNotThrown++;
                    }
                }
                if (catchBlock.OperationFeatures["NumThrown"] > 1)
                {
                    this[exception].NumThrown++;
                    NumThrown++;
                }
                if (catchBlock.OperationFeatures["Binded"] == 1)
                {
                    this[exception].NumBinded++;
                    NumBinded++;
                }
                if (catchBlock.ExceptionType == "!NO_EXCEPTION_DECLARED!")
                {
                    this[exception].NumNoDeclaration++;
                    NumNoDeclaration++;
                }
                if (catchBlock.OperationFeatures["RecoveredBinding"] == 1)
                {
                    this[exception].NumRecoveredBinding++;
                    NumRecoveredBinding++;
                }
                if (catchBlock.OperationFeatures["NumDistinctMethodsNotBinded"] > 0)
                {
                    this[exception].NumMethodsNotBinded++;
                    NumMethodsNotBinded++;
                }
            }
        }
        public void PrintToFileCSV()
        {
            Logger.Log("Writing CatchBlock features into file...");
            StreamWriter csvSW = new StreamWriter(IOFile.CompleteFileNameOutput("CatchBlock.csv"));
            StreamWriter metaCSVSW = new StreamWriter(IOFile.CompleteFileNameOutput("CatchBlock_Meta.csv"));

            int catchId = 0;
            string metaKey = "";

            foreach (var meta in CatchBlock.MetaKeys)
            {
                metaKey += (meta + ",");
            }

            string OpFeaturesKey = "";

            foreach (var OpFeature in CatchBlock.OpFeaturesKeys)
            {
                OpFeaturesKey += (OpFeature + ",");
            }

            csvSW.WriteLine("id," + OpFeaturesKey + "ExceptionType,ParentMethod,ParentType,FilePath,StartLine");
            metaCSVSW.WriteLine("id," + metaKey);
            
            foreach (string exception in this.Keys)
            {
                CatchList catchList = this[exception];
                foreach (var catchblock in catchList)
                {
                    catchId++;
                    csvSW.WriteLine(catchId + "," + catchblock.PrintFeaturesCSV());
                    metaCSVSW.WriteLine(catchId + "," + catchblock.PrintMetaInfoCSV());
                }
                csvSW.Flush();
                metaCSVSW.Flush();
            }

            csvSW.Close();
            metaCSVSW.Close();
            Logger.Log("Writing done.");
        }
    }
    public class PossibleExceptionsBlock : CommonFeature
    {
        public string CaughtType;
        public string InvokedMethod;
        public int InvokedMethodLine;
        public string DeclaringMethod;

        public static List<string> MetaKeys;
        public static List<string> OpFeaturesKeys;

        public PossibleExceptionsBlock() : base()
        {
            OperationFeatures.Add("Kind", 0);
            OperationFeatures.Add("IsDocSemantic", 0);
            OperationFeatures.Add("IsDocSyntax", 0);
            OperationFeatures.Add("IsThrow", 0);
            OperationFeatures.Add("HandlerTypeCode", 0);
            OperationFeatures.Add("LevelFound", 0);

            //MetaInfo.Add("PossibleExceptionsBlock", "'-PossibleExceptionsBlock");

            MetaKeys = MetaInfo.Keys.ToList();
            OpFeaturesKeys = OperationFeatures.Keys.ToList();
        }

        public override string PrintFeaturesCSV()
        {
            string csv = "";

            foreach (var key in OperationFeatures.Keys)
            {
                csv += (OperationFeatures[key] + ",");
            }

            csv += (CleanQuotesCSV(ExceptionType) + ",");
            csv += (CleanQuotesCSV(CaughtType) + ",");
            csv += (CleanQuotesCSV(DeclaringMethod) + ",");
            csv += (CleanQuotesCSV(InvokedMethod) + ",");
            csv += (InvokedMethodLine + ",");
            csv += (CleanQuotesCSV(FilePath) + ",");
            csv += (StartLine);

            return csv;
        }
    }
    public class PossibleExceptionsList : List<PossibleExceptionsBlock>
    {

    }

    public class PossibleExceptionsDic : Dictionary<string, PossibleExceptionsList>
    {
        public int NumPossibleExceptions = 0;

        public void Add(List<PossibleExceptionsBlock> possibleExceptionsList)
        {
            foreach (PossibleExceptionsBlock possibleExceptionsBlock in possibleExceptionsList)
            {
                if (possibleExceptionsBlock == null) continue;
                NumPossibleExceptions++;
                string exception = possibleExceptionsBlock.ExceptionType;
                if (this.ContainsKey(exception))
                {
                    this[exception].Add(possibleExceptionsBlock);
                }
                else
                {
                    //Create a new list for this type.
                    this.Add(exception, new PossibleExceptionsList());
                    this[exception].Add(possibleExceptionsBlock);
                }
            }
        }

        public void PrintToFileCSV()
        {

            Logger.Log("Writing CatchBlock features into file...");
            StreamWriter csvSW = new StreamWriter(IOFile.CompleteFileNameOutput("PossibleExceptionsBlock.csv"));
            StreamWriter metaCSVSW = new StreamWriter(IOFile.CompleteFileNameOutput("PossibleExceptionsBlock_Meta.csv"));

            int catchId = 0;
            string metaKey = "";

            foreach (var meta in PossibleExceptionsBlock.MetaKeys)
            {
                metaKey += (meta + ",");
            }

            string OpFeaturesKey = "";

            foreach (var OpFeature in PossibleExceptionsBlock.OpFeaturesKeys)
            {
                OpFeaturesKey += (OpFeature + ",");
            }

            csvSW.WriteLine("id," + OpFeaturesKey + "ThrownType,CaughtType,DeclaringMethod,InvokedMethod,InvokedMethodLine,CatchFilePath,CatchStartLine");
            metaCSVSW.WriteLine("id," + metaKey);

            foreach (string exception in this.Keys)
            {
                PossibleExceptionsList possibleExceptionsList = this[exception];
                foreach (var possibleExceptionsBlock in possibleExceptionsList)
                {
                    catchId++;
                    csvSW.WriteLine(catchId + "," + possibleExceptionsBlock.PrintFeaturesCSV());
                    metaCSVSW.WriteLine(catchId + "," + possibleExceptionsBlock.PrintMetaInfoCSV());
                }
                csvSW.Flush();
                metaCSVSW.Flush();
            }

            csvSW.Close();
            metaCSVSW.Close();
            Logger.Log("Writing done.");
        }
    }
}

