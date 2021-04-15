using System;
using System.IO;
using Microsoft.Extensions.Logging;
using StreamCompressor.Processors;

namespace StreamCompressor
{
    public class Program
    {
        /// <summary>
        /// Number of threads used in the compressor.
        /// </summary>
        private static readonly int NumberOfThreads = Environment.ProcessorCount;
        
        /// <summary>
        /// Block size used in the compressor. Specified in bytes.
        /// </summary>
        private const int BlockSize = 1024 * 1024;

        public static readonly ILoggerFactory LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(
            builder =>
            {
                builder.SetMinimumLevel(LogLevel.Warning)
                    .AddConsole();
            });
        
        public static int Main(string[] args)
        {
            var options = CliOptions.FromArgs(args);
            if (options == null)
            {
                return 1;
            }

            var logger = LoggerFactory.CreateLogger<Program>();

            FileStream? outputStream = null;
            try
            {
                logger.LogInformation("Opening file to read: {InputFilePath}", options.InputFilePath);
                using var inputStream = File.OpenRead(options.InputFilePath);
                
                logger.LogInformation("Opening file to write: {OutputFilePath}", options.OutputFilePath);
                outputStream = File.Open(options.OutputFilePath, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None);

                BaseParallelProcessor processor = options.ProgramAction switch
                {
                    ProgramAction.Compress => new ParallelCompressor(BlockSize, NumberOfThreads, LoggerFactory),
                    ProgramAction.Decompress => new ParallelDecompressor(BlockSize, NumberOfThreads, LoggerFactory),
                    _ => throw new NotImplementedException()
                };
                
                logger.LogInformation("Executing action: {ProgramAction}. Used processor: {Processor}",
                    options.ProgramAction, processor.GetType().Name);

                processor.Process(inputStream, outputStream);

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                if (outputStream != null)
                {
                    outputStream.Dispose();
                    File.Delete(options.OutputFilePath);
                }
                return 1;
            }
        }
    }
}