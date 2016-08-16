using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;

namespace NTratch
{
    /// <summary>
    /// Set up Logger class
    /// </summary>
    public static class Logger
    {
        static string LogFileName;
        static StreamWriter LogWriter;
        public static void Initialize()
        {
            LogFileName = IOFile.CompleteFileNameOutput(
                DateTime.Today.Date.ToShortDateString().Replace("/", "") + ".log");
            LogWriter = File.AppendText(LogFileName);
            Log("-------------------------------------------------------");
            Log("-------------------------------------------------------");
            Log("New Task.");
        }
        public static void Log(string message)
        {
            Console.WriteLine("[{0}] {1}", DateTime.Now.ToString(), message);
            LogWriter.WriteLine("[{0}] {1}", DateTime.Now.ToString(), message);
            LogWriter.Flush();
        }
        public static void Log(int number)
        {
            Log(number.ToString());
        }
        public static void Log(Exception e)
        {
            Console.WriteLine("[{0}]", DateTime.Now.ToString());
            LogWriter.WriteLine("[{0}]", DateTime.Now.ToString());
            Console.WriteLine(e);
            LogWriter.WriteLine(e);
            LogWriter.Flush();
        }
        public static void Close()
        {
            LogWriter.Close();
        }
    }

    /// <summary>
    /// Set up Config class to process the config file
    /// </summary>
    static class Config
    {
        static public string[] LogMethods { get; private set; } // "WriteError"
        static public string[] NotLogMethods { get; private set; } // "TraceUtil.If"
        static public int LogLevelArgPos { get; private set; } // ="2"
        static public string[] AbortMethods { get; private set; } 
        static public string[] DefaultMethods { get; private set; } 

        static public void Load(string FileName)
        {
            StreamReader Input = null;

            try
            {
                Input = new StreamReader(FileName);
            }
            catch (FileNotFoundException)
            {
                MemoryStream stream = new MemoryStream();
                StreamWriter writer = new StreamWriter(stream);
                
                writer.WriteLine("LoggingService.,host.Writer.Write,WriteComment,Trace.Write,TraceUtil.Write,File.WriteAllText,DebugLogException,LogError,Debug.Assert,Debug.Write,LogWarningFromException,LogSyntaxError,WriteLine,stackTrace.Append,Debug,Error%	 	LogMethods");
                writer.WriteLine("WriteLineIf,TraceUtil.If,html.WriteLine,gen.WriteLine,output.WriteLine,o.WriteLine% NotLogMethods");
                writer.WriteLine("0%					LogLevelIndex");
                writer.WriteLine("abort,exit%	 	AbortMethods");
                writer.WriteLine("printStackTrace%	 	DefaultMethods");

                writer.Flush();
                stream.Position = 0;

                Input = new StreamReader(stream);
            }
            finally
            {
                LogMethods = GetOneParameter(Input).Split(',');
                NotLogMethods = GetOneParameter(Input).Split(',');
                LogLevelArgPos = Convert.ToInt32(GetOneParameter(Input));
                Input.Close();
            }
        }

        static private string GetOneParameter(StreamReader Input)
        {
            string Parameter = Input.ReadLine();
            Parameter = Parameter.Split('%')[0];
            return Parameter;
        }
    }

    /// <summary>
    /// File name processing
    /// </summary>
    static class IOFile
    {
        public static string InputFolderPath;
        public static string OutputFolderPath;
       
        public static string CompleteFileNameInput(string tail)
        {
            return (InputFolderPath + "\\" + InputFolderPath.Split('\\').Last() + "_" + tail);
        }

        public static string CompleteFileNameOutput(string tail)
        {
            return (OutputFolderPath + "\\" + OutputFolderPath.Split('\\').Last() + "_" + tail);
        }
        

        static public string DeleteSpace(string str)
        {
            if (str == null || str == "") return str;

            string updatedStr = str.Replace("\n", "").Replace("\r", "").Replace("\t", "")
            .Replace("    ", " ").Replace("    ", " ").Replace("   ", " ")
            .Replace("  ", " ");

            return updatedStr;
        }

        static public string MethodNameExtraction(string str)
        {
            try
            {
                string methodName = str;
                try
                {                  
                    methodName = Regex.Replace(methodName, "<.*>", "");
                    methodName = Regex.Replace(methodName, "{.*}", "");
                    methodName = Regex.Replace(methodName, "\\(.*\\)", "");
                    if (methodName.IndexOf('(') != -1)
                    {
                        methodName = methodName.Split('(').First();
                    }
                    methodName = DeleteSpace(methodName);
                    methodName = methodName.Replace(" ", "");
                }
                catch { }
                return methodName;
            }
            catch
            {
                return null;
            }
        }

        static public string ShortMethodNameExtraction(string str)
        {
            try
            {
                string methodName = null;
                MatchCollection allMatches = Regex.Matches(str, "\\.[a-zA-Z0-9\\s]+\\(");
                if (allMatches.Count > 1)
                {
                    methodName = Regex.Replace(allMatches[allMatches.Count - 1].ToString(), "[\\.(\\s]", "");
                }
                else
                {
                    methodName = MethodNameExtraction(str);
                }
                if (methodName.IndexOf('.') != -1)
                {
                    methodName = methodName.Split('.').Last();
                }
                else if (methodName == null)
                {
                    Logger.Log("An API call cannot be extracted by the ShortMethodNameExtraction function:\n" + str);
                }

                return methodName;
            }
            catch
            {
                return null;
            }
        }
    }

}