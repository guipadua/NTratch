using System;
using System.IO;

namespace NTratch
{
    class Program
    {
        static void Main(string[] args)
        {
            string inputMode = args[0];
            string filePath = args[1];
            if (filePath.EndsWith("\\"))
            {
                filePath = filePath.Remove(filePath.LastIndexOf('\\'));
            }
            IOFile.InputFolderPath = Path.GetFullPath(filePath);
            IOFile.OutputFolderPath = Directory.GetCurrentDirectory();
            
            Logger.Initialize();
            DateTime StartTime = DateTime.Now;

            Logger.Log("Current directory is: " + IOFile.OutputFolderPath.ToString());


            Config.Load(IOFile.CompleteFileNameInput("Config.txt"));

            // traverse all the code folder for pattern check
            CodeWalker walker = new CodeWalker();
            walker.LoadByInputMode(inputMode, filePath);

            DateTime EndTime = DateTime.Now;
            Logger.Log("All done. Elapsed time: " + (EndTime - StartTime).ToString());
            Logger.Log("Press any key to terminate!");
            Logger.Close();
            //Console.ReadKey();  
        }
    }
}
