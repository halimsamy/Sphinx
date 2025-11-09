# Sphinx

Sphinx is an experimental, component-driven .NET software protection and licensing toolkit. It loads one
or more managed assemblies, evaluates them inside isolated *contexts*, and then applies a configurable
pipeline of protection components (obfuscation, metadata hardening, constant removal, etc.) before
writing hardened binaries back to disk.

> Status: 0.0.1 beta. The codebase is usable for experimentation and research but is not production-ready.

## Features
- **Context-aware pipeline** – Each target module runs inside its own `Context`, honoring per-module and
  global configuration values plus automatically resolving cross-assembly dependencies.
- **Component model** – Protections inherit from `Component`, expose CLI-friendly identifiers, and are
  orchestrated through deterministic execution phases (`Analyze`, `Apply`, `Finalize`). New components can
  be registered just by dropping a class into the assembly.
- **Dependency injection & logging** – Components receive services (e.g., `ILogger<T>`) via the built-in
  Microsoft DI container and log through NLog with rich console output.
- **Built-in protections** – Anti-ILDasm, constant string removal/virtualized decode helpers, and invalid
  metadata emitters demonstrate the pipeline and can be toggled per target.
- **dnlib-powered** – Low-level IL, metadata, and PE rewrites rely on [dnlib](https://github.com/0xd4d/dnlib),
  making the toolchain cross-platform and scriptable.

## Requirements
- .NET Core SDK 3.1.x (the project targets `netcoreapp3.1`).
- A supported OS for .NET Core (Windows, macOS, Linux). The sample commands below assume macOS/Linux shells.
- Input assemblies must be managed .NET assemblies readable by dnlib.

## Building
```bash
# Restore dependencies and compile
DOTNET_CLI_UI_LANGUAGE=en dotnet build Sphinx/Sphinx.csproj -c Release
```
The compiled binary can be found at `Sphinx/bin/Release/netcoreapp3.1/Sphinx.dll` (or `.exe` on Windows).

## Running
Sphinx is configured entirely via command-line key/value pairs and optional XML project files.

```bash
# Run directly from source
DOTNET_CLI_UI_LANGUAGE=en dotnet run --project Sphinx/Sphinx.csproj -- \
  Project=protect.xml

# Or run a published binary
./Sphinx/bin/Release/netcoreapp3.1/Sphinx \
  Project=protect.xml Target:App:AntiILDasm=true
```

Key notes:
- Arguments follow `key=value` syntax. Prefix with `Target:<ContextName>:` to scope values to a specific
  module context. Any value without a target prefix acts as a global default.
- Passing `Project=<path>` loads additional configuration from an XML file. After loading the XML, CLI
  arguments are applied again so they can override file-based settings.
- Logs are emitted through NLog (TRACE+ by default) so you can monitor each phase and component.

## Configuration file layout
`protect.xml` (or any `<Project>` XML file) describes the targets you want to harden.

```xml
<Project>
  <Target>
    <App>
      <InputFile>./samples/App.exe</InputFile>
      <OutputFile>./protected/App-protected.exe</OutputFile>
      <WritePdb>false</WritePdb>

      <!-- Toggle components by their Id -->
      <AntiILDasm>true</AntiILDasm>
      <ConstRemoval>true</ConstRemoval>
      <InvalidMetadata>true</InvalidMetadata>
    </App>

    <Dependency>
      <InputFile>./samples/Lib.dll</InputFile>
      <OutputDir>./protected/lib</OutputDir>
      <AntiILDasm>true</AntiILDasm>
      <InvalidMetadata>false</InvalidMetadata>
    </Dependency>
  </Target>
</Project>
```

Configuration tips:
- `Target` children (`App`, `Dependency`, …​) become context names. Each must define at least `InputFile` and
  either `OutputFile` or `OutputDir` (defaults to in-place overwrite with backup `.bak`).
- Global switches (e.g., `WritePdb`, `OutputDir`, `AntiILDasm`) can be declared at the root level and serve as
  defaults for every target. Context values override globals.
- Component parameters (marked with `[ComponentParam]` inside a component) are materialized automatically using
  `Id:ParameterName` keys (e.g., `ConstRemoval:DecoderSeed=123`). The current components rely only on booleans
  but the infrastructure supports typed values.

## Built-in components
| Id             | Usage        | Description |
|----------------|--------------|-------------|
| `AntiILDasm`   | Protecting   | Injects `SuppressIldasmAttribute` to discourage disassembly in ILDasm and similar tools. |
| `ConstRemoval` | Protecting   | Replaces literal strings with on-the-fly XOR-decoded helpers backed by randomly generated keys embedded as resources. Utilizes `TraceService` to stay IL-valid. |
| `InvalidMetadata` | Protecting | Hooks dnlib writer events to emit malformed metadata/PE sections (extra heaps, bogus ENC tables, truncated headers) that confuse many inspectors. |

Components run in order of `ComponentUsage` (Licensing → Optimizing → Protecting → Compressing) and then by
ascending `Priority`. You can add your own by:
1. Creating a class that inherits `Component`.
2. Implementing metadata (`Id`, `Name`, `Description`, `Usage`, `Priority`).
3. Annotating phase-specific methods with `[ComponentExecutionPoint(ExecutionPhase.Apply)]` (or `Analyze` / `Finalize`).
4. Optionally declaring `[ComponentParam("SomeSetting", defaultValue)]` fields to surface configuration knobs.
5. Dropping the file into the project—`Component.Resolve()` picks it up automatically and DI injects dependencies.

## Context processing & output
- `Context.Resolve()` reads every `<Target>` entry, loads the module via dnlib, and keeps a `TraceService` per
  context for IL flow analysis.
- `ContextDependencyResolver.Sort()` ensures referenced assemblies are processed before dependents so metadata
  remains consistent when rewriting multi-module solutions.
- After all components finish, `Context.WriteModule()` writes the protected assembly, creating backups when the
  output path equals the input path and ensuring destination directories exist.

## Development
- Restore dependencies with `dotnet restore Sphinx/Sphinx.csproj`.
- Use your preferred IDE (Rider, VS, VS Code + C# extension). The solution file `Sphinx.sln` targets a single
  executable project.
- Logging is configured in `Program.cs::ConfigNLog` and can be customized to add files, set lower verbosity, etc.
- Contributions are welcome—file issues or PRs describing the protection idea, its intended `ComponentUsage`, and
  any additional configuration keys you plan to introduce.

## License & credits
The upstream repository credits Abdelhalim Samy (2020). No explicit OSS license is bundled here; assume "all rights reserved"
unless the owner specifies otherwise. dnlib and Microsoft.Extensions packages retain their respective licenses.
