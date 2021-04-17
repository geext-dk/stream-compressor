using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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
        public bool Verbose { get; }

        private CliOptions(ProgramAction programAction, string inputFilePath, string outputFilePath, bool verbose)
        {
            ProgramAction = programAction;
            InputFilePath = inputFilePath;
            OutputFilePath = outputFilePath;
            Verbose = verbose;
        }

        /// <summary>
        /// Parse, validate command line arguments and create an instance of CliOptions 
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns></returns>
        public static CliOptions? FromArgs(string[] args)
        {
            var options = args.Where(a => a.StartsWith("--"))
                .Select(a => a[2..].ToLower())
                .Where(a => a != string.Empty)
                .ToList();

            var positionalArgs = args.Where(a => !a.StartsWith("--"))
                .ToArray();
            
            if (positionalArgs.Length != 3 || !Enum.TryParse(positionalArgs[0], true, out ProgramAction action))
            {
                PrintUsage();
                return null;
            }

            var inputFilePath = positionalArgs[1];
            var outputFilePath = positionalArgs[2];

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

            return new CliOptions(action, inputFilePath, outputFilePath, options.Contains("verbose"));
        }

        private static void PrintUsage()
        {
            Console.WriteLine("USAGE: GeeXT.StreamCompressor.exe compress/decompress <INPUT_PATH> <OUTPUT_PATH>");
            Console.WriteLine();
            Console.WriteLine("Possible options:");
            Console.WriteLine("\t--verbose\tPrints additional information during the execution");
        }
    }
}