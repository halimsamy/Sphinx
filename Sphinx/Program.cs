using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using dnlib.DotNet.Writer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Targets;
using LogLevel = NLog.LogLevel;

namespace Sphinx
{
    internal class Program
    {
        public static IConfigurationRoot Config;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static void Main(string[] args)
        {
            ConfigNLog();

            try
            {
                Logger.Fatal("Sphinx - Advanced .NET Software Protection and Licensing System [Version 0.0.1b]");
                Logger.Fatal("Copyright (C) 2020 Klito. All rights reserved.");
                Logger.Info($"Running on {Environment.OSVersion} - {IntPtr.Size * 8} bit, .NET {Environment.Version}");

                Config = BuildConfiguration(args);

                using var servicesProvider = BuildServiceProvider();

                var stopwatch = new Stopwatch();
                stopwatch.Start();

                Logger.Info("Loading targets...");
                var contexts = Context.Contexts;

                if (contexts.Count > 0)
                {
                    Logger.Info("Resolving dependecny...");
                    contexts = ContextDependencyResolver.Sort(contexts);

                    var components = Component.Resolve()
                        .Select(t => servicesProvider.GetRequiredService(t) as Component)
                        .Where(c => contexts.Any(ctx => ctx.IsEnabled(c)))
                        .ToList();

                    if (components.Count > 0)
                    {
                        components.Sort();

                        foreach (var component in components)
                        {
                            Logger.Info($"Applying '{component.Name}'...");

                            for (ExecutionPhase phase = 0; phase <= ExecutionPhase.Finishes; phase++)
                            {
                                Logger.Trace($"'{component.Name}' {phase} phase...");
                                foreach (var context in contexts.Where(context => context.IsEnabled(component)))
                                {
                                    component.Switch(context);
                                    component.Execute(phase);
                                }
                            }
                        }

                        Logger.Info("Saving targets...");
                        contexts.ForEach(ctx => ctx.WriteModule());
                    }
                    else
                    {
                        Logger.Fatal("There is nothing to apply to any target.");
                    }
                }
                else
                {
                    Logger.Fatal("No targets were found.");
                }

                stopwatch.Stop();
                Logger.Info($"Done. Total elapsed time ({stopwatch.Elapsed})");
            }
            catch (ModuleWriterException e)
            {
                Logger.Fatal("Unable to save targets.");
                Logger.Debug(e);
            }
            catch (Exception ex)
            {
                //logger.Error(ex, "Stopped program because of exception");
                Logger.Error(ex.Message);
                Logger.Debug(ex);
            }
            finally
            {
                // Ensure to flush and stop internal timers/threads before application-exit
                // (Avoid segmentation fault on Linux)
                LogManager.Shutdown();
            }
        }

        private static void ConfigNLog()
        {
            var logConfig = new LoggingConfiguration();
            var logConsole = new ColoredConsoleTarget("console")
            {
                Layout = "[${level:uppercase=true:padding=5}] ${message} ${exception}"
            };
            logConfig.AddRule(LogLevel.Trace, LogLevel.Fatal, logConsole);
            LogManager.Configuration = logConfig;
        }

        private static IConfigurationRoot BuildConfiguration(string[] args)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddCommandLine(args)
                .Build();

            var projectFile = config.GetValue<string>("Project");

            if (projectFile != null)
            {
                Logger.Info("Project file detected...");

                var projectFileDir = Path.GetDirectoryName(projectFile);
                if (!string.IsNullOrEmpty(projectFileDir))
                {
                    Logger.Info("Changing current directory to match the project file...");
                    Directory.SetCurrentDirectory(projectFileDir);
                }

                config = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddCommandLine(args)
                    .AddXmlFile(projectFile, false, false)
                    .Build();
            }

            // TODO: Handle other Commands from the Console.

            return config;
        }

        private static ServiceProvider BuildServiceProvider()
        {
            var s = new ServiceCollection()
                .AddLogging(loggingBuilder =>
                {
                    // configure Logging with NLog
                    loggingBuilder.ClearProviders();
                    loggingBuilder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                    loggingBuilder.AddNLog(Config);
                });

            Component.Resolve().ForEach(ct => s.AddTransient(ct));

            return s.BuildServiceProvider();
        }
    }
}