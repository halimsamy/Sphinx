using System.Collections.Generic;
using System.IO;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Writer;
using Microsoft.Extensions.Configuration;

namespace Sphinx
{
    /// <summary>
    ///     A module context.
    /// </summary>
    public class Context
    {
        public static IConfigurationRoot ProjectConfig;
        public readonly IConfigurationSection Config;
        public readonly ModuleDefMD Module;
        public readonly ModuleWriterOptions WriterOptions;

        public Context(IConfigurationSection config, ModuleDefMD module)
        {
            this.Config = config;
            this.Module = module;
            this.WriterOptions = new ModuleWriterOptions(module)
            {
                WritePdb = this.GetOptionValue("WritePdb", true)
            };
        }

        /// <summary>
        ///     Resolve all Contexts from a Configuration.
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        public static List<Context> Resolve(IConfigurationRoot config)
        {
            var modCtx = ModuleDef.CreateModuleContext();

            ProjectConfig = config;
            return config.GetSection("Target").GetChildren().Select(t =>
                    new Context(t, ModuleDefMD.Load(t.GetValue<string>("InputFile"), modCtx)))
                .ToList();
        }

        public T GetOptionValue<T>(string key, T defaultValue = default)
        {
            return this.Config.GetValue(key, ProjectConfig.GetValue(key, defaultValue));
        }

        public bool IsEnabled(Component component)
        {
            return this.GetOptionValue(component.Id, false);
        }

        public void WriteModule()
        {
            var inputFile = this.Config.GetValue<string>("InputFile");
            var outputFile = this.Config.GetValue("OutputFile", inputFile);
            var outputDir = Path.GetDirectoryName(outputFile);

            if (outputFile == inputFile)
            {
                var backup = inputFile + ".bak";
                if (File.Exists(backup)) File.Delete(backup);
                File.Move(inputFile, backup);
            }

            if (outputDir != "" && !Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            if (File.Exists(outputFile)) File.Delete(outputFile);

            this.Module.Write(outputFile, this.WriterOptions);
        }
    }
}