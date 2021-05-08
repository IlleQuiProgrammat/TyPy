using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;

namespace TyPy.Cli
{
    class Program
    {
        static int Main(string[] args)
        {
            var outOption = new Option<FileInfo>(
                "--out",
                () => null,
                "Defines the output executable.");
            outOption.AddAlias("-o");
            
            var inOption = new Option<FileInfo>(
                "--in",
                () => null,
                "Main program file.");
            inOption.AddAlias("-i");
            
            var rootCommand = new RootCommand
            {
                outOption,
                inOption
            };

            rootCommand.Description = "Compiler for TyPy - a statically-typed, python-based language.";
            // Note that the parameters of the handler method are matched according to the names of the options
            rootCommand.Handler = CommandHandler.Create<FileInfo, FileInfo>(Run);

            // Parse the incoming args and invoke the handler
            return rootCommand.InvokeAsync(args).Result;
        }

        static void Run(FileInfo outFile, FileInfo inFile)
        {
            Console.WriteLine(outFile.FullName);
            Console.WriteLine(inFile.FullName);
        }
    }
}
