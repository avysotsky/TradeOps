using System.Diagnostics;

namespace TradeOps.ProviderTranscriptResearchDemo;

public sealed record ProviderTranscriptResearchDemoOptions(
    string DocFlowRoot,
    string Model,
    string PythonExecutable,
    string? WorkDirectory)
{
    public const string DefaultPythonExecutable =
        "python";

    public static ProviderTranscriptResearchDemoOptions Parse(
        string[] args)
    {
        ArgumentNullException.ThrowIfNull(
            args);

        string? docFlowRoot =
            null;
        string? model =
            null;
        string python =
            DefaultPythonExecutable;
        string? workDirectory =
            null;

        for (var index = 0;
             index < args.Length;
             index++)
        {
            switch (args[index])
            {
                case "--docflow-root":
                    docFlowRoot =
                        RequireValue(
                            args,
                            ref index,
                            "--docflow-root");
                    break;

                case "--model":
                    model =
                        RequireValue(
                            args,
                            ref index,
                            "--model");
                    break;

                case "--python":
                    python =
                        RequireValue(
                            args,
                            ref index,
                            "--python");
                    break;

                case "--work-dir":
                    workDirectory =
                        RequireValue(
                            args,
                            ref index,
                            "--work-dir");
                    break;

                default:
                    throw new ArgumentException(
                        "Unknown argument '" + args[index] + "'. Supported: --docflow-root <path>, --model <model>, --python <executable>, --work-dir <path>.");
            }
        }

        if (string.IsNullOrWhiteSpace(
                docFlowRoot))
        {
            throw new ArgumentException(
                "--docflow-root <path> is required.");
        }

        if (string.IsNullOrWhiteSpace(
                model))
        {
            throw new ArgumentException(
                "--model <explicit-openai-model> is required; no default model is configured.");
        }

        return new ProviderTranscriptResearchDemoOptions(
            docFlowRoot,
            model,
            python,
            workDirectory);
    }

    public string ResolveWorkDirectory(
        string tradeOpsRoot)
    {
        if (!string.IsNullOrWhiteSpace(
                WorkDirectory))
        {
            return Path.GetFullPath(
                WorkDirectory);
        }

        return Path.Combine(
            Path.GetFullPath(
                tradeOpsRoot),
            ".tradeops",
            "provider-transcript-demo");
    }

    private static string RequireValue(
        string[] args,
        ref int index,
        string option)
    {
        if (index + 1 >=
            args.Length
            || string.IsNullOrWhiteSpace(
                args[index + 1]))
        {
            throw new ArgumentException(
                option + " requires a value.");
        }

        index++;
        return args[index];
    }
}

public sealed record ChildProcessInvocation(
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory);

public interface IChildProcessRunner
{
    int Run(
        ChildProcessInvocation invocation);
}

