using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Writer;
using Microsoft.Extensions.Configuration;
using Sphinx.Services;

namespace Sphinx
{
    /// <summary>
    ///     A module context.
    /// </summary>
    public class Context
    {
        public readonly string Name;
        public readonly ModuleDefMD Module;
        public readonly ModuleWriterOptions WriterOptions;
        public readonly TraceService TraceService;

        public Context(string name, ModuleDefMD module)
        {
            this.Name = name;
            this.Module = module;
            this.WriterOptions = new ModuleWriterOptions(module)
            {
                WritePdb = this.GetParam("WritePdb", true)
            };
            this.TraceService = new TraceService();
        }

        /// <summary>
        ///     Resolve all Contexts.
        /// </summary>
        /// <returns></returns>
        public static List<Context> Resolve()
        {
            var modCtx = ModuleDef.CreateModuleContext();

            return Program.Config.GetSection("Target").GetChildren().Select(t =>
                    new Context(t.Key, ModuleDefMD.Load(t.GetValue<string>("InputFile"), modCtx)))
                .ToList();
        }

        public T GetParam<T>(string key, T defaultValue = default)
        {
            return Program.Config.GetValue($"Target:{this.Name}:{key}",
                Program.Config.GetValue(key, defaultValue));
        }

        public object GetParam(Type type, string key, object defaultValue)
        {
            return Program.Config.GetValue(type, $"Target:{this.Name}:{key}",
                Program.Config.GetValue(type, key, defaultValue));
        }

        public bool IsEnabled(Component component)
        {
            return this.GetParam(component.Id, false);
        }

        public void WriteModule()
        {
            var inputFile = Program.Config.GetValue<string>($"Target:{this.Name}:InputFile");
            var outputFile = Program.Config.GetValue<string>($"Target:{this.Name}:OutputFile");
            var outputDir = Path.GetDirectoryName(outputFile);

            if (string.IsNullOrEmpty(outputFile))
            {
                outputDir = this.GetParam<string>("OutputDir");
                outputFile = !string.IsNullOrEmpty(outputDir)
                    ? Path.Combine(outputDir, Path.GetFileName(inputFile))
                    : inputFile;
            }

            if (outputFile == inputFile)
            {
                var counter = 0;
                var backup = inputFile + ".bak";
                while (File.Exists(backup))
                {
                    counter++;
                    backup = inputFile + "_" + counter + ".bak";
                }

                File.Move(inputFile, backup);
            }

            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            if (File.Exists(outputFile)) File.Delete(outputFile);

            this.Module.Write(outputFile, this.WriterOptions);
        }
    }
}