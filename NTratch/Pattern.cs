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

        public TreeStatistics()
        {
            CodeStats = new Dictionary<string, int>();
            CodeStats.Add("NumLOC", 0);
            CodeStats.Add("NumCatchBlock", 0);

            CodeStats.Add("NumLoggedCatchBlock", 0);
            CodeStats.Add("NumExceptionTypeCatch", 0);

            CodeStats.Add("NumBinded", 0);
            CodeStats.Add("NumRecoveredBinding", 0);
            CodeStats.Add("NumMethodsNotBinded", 0);

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

        public CodeStatistics(List<Tuple<SyntaxTree, TreeStatistics>> codeStatsList)
        {
            TreeStats = codeStatsList;
            CatchBlocks = new CatchDic();
            //APICalls = new CallDic();
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
                if (treetuple.Item2.CodeStats != null) 
                {
                    CodeAnalyzer.MergeDic<String>(ref CodeStats, treetuple.Item2.CodeStats);
                }
            }
            CodeStats["NumExceptionTypeCatch"] = CatchBlocks.Count;
            CodeStats["NumLoggedCatchBlock"] = CatchBlocks.NumLogged;

            CodeStats["NumBinded"] = CatchBlocks.NumBinded;
            CodeStats["NumRecoveredBinding"] = CatchBlocks.NumRecoveredBinding;
            CodeStats["NumMethodsNotBinded"] = CatchBlocks.NumMethodsNotBinded;


            //CodeStats["NumCallType"] = APICalls.Count;
            //CodeStats["NumAPICall"] = APICalls.NumAPICall;
            //CodeStats["NumLoggedAPICall"] = APICalls.NumLogged;
        }

        public void PrintSatistics()
        {           
            foreach (var stat in CodeStats.Keys)
            {
                Logger.Log(stat + ": " + CodeStats[stat]);
            }
            CatchBlocks.PrintToFile();
            //APICalls.PrintToFile();
        }
    }

    class CommonFeature
    {
        public Dictionary<string, int> OperationFeatures;
        //public Dictionary<String, int> TextFeatures; //TextFeatures is only on the API call
        public string FilePath;
        public string ExceptionType;
        public string ParentType;
        public string ParentMethod;

        public Dictionary<string, string> MetaInfo;

        public const string Splitter = "\t";

        public CommonFeature()
        {
            OperationFeatures = new Dictionary<string, int>();
            MetaInfo = new Dictionary<string, string>();

            OperationFeatures.Add("TryLine", 0);
            OperationFeatures.Add("TryLOC", 0);
            OperationFeatures.Add("CatchLine", 0);
            OperationFeatures.Add("CatchLOC", 0);
            OperationFeatures.Add("MethodLine", 0);
            OperationFeatures.Add("MethodLOC", 0);

            MetaInfo.Add("FilePath", "'-filepath");
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

            return features;
        }

        public string PrintCSV()
        {
            string csv = null;

            foreach (var key in OperationFeatures.Keys)
            {
                csv += (OperationFeatures[key] + ",");
            }
            csv += (ExceptionType + ",");
            csv += (ParentMethod + ",");
            csv += (ParentType + ",");
            csv += (FilePath);

            return csv;
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
            OperationFeatures.Add("NumMethodsNotBinded", 0);

            MetaInfo.Add("DistinctExceptions", "'DistinctExceptions");
            OperationFeatures.Add("NumDistinctExceptions", 0);

            OperationFeatures.Add("NumSpecificHandler", 0);
            OperationFeatures.Add("NumSubsumptionHandler", 0);
            OperationFeatures.Add("NumSupersumptionHandler", 0);
            OperationFeatures.Add("NumOtherHandler", 0);

            OperationFeatures.Add("MaxLevel", 0);
            OperationFeatures.Add("NumIsXMLSemantic", 0);
            OperationFeatures.Add("NumIsXMLSyntax", 0);
            //OperationFeatures.Add("IsLoop", 0);
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
        public int NumRecoveredBinding = 0;
        public int NumMethodsNotBinded = 0;
    }

    class CatchDic : Dictionary<string, CatchList>
    {
        public int NumCatch = 0;
        public int NumBinded = 0;
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
                if (catchBlock.OperationFeatures["RecoveredBinding"] == 1)
                {
                    this[exception].NumRecoveredBinding++;
                    NumRecoveredBinding++;
                }
                if (catchBlock.OperationFeatures["NumMethodsNotBinded"] > 0)
                {
                    this[exception].NumMethodsNotBinded++;
                    NumMethodsNotBinded++;
                }
            }
        }

        public void PrintToFile()
        {
            Logger.Log("Writing CatchBlock features into file...");
            StreamWriter sw = new StreamWriter(IOFile.CompleteFileNameOutput("CatchBlock.txt"));
            StreamWriter metaSW = new StreamWriter(IOFile.CompleteFileNameOutput("CatchBlock_Meta.txt"));
            StreamWriter csvSW = new StreamWriter(IOFile.CompleteFileNameOutput("CatchBlock.csv"));

            int catchId = 0;

            string metaKey = CatchBlock.Splitter;
            foreach (var meta in CatchBlock.MetaKeys)
            {
                metaKey += (meta + CatchBlock.Splitter);
            }

            string OpFeaturesKey = "";
            foreach (var OpFeature in CatchBlock.OpFeaturesKeys)
            {
                OpFeaturesKey += (OpFeature + ",");
            }

            csvSW.WriteLine("ID," + OpFeaturesKey + "ExceptionType,ParentMethod,ParentType,FilePath");
            metaSW.WriteLine(metaKey);
            metaSW.WriteLine("'--------------------------------------------------------");
            metaSW.WriteLine("NumExceptionType: {0}, NumCatchBlock: {1}, NumLogged: {2}, "
                    + "NumThrown: {3}, NumLoggedAndThrown: {4}, NumLoggedNotThrown: {5}.",
                    this.Count,
                    NumCatch,
                    NumLogged,
                    NumThrown,
                    NumLoggedAndThrown,
                    NumLoggedNotThrown);
            metaSW.WriteLine();

            foreach (string exception in this.Keys)
            {
                metaSW.WriteLine("'--------------------------------------------------------");
                CatchList catchList = this[exception];
                metaSW.WriteLine("Exception Type [{0}]: NumCatchBlock: {1}, NumLogged: {2}, "
                        + "NumThrown: {3}, NumLoggedAndThrown: {4}, NumLoggedNotThrown: {5}.",
                        exception,
                        catchList.Count,
                        catchList.NumLogged,
                        catchList.NumThrown,
                        catchList.NumLoggedAndThrown,
                        catchList.NumLoggedNotThrown
                        );
                foreach (var catchblock in catchList)
                {
                    catchId++;
                    sw.WriteLine("ID:" + catchId + CatchBlock.Splitter + catchblock.PrintFeatures());
                    metaSW.WriteLine("ID:" + catchId + CatchBlock.Splitter + catchblock.PrintMetaInfo());
                    csvSW.WriteLine(catchId + "," + catchblock.PrintCSV());
                }
                metaSW.WriteLine();
                metaSW.WriteLine();
                csvSW.WriteLine();
                sw.Flush();
                metaSW.Flush();
                csvSW.Flush();
            }

            //Print summary
            metaSW.WriteLine("'------------------------ Summary -------------------------");
            metaSW.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}",
                    "Exception Type",
                    "NumCatch",
                    "NumLogged",
                    "NumThrown",
                    "NumLoggedAndThrown",
                    "NumLoggedNotThrown");

            foreach (string exception in this.Keys)
            {
                var catchList = this[exception];
                metaSW.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}",
                        exception,
                        catchList.Count,
                        catchList.NumLogged,
                        catchList.NumThrown,
                        catchList.NumLoggedAndThrown,
                        catchList.NumLoggedNotThrown
                        );
            }
            sw.Close();
            metaSW.Close();
            csvSW.Close();
            Logger.Log("Writing done.");
        }
    }

}