public sealed class SystemChildProcessRunner :
    IChildProcessRunner
{
    public int Run(
        ChildProcessInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(
            invocation);

        using var process =
            new Process
            {
                StartInfo =
                    CreateStartInfo(
                        invocation)
            };

        process.OutputDataReceived +=
            static (_, _) =>
            {
            };
        process.ErrorDataReceived +=
            static (_, _) =>
            {
            };

        if (!process.Start())
        {
            throw new InvalidOperationException(
                "Child process could not be started.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        return process.ExitCode;
    }

    public static ProcessStartInfo CreateStartInfo(
        ChildProcessInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(
            invocation);

        if (string.IsNullOrWhiteSpace(
                invocation.FileName))
        {
            throw new ArgumentException(
                "Process file name is required.",
                nameof(invocation));
        }

        if (string.IsNullOrWhiteSpace(
                invocation.WorkingDirectory))
        {
            throw new ArgumentException(
                "Process working directory is required.",
                nameof(invocation));
        }

        var startInfo =
            new ProcessStartInfo
            {
                FileName =
                    invocation.FileName,
                WorkingDirectory =
                    invocation.WorkingDirectory,
                UseShellExecute =
                    false,
                RedirectStandardOutput =
                    true,
                RedirectStandardError =
                    true,
                CreateNoWindow =
                    true
            };

        foreach (var argument in
                 invocation.Arguments)
        {
            startInfo.ArgumentList.Add(
                argument);
        }

        return startInfo;
    }
}

public interface IEnvironmentReader
{
    string? Get(
        string name);
}

public sealed class SystemEnvironmentReader :
    IEnvironmentReader
{
    public string? Get(
        string name) =>
        Environment.GetEnvironmentVariable(
            name);
}

public static class TradeOpsRepositoryLocator
{
    public static string Resolve()
    {
        var directory =
            new DirectoryInfo(
                AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(
                    Path.Combine(
                        directory.FullName,
                        "TradeOps.sln")))
            {
                return directory.FullName;
            }

            directory =
                directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "TradeOps repository root could not be resolved from the executable location.");
    }
}

public sealed class ProviderTranscriptResearchDemoHarness
{
    public const string ApiKeyEnvironmentVariable =
        "OPENAI_API_KEY";

    public const string PriorDocumentName =
        "Synthetic Example Company Q1 2026 earnings call";

    public const string CurrentDocumentName =
        "Synthetic Example Company Q2 2026 earnings call";

    private readonly IChildProcessRunner _processRunner;
    private readonly IEnvironmentReader _environmentReader;

    public ProviderTranscriptResearchDemoHarness(
        IChildProcessRunner processRunner,
        IEnvironmentReader environmentReader)
    {
        _processRunner =
            processRunner
            ?? throw new ArgumentNullException(
                nameof(processRunner));
        _environmentReader =
            environmentReader
            ?? throw new ArgumentNullException(
                nameof(environmentReader));
    }

    public int Run(
        ProviderTranscriptResearchDemoOptions options,
        string tradeOpsRoot,
        TextWriter? output = null,
        TextWriter? error = null)
    {
        ArgumentNullException.ThrowIfNull(
            options);

        output ??=
            Console.Out;
        error ??=
            Console.Error;

        try
        {
            return RunCore(
                options,
                tradeOpsRoot,
                output);
        }
        catch (Exception exception)
        {
            error.WriteLine(
                "PROVIDER TRANSCRIPT RESEARCH DEMO: FAIL (" +
                exception.GetType().Name +
                ": " +
                exception.Message +
                ")");

            return 1;
        }
    }

    private int RunCore(
        ProviderTranscriptResearchDemoOptions options,
        string tradeOpsRoot,
        TextWriter output)
    {
        var resolvedTradeOpsRoot =
            Path.GetFullPath(
                tradeOpsRoot);
        var docFlowRoot =
            Path.GetFullPath(
                options.DocFlowRoot);
        var docFlowWorkerDirectory =
            Path.Combine(
                docFlowRoot,
                "src",
                "DocFlow.Extraction.Worker");
        var docFlowEntryPoint =
            Path.Combine(
                docFlowWorkerDirectory,
                "text_artifact_main.py");
        var workDirectory =
            options.ResolveWorkDirectory(
                resolvedTradeOpsRoot);

        ValidateDocFlow(
            docFlowRoot,
            docFlowEntryPoint);
        ValidateWorkDirectory(
            resolvedTradeOpsRoot,
            workDirectory);

        var priorRaw =
            RequireFile(
                Path.Combine(
                    resolvedTradeOpsRoot,
                    "samples",
                    "research",
                    "provider-transcript-demo",
                    "prior-raw.json"),
                "Prior raw transcript");
        var currentRaw =
            RequireFile(
                Path.Combine(
                    resolvedTradeOpsRoot,
                    "samples",
                    "research",
                    "provider-transcript-demo",
                    "current-raw.json"),
                "Current raw transcript");
        var schemaRequest =
            RequireFile(
                Path.Combine(
                    resolvedTradeOpsRoot,
                    "schemas",
                    "research",
                    "earnings-transcript-facts-v1.schema-request.json"),
                "Earnings extraction schema request");
        var policy =
            RequireFile(
                Path.Combine(
                    resolvedTradeOpsRoot,
                    "samples",
                    "research",
                    "transcript-research",
                    "policy.json"),
                "Transcript research policy");
        var consumerManifest =
            RequireFile(
                Path.Combine(
                    resolvedTradeOpsRoot,
                    "samples",
                    "research",
                    "provider-transcript-demo",
                    "consumer-manifest.json"),
                "Provider demo consumer manifest");

        if (string.IsNullOrWhiteSpace(
                _environmentReader.Get(
                    ApiKeyEnvironmentVariable)))
        {
            throw new InvalidOperationException(
                ApiKeyEnvironmentVariable +
                " is required in the environment.");
        }

        Directory.CreateDirectory(
            workDirectory);

        var priorNormalized =
            Path.Combine(
                workDirectory,
                "prior-normalized.json");
        var priorStructured =
            Path.Combine(
                workDirectory,
                "prior-structured.json");
        var currentNormalized =
            Path.Combine(
                workDirectory,
                "current-normalized.json");
        var currentStructured =
            Path.Combine(
                workDirectory,
                "current-structured.json");
        var runtimeManifest =
            Path.Combine(
                workDirectory,
                "manifest.json");
        var resultJson =
            Path.Combine(
                workDirectory,
                "transcript-research-result.json");

        output.WriteLine(
            "Stage: initialize");
        output.WriteLine(
            "Model: " +
            options.Model);
        output.WriteLine(
            "DocFlow root: " +
            docFlowRoot);
        output.WriteLine(
            "Work directory: " +
            workDirectory);

        var priorExit =
            RunStage(
                output,
                "DocFlow prior",
                BuildDocFlowInvocation(
                    options.PythonExecutable,
                    docFlowWorkerDirectory,
                    priorRaw,
                    schemaRequest,
                    options.Model,
                    priorNormalized,
                    priorStructured,
                    PriorDocumentName));

        if (priorExit != 0)
        {
            throw new InvalidOperationException(
                "Prior DocFlow stage failed with exit code " +
                priorExit +
                ".");
        }

        var currentExit =
            RunStage(
                output,
                "DocFlow current",
                BuildDocFlowInvocation(
                    options.PythonExecutable,
                    docFlowWorkerDirectory,
                    currentRaw,
                    schemaRequest,
                    options.Model,
                    currentNormalized,
                    currentStructured,
                    CurrentDocumentName));

        if (currentExit != 0)
        {
            throw new InvalidOperationException(
                "Current DocFlow stage failed with exit code " +
                currentExit +
                ".");
        }

        RequireGeneratedFile(
            priorNormalized,
            "Prior normalized artifact");
        RequireGeneratedFile(
            priorStructured,
            "Prior structured artifact");
        RequireGeneratedFile(
            currentNormalized,
            "Current normalized artifact");
        RequireGeneratedFile(
            currentStructured,
            "Current structured artifact");

        File.Copy(
            consumerManifest,
            runtimeManifest,
            overwrite:
                true);

        RequireGeneratedFile(
            runtimeManifest,
            "Runtime consumer manifest");

        var consumerExit =
            RunStage(
                output,
                "TradeOps VS-08",
                new ChildProcessInvocation(
                    "dotnet",
                    new[]
                    {
                        "run",
                        "--project",
                        "tools/TradeOps.TranscriptResearchDemo",
                        "--",
                        "--manifest",
                        runtimeManifest,
                        "--policy",
                        policy,
                        "--json",
                        resultJson
                    },
                    resolvedTradeOpsRoot));

        if (consumerExit != 0)
        {
            throw new InvalidOperationException(
                "TradeOps VS-08 stage failed with exit code " +
                consumerExit +
                ".");
        }

        if (!File.Exists(
                resultJson)
            || new FileInfo(
                    resultJson).Length == 0)
        {
            throw new InvalidDataException(
                "TradeOps VS-08 result artifact is missing or empty.");
        }

        output.WriteLine(
            "PROVIDER TRANSCRIPT RESEARCH DEMO: PASS");
        output.WriteLine(
            "Result JSON: " +
            resultJson);

        return 0;
    }

    private int RunStage(
        TextWriter output,
        string stage,
        ChildProcessInvocation invocation)
    {
        output.WriteLine(
            "Stage: " +
            stage);

        var exitCode =
            _processRunner.Run(
                invocation);

        output.WriteLine(
            "Exit status: " +
            exitCode);

        return exitCode;
    }

    private static ChildProcessInvocation BuildDocFlowInvocation(
        string pythonExecutable,
        string docFlowWorkerDirectory,
        string inputRawJson,
        string schemaRequest,
        string model,
        string outputNormalizedJson,
        string outputStructuredJson,
        string documentName) =>
        new(
            pythonExecutable,
            new[]
            {
                "text_artifact_main.py",
                "--input-raw-json",
                inputRawJson,
                "--schema-request",
                schemaRequest,
                "--model",
                model,
                "--output-normalized-json",
                outputNormalizedJson,
                "--output-structured-json",
                outputStructuredJson,
                "--document-name",
                documentName
            },
            docFlowWorkerDirectory);

    private static void ValidateDocFlow(
        string docFlowRoot,
        string entryPoint)
    {
        if (!Directory.Exists(
                docFlowRoot))
        {
            throw new DirectoryNotFoundException(
                "DocFlow root directory was not found.");
        }

        if (!File.Exists(
                entryPoint))
        {
            throw new FileNotFoundException(
                "DocFlow DF-06 entry point was not found.",
                entryPoint);
        }
    }

    private static void ValidateWorkDirectory(
        string tradeOpsRoot,
        string workDirectory)
    {
        var samplesRoot =
            Path.Combine(
                tradeOpsRoot,
                "samples");
        var schemasRoot =
            Path.Combine(
                tradeOpsRoot,
                "schemas");

        if (IsWithin(
                workDirectory,
                samplesRoot)
            || IsWithin(
                workDirectory,
                schemasRoot))
        {
            throw new InvalidOperationException(
                "Work directory must not be inside committed sample/schema directories.");
        }
    }

    private static bool IsWithin(
        string candidate,
        string parent)
    {
        var relative =
            Path.GetRelativePath(
                Path.GetFullPath(
                    parent),
                Path.GetFullPath(
                    candidate));

        if (string.Equals(
                relative,
                ".",
                StringComparison.Ordinal))
        {
            return true;
        }

        if (Path.IsPathRooted(
                relative)
            || string.Equals(
                relative,
                "..",
                StringComparison.Ordinal)
            || relative.StartsWith(
                ".." +
                Path.DirectorySeparatorChar,
                StringComparison.Ordinal)
            || relative.StartsWith(
                ".." +
                Path.AltDirectorySeparatorChar,
                StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static string RequireFile(
        string path,
        string description)
    {
        var fullPath =
            Path.GetFullPath(
                path);

        if (!File.Exists(
                fullPath))
        {
            throw new FileNotFoundException(
                description +
                " file was not found.",
                fullPath);
        }

        return fullPath;
    }

    private static void RequireGeneratedFile(
        string path,
        string description)
    {
        if (!File.Exists(
                path))
        {
            throw new FileNotFoundException(
                description +
                " was not produced.",
                path);
        }
    }
}

public static class ProviderTranscriptResearchDemoCli
{
    public static int Run(
        string[] args,
        TextWriter? output = null,
        TextWriter? error = null,
        IChildProcessRunner? processRunner = null,
        IEnvironmentReader? environmentReader = null,
        string? tradeOpsRoot = null)
    {
        output ??=
            Console.Out;
        error ??=
            Console.Error;

        try
        {
            var options =
                ProviderTranscriptResearchDemoOptions
                    .Parse(
                        args);
            var root =
                string.IsNullOrWhiteSpace(
                    tradeOpsRoot)
                ? TradeOpsRepositoryLocator
                    .Resolve()
                : Path.GetFullPath(
                    tradeOpsRoot);

            return new ProviderTranscriptResearchDemoHarness(
                    processRunner
                    ?? new SystemChildProcessRunner(),
                    environmentReader
                    ?? new SystemEnvironmentReader())
                .Run(
                    options,
                    root,
                    output,
                    error);
        }
        catch (Exception exception)
        {
            error.WriteLine(
                "PROVIDER TRANSCRIPT RESEARCH DEMO: FAIL (" +
                exception.GetType().Name +
                ": " +
                exception.Message +
                ")");

            return 1;
        }
    }
}
