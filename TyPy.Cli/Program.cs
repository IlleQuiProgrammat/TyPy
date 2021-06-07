using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using TyPy.Compiler;

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
            Run(null, null);
            // Parse the incoming args and invoke the handler
            return rootCommand.InvokeAsync(args).Result;
        }

        static void Run(FileInfo outFile, FileInfo inFile)
        {
            Console.WriteLine(outFile?.FullName);
            Console.WriteLine(inFile?.FullName);
            var tyPyPipeline = new TyPyPipeline();
            var begin = DateTime.Now;
            tyPyPipeline.Execute("1 - (5 + 2) + 6\n" +
                                 "5+3*6\n" +
                                 "7*3-10/2\n" +
                                 "4*6/3\n" +
                                 "4*5+4*3\n" +
                                 "4 ^ 2 / 2\n" +
                                 "5+10*5\n" +
                                 "8+5^2-9\n" +
                                 "(6*5)/(10-7)\n" +
                                 "8-5*2^2\n");
            Console.WriteLine($"Compilation took: {DateTime.Now - begin}");
        }
    }
}