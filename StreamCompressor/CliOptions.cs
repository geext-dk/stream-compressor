using System;
using System.IO;

namespace StreamCompressor
{
    /// <summary>
    /// Program action
    /// </summary>
    public enum ProgramAction
    {
        /// <summary>
        /// Compress a file using the parallel algorithm
        /// </summary>
        Compress,

        /// <summary>
        /// Decompress a file using the default algorithm
        /// </summary>
        Decompress
    }

    public class CliOptions
    {
        public ProgramAction ProgramAction { get; }
        public string InputFilePath { get; }
        public string OutputFilePath { get; }

        private CliOptions(ProgramAction programAction, string inputFilePath, string outputFilePath)
        {
            ProgramAction = programAction;
            InputFilePath = inputFilePath;
            OutputFilePath = outputFilePath;
        }

        /// <summary>
        /// Parse, validate command line arguments and create an instance of CliOptions 
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns></returns>
        public static CliOptions? FromArgs(string[] args)
        {
            if (args.Length != 3 || !Enum.TryParse(args[0], true, out ProgramAction action))
            {
                PrintUsage();
                return null;
            }

            var inputFilePath = args[1];
            var outputFilePath = args[2];

            if (!File.Exists(inputFilePath))
            {
                Console.Error.WriteLine($"The input file does not exist: {inputFilePath}.");
                return null;
            }

            if (File.Exists(outputFilePath))
            {
                Console.Error.WriteLine($"A file with the same filename already exists: {outputFilePath}.");
                return null;
            }

            return new CliOptions(action, inputFilePath, outputFilePath);
        }

        private static void PrintUsage()
        {
            Console.WriteLine("USAGE: GeeXT.StreamCompressor.exe compress/decompress <INPUT_PATH> <OUTPUT_PATH>");
        }
    }
}