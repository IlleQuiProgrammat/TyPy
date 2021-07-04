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
            // Parse the incoming args and invoke the handler
            return rootCommand.InvokeAsync(args).Result;
        }

        static void Run(FileInfo outFile, FileInfo inFile)
        {
            Console.WriteLine(outFile?.FullName);
            Console.WriteLine(inFile?.FullName);
            var tyPyPipeline = new TyPyPipeline();
            var begin = DateTime.Now;
            tyPyPipeline.Execute("import string\n" +
                                 "import os as operating_system\n" +
                                 "from functools import reduce as red, wraps as wrap\n");
            Console.WriteLine($"Compilation took: {DateTime.Now - begin}");
            // var assemblyName = new AssemblyName("TestOutput");
            // var assemblyBuilder =
            //     AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndCollect);
            // var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name);
            // var exampleClass = moduleBuilder.DefineType("Ops", TypeAttributes.Public);
            // var methodBuilder = exampleClass.DefineMethod(
            //     "SumIt",
            //     MethodAttributes.Public,
            //     CallingConventions.Standard,
            //     typeof(int),
            //     new[] {typeof(int), typeof(int)}
            // );
            // var ilGenerator = methodBuilder.GetILGenerator();
            //
            // ilGenerator.Emit(OpCodes.Ldarg_1);
            // ilGenerator.Emit(OpCodes.Ldarg_2);
            // ilGenerator.Emit(OpCodes.Add_Ovf);
            // ilGenerator.Emit(OpCodes.Ret);
            //
            // Type newType = exampleClass.CreateType();
            //
            // dynamic typeInstance = Activator.CreateInstance(newType);
            // Console.WriteLine(typeInstance.SumIt(3, 4));
        }
    }
}