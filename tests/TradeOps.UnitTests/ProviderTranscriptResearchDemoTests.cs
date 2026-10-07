using TradeOps.ProviderTranscriptResearchDemo;
using Xunit;

namespace TradeOps.UnitTests;

public sealed class ProviderTranscriptResearchDemoTests
{
    [Fact]
    public void Options_require_explicit_model_and_have_no_model_default()
    {
        Assert.Throws<ArgumentException>(
            () =>
                ProviderTranscriptResearchDemoOptions
                    .Parse(
                        new[]
                        {
                            "--docflow-root",
                            "docflow"
                        }));
    }

    [Fact]
    public void Options_default_python_and_default_work_directory_are_exact()
    {
        var options =
            ProviderTranscriptResearchDemoOptions
                .Parse(
                    new[]
                    {
                        "--docflow-root",
                        "docflow",
                        "--model",
                        "explicit-model"
                    });

        Assert.Equal(
            "python",
            options.PythonExecutable);
        Assert.Null(
            options.WorkDirectory);

        var root =
            Path.GetFullPath(
                Path.Combine(
                    Path.GetTempPath(),
                    "tradeops-root"));

        Assert.Equal(
            Path.Combine(
                root,
                ".tradeops",
                "provider-transcript-demo"),
            options.ResolveWorkDirectory(
                root));
    }

    [Fact]
    public void Options_honor_explicit_python_model_and_work_directory()
    {
        using var fixture =
            new Fixture();
        var explicitWork =
            Path.Combine(
                fixture.Root,
                "explicit-work");

        var options =
            ProviderTranscriptResearchDemoOptions
                .Parse(
                    new[]
                    {
                        "--docflow-root",
                        fixture.DocFlowRoot,
                        "--model",
                        "model-verbatim",
                        "--python",
                        "python-custom",
                        "--work-dir",
                        explicitWork
                    });

        Assert.Equal(
            "model-verbatim",
            options.Model);
        Assert.Equal(
            "python-custom",
            options.PythonExecutable);
        Assert.Equal(
            Path.GetFullPath(
                explicitWork),
            options.ResolveWorkDirectory(
                fixture.TradeOpsRoot));
    }

    [Fact]
    public void Invalid_docflow_root_stops_before_process_execution()
    {
        using var fixture =
            new Fixture();
        var runner =
            new RecordingRunner();
        var options =
            fixture.Options(
                docFlowRoot:
                    Path.Combine(
                        fixture.Root,
                        "missing-docflow"));

        var result =
            fixture.Run(
                options,
                runner);

        Assert.Equal(
            1,
            result.ExitCode);
        Assert.Empty(
            runner.Invocations);
    }

    [Fact]
    public void Missing_df06_entry_point_stops_before_process_execution()
    {
        using var fixture =
            new Fixture();
        File.Delete(
            fixture.DocFlowEntryPoint);
        var runner =
            new RecordingRunner();

        var result =
            fixture.Run(
                fixture.Options(),
                runner);

        Assert.Equal(
            1,
            result.ExitCode);
        Assert.Empty(
            runner.Invocations);
    }

    [Fact]
    public void Missing_api_key_stops_before_process_execution()
    {
        using var fixture =
            new Fixture();
        var runner =
            new RecordingRunner();

        var result =
            fixture.Run(
                fixture.Options(),
                runner,
                apiKey:
                    null);

        Assert.Equal(
            1,
            result.ExitCode);
        Assert.Empty(
            runner.Invocations);
        Assert.Contains(
            "OPENAI_API_KEY is required",
            result.Error);
    }

    [Fact]
    public void Prior_and_current_df06_calls_use_exact_argument_lists_model_and_working_directory()
    {
        using var fixture =
            new Fixture();
        var runner =
            fixture.SuccessfulRunner();

        var result =
            fixture.Run(
                fixture.Options(
                    model:
                        "provider-model",
                    python:
                        "python-x"),
                runner);

        Assert.Equal(
            0,
            result.ExitCode);
        Assert.Equal(
            3,
            runner.Invocations.Count);

        var expectedWorkingDirectory =
            Path.Combine(
                fixture.DocFlowRoot,
                "src",
                "DocFlow.Extraction.Worker");

        Assert.Equal(
            expectedWorkingDirectory,
            runner.Invocations[0]
                .WorkingDirectory);
        Assert.Equal(
            expectedWorkingDirectory,
            runner.Invocations[1]
                .WorkingDirectory);

        Assert.Equal(
            "python-x",
            runner.Invocations[0]
                .FileName);
        Assert.Equal(
            "python-x",
            runner.Invocations[1]
                .FileName);

        Assert.Equal(
            new[]
            {
                "text_artifact_main.py",
                "--input-raw-json",
                fixture.PriorRaw,
                "--schema-request",
                fixture.SchemaRequest,
                "--model",
                "provider-model",
                "--output-normalized-json",
                Path.Combine(
                    fixture.WorkDirectory,
                    "prior-normalized.json"),
                "--output-structured-json",
                Path.Combine(
                    fixture.WorkDirectory,
                    "prior-structured.json"),
                "--document-name",
                ProviderTranscriptResearchDemoHarness
                    .PriorDocumentName
            },
            runner.Invocations[0]
                .Arguments);

        Assert.Equal(
            new[]
            {
                "text_artifact_main.py",
                "--input-raw-json",
                fixture.CurrentRaw,
                "--schema-request",
                fixture.SchemaRequest,
                "--model",
                "provider-model",
                "--output-normalized-json",
                Path.Combine(
                    fixture.WorkDirectory,
                    "current-normalized.json"),
                "--output-structured-json",
                Path.Combine(
                    fixture.WorkDirectory,
                    "current-structured.json"),
                "--document-name",
                ProviderTranscriptResearchDemoHarness
                    .CurrentDocumentName
            },
            runner.Invocations[1]
                .Arguments);
    }

    [Fact]
    public void Api_key_is_never_added_to_child_arguments_or_console()
    {
        using var fixture =
            new Fixture();
        var runner =
            fixture.SuccessfulRunner();
        const string secret =
            "unit-test-secret-value";

        var result =
            fixture.Run(
                fixture.Options(),
                runner,
                apiKey:
                    secret);

        Assert.Equal(
            0,
            result.ExitCode);

        foreach (var invocation in
                 runner.Invocations)
        {
            Assert.DoesNotContain(
                invocation.Arguments,
                argument =>
                    argument.Contains(
                        secret,
                        StringComparison.Ordinal));
        }

        Assert.DoesNotContain(
            secret,
            result.Output,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            secret,
            result.Error,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "100,000,000",
            result.Output,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "100,000,000",
            result.Error,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Process_start_info_uses_argument_list_and_no_shell()
    {
        var invocation =
            new ChildProcessInvocation(
                "python",
                new[]
                {
                    "text_artifact_main.py",
                    "--model",
                    "m"
                },
                Path.GetTempPath());

        var startInfo =
            SystemChildProcessRunner
                .CreateStartInfo(
                    invocation);

        Assert.False(
            startInfo.UseShellExecute);
        Assert.True(
            startInfo.RedirectStandardOutput);
        Assert.True(
            startInfo.RedirectStandardError);
        Assert.Equal(
            "python",
            startInfo.FileName);
        Assert.Equal(
            invocation.Arguments,
            startInfo.ArgumentList
                .ToArray());
        Assert.DoesNotContain(
            startInfo.FileName,
            new[]
            {
                "cmd.exe",
                "powershell",
                "bash",
                "sh"
            });
    }

    [Fact]
    public void Prior_failure_stops_current_and_vs08()
    {
        using var fixture =
            new Fixture();
        var runner =
            new RecordingRunner(
                (_, _) =>
                    1);

        var result =
            fixture.Run(
                fixture.Options(),
                runner);

        Assert.Equal(
            1,
            result.ExitCode);
        Assert.Single(
            runner.Invocations);
    }

    [Fact]
    public void Df06_exit_two_is_failure_and_partial_artifact_is_preserved()
    {
        using var fixture =
            new Fixture();
        var partial =
            Path.Combine(
                fixture.WorkDirectory,
                "prior-structured.json");
        var runner =
            new RecordingRunner(
                (_, index) =>
                {
                    if (index == 0)
                    {
                        Directory.CreateDirectory(
                            fixture.WorkDirectory);
                        File.WriteAllText(
                            partial,
                            "{}");
                        return 2;
                    }

                    return 0;
                });

        var result =
            fixture.Run(
                fixture.Options(),
                runner);

        Assert.Equal(
            1,
            result.ExitCode);
        Assert.Single(
            runner.Invocations);
        Assert.True(
            File.Exists(
                partial));
    }

    [Fact]
    public void Current_failure_stops_before_vs08()
    {
        using var fixture =
            new Fixture();
        var runner =
            new RecordingRunner(
                (invocation, index) =>
                {
                    if (index == 0)
                    {
                        WriteProviderArtifacts(
                            invocation);
                        return 0;
                    }

                    return 9;
                });

        var result =
            fixture.Run(
                fixture.Options(),
                runner);

        Assert.Equal(
            1,
            result.ExitCode);
        Assert.Equal(
            2,
            runner.Invocations.Count);
        Assert.DoesNotContain(
            runner.Invocations,
            item =>
                string.Equals(
                    item.FileName,
                    "dotnet",
                    StringComparison.Ordinal));
    }

    [Fact]
    public void Consumer_manifest_is_copied_unchanged_and_vs08_arguments_are_exact()
    {
        using var fixture =
            new Fixture();
        var sourceBytes =
            File.ReadAllBytes(
                fixture.ConsumerManifest);
        var runner =
            fixture.SuccessfulRunner();

        var result =
            fixture.Run(
                fixture.Options(),
                runner);

        Assert.Equal(
            0,
            result.ExitCode);

        var runtimeManifest =
            Path.Combine(
                fixture.WorkDirectory,
                "manifest.json");

        Assert.Equal(
            sourceBytes,
            File.ReadAllBytes(
                runtimeManifest));

        var consumer =
            runner.Invocations[2];

        Assert.Equal(
            "dotnet",
            consumer.FileName);
        Assert.Equal(
            fixture.TradeOpsRoot,
            consumer.WorkingDirectory);
        Assert.Equal(
            new[]
            {
                "run",
                "--project",
                "tools/TradeOps.TranscriptResearchDemo",
                "--",
                "--manifest",
                runtimeManifest,
                "--policy",
                fixture.Policy,
                "--json",
                Path.Combine(
                    fixture.WorkDirectory,
                    "transcript-research-result.json")
            },
            consumer.Arguments);
    }

    [Fact]
    public void Vs08_failure_propagates_nonzero()
    {
        using var fixture =
            new Fixture();
        var runner =
            new RecordingRunner(
                (invocation, index) =>
                {
                    if (index < 2)
                    {
                        WriteProviderArtifacts(
                            invocation);
                        return 0;
                    }

                    return 5;
                });

        var result =
            fixture.Run(
                fixture.Options(),
                runner);

        Assert.Equal(
            1,
            result.ExitCode);
        Assert.Equal(
            3,
            runner.Invocations.Count);
    }

    [Fact]
    public void Success_requires_all_four_provider_artifacts()
    {
        using var fixture =
            new Fixture();
        var runner =
            new RecordingRunner(
                (invocation, index) =>
                {
                    if (index == 0)
                    {
                        WriteProviderArtifacts(
                            invocation);
                    }
                    else if (index == 1)
                    {
                        WriteArgumentFile(
                            invocation,
                            "--output-normalized-json",
                            "{}");
                    }

                    return 0;
                });

        var result =
            fixture.Run(
                fixture.Options(),
                runner);

        Assert.Equal(
            1,
            result.ExitCode);
        Assert.Equal(
            2,
            runner.Invocations.Count);
    }

    [Fact]
    public void Success_requires_nonempty_final_json_and_success_returns_zero()
    {
        using var fixture =
            new Fixture();

        var emptyRunner =
            new RecordingRunner(
                (invocation, index) =>
                {
                    if (index < 2)
                    {
                        WriteProviderArtifacts(
                            invocation);
                    }
                    else
                    {
                        WriteArgumentFile(
                            invocation,
                            "--json",
                            string.Empty);
                    }

                    return 0;
                });

        var emptyResult =
            fixture.Run(
                fixture.Options(),
                emptyRunner);

        Assert.Equal(
            1,
            emptyResult.ExitCode);

        using var successFixture =
            new Fixture();
        var successRunner =
            successFixture.SuccessfulRunner();
        var success =
            successFixture.Run(
                successFixture.Options(),
                successRunner);

        Assert.Equal(
            0,
            success.ExitCode);
        Assert.True(
            new FileInfo(
                    Path.Combine(
                        successFixture.WorkDirectory,
                        "transcript-research-result.json"))
                .Length >
            0);
    }

    [Theory]
    [InlineData(
        "samples")]
    [InlineData(
        "schemas")]
    public void Work_directory_must_not_write_generated_output_into_committed_input_directories(
        string committedDirectory)
    {
        using var fixture =
            new Fixture();
        var runner =
            new RecordingRunner();
        var blocked =
            Path.Combine(
                fixture.TradeOpsRoot,
                committedDirectory,
                "generated");

        var result =
            fixture.Run(
                fixture.Options(
                    workDirectory:
                        blocked),
                runner);

        Assert.Equal(
            1,
            result.ExitCode);
        Assert.Empty(
            runner.Invocations);
        Assert.False(
            Directory.Exists(
                blocked));
    }

    [Fact]
    public void New_tool_has_no_sdk_http_docflow_dependency_or_duplicate_decision_logic()
    {
        var root =
            FindRepositoryRoot();
        var project =
            File.ReadAllText(
                Path.Combine(
                    root,
                    "tools",
                    "TradeOps.ProviderTranscriptResearchDemo",
                    "TradeOps.ProviderTranscriptResearchDemo.csproj"));
        var source =
            File.ReadAllText(
                Path.Combine(
                    root,
                    "tools",
                    "TradeOps.ProviderTranscriptResearchDemo",
                    "ProviderTranscriptResearchDemo.cs"));

        Assert.DoesNotContain(
            "<ProjectReference",
            project,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "<PackageReference",
            project,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "HttpClient",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "TranscriptResearchDecisionDemoService",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "DeterministicEarningsDecisionRule",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ResearchDecisionAction",
            source,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
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
            "TradeOps repository root not found.");
    }

    private static void WriteProviderArtifacts(
        ChildProcessInvocation invocation)
    {
        WriteArgumentFile(
            invocation,
            "--output-normalized-json",
            "{}");
        WriteArgumentFile(
            invocation,
            "--output-structured-json",
            "{}");
    }

    private static void WriteArgumentFile(
        ChildProcessInvocation invocation,
        string option,
        string content)
    {
        var index =
            invocation.Arguments
                .ToList()
                .IndexOf(
                    option);

        Assert.True(
            index >= 0
            && index + 1 <
            invocation.Arguments.Count);

        var path =
            invocation.Arguments[index + 1];
        var directory =
            Path.GetDirectoryName(
                path);

        if (!string.IsNullOrWhiteSpace(
                directory))
        {
            Directory.CreateDirectory(
                directory);
        }

        File.WriteAllText(
            path,
            content);
    }

    private sealed class RecordingRunner :
        IChildProcessRunner
    {
        private readonly Func<ChildProcessInvocation, int, int> _handler;

        public RecordingRunner(
            Func<ChildProcessInvocation, int, int>? handler = null)
        {
            _handler =
                handler
                ?? ((_, _) =>
                    0);
        }

        public List<ChildProcessInvocation> Invocations
        {
            get;
        } =
            new();

        public int Run(
            ChildProcessInvocation invocation)
        {
            var index =
                Invocations.Count;
            Invocations.Add(
                invocation);

            return _handler(
                invocation,
                index);
        }
    }

    private sealed class EnvironmentReader :
        IEnvironmentReader
    {
        private readonly string? _apiKey;

        public EnvironmentReader(
            string? apiKey)
        {
            _apiKey =
                apiKey;
        }

        public string? Get(
            string name) =>
            string.Equals(
                name,
                ProviderTranscriptResearchDemoHarness
                    .ApiKeyEnvironmentVariable,
                StringComparison.Ordinal)
                ? _apiKey
                : null;
    }

    private sealed class Fixture :
        IDisposable
    {
        public Fixture()
        {
            Root =
                Path.Combine(
                    Path.GetTempPath(),
                    "tradeops-vs10-" +
                    Guid.NewGuid()
                        .ToString(
                            "N"));
            TradeOpsRoot =
                Path.Combine(
                    Root,
                    "TradeOps");
            DocFlowRoot =
                Path.Combine(
                    Root,
                    "DocFlow");
            WorkDirectory =
                Path.Combine(
                    Root,
                    "work");

            Directory.CreateDirectory(
                TradeOpsRoot);
            File.WriteAllText(
                Path.Combine(
                    TradeOpsRoot,
                    "TradeOps.sln"),
                string.Empty);

            PriorRaw =
                CreateTradeOpsFile(
                    "samples",
                    "research",
                    "provider-transcript-demo",
                    "prior-raw.json");
            CurrentRaw =
                CreateTradeOpsFile(
                    "samples",
                    "research",
                    "provider-transcript-demo",
                    "current-raw.json");
            ConsumerManifest =
                CreateTradeOpsFile(
                    "samples",
                    "research",
                    "provider-transcript-demo",
                    "consumer-manifest.json",
                    "{\n  \"schemaVersion\": 1\n}\n");
            SchemaRequest =
                CreateTradeOpsFile(
                    "schemas",
                    "research",
                    "earnings-transcript-facts-v1.schema-request.json");
            Policy =
                CreateTradeOpsFile(
                    "samples",
                    "research",
                    "transcript-research",
                    "policy.json");

            DocFlowEntryPoint =
                Path.Combine(
                    DocFlowRoot,
                    "src",
                    "DocFlow.Extraction.Worker",
                    "text_artifact_main.py");
            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    DocFlowEntryPoint)!);
            File.WriteAllText(
                DocFlowEntryPoint,
                string.Empty);
        }

        public string Root
        {
            get;
        }

        public string TradeOpsRoot
        {
            get;
        }

        public string DocFlowRoot
        {
            get;
        }

        public string DocFlowEntryPoint
        {
            get;
        }

        public string WorkDirectory
        {
            get;
        }

        public string PriorRaw
        {
            get;
        }

        public string CurrentRaw
        {
            get;
        }

        public string ConsumerManifest
        {
            get;
        }

        public string SchemaRequest
        {
            get;
        }

        public string Policy
        {
            get;
        }

        public ProviderTranscriptResearchDemoOptions Options(
            string? docFlowRoot = null,
            string model = "test-model",
            string python = "python",
            string? workDirectory = null) =>
            new(
                docFlowRoot
                ?? DocFlowRoot,
                model,
                python,
                workDirectory
                ?? WorkDirectory);

        public RecordingRunner SuccessfulRunner() =>
            new(
                (invocation, index) =>
                {
                    if (index < 2)
                    {
                        WriteProviderArtifacts(
                            invocation);
                    }
                    else
                    {
                        WriteArgumentFile(
                            invocation,
                            "--json",
                            "{}");
                    }

                    return 0;
                });

        public RunResult Run(
            ProviderTranscriptResearchDemoOptions options,
            RecordingRunner runner,
            string? apiKey = "test-key")
        {
            using var output =
                new StringWriter();
            using var error =
                new StringWriter();

            var exitCode =
                new ProviderTranscriptResearchDemoHarness(
                        runner,
                        new EnvironmentReader(
                            apiKey))
                    .Run(
                        options,
                        TradeOpsRoot,
                        output,
                        error);

            return new RunResult(
                exitCode,
                output.ToString(),
                error.ToString());
        }

        public void Dispose()
        {
            if (Directory.Exists(
                    Root))
            {
                Directory.Delete(
                    Root,
                    recursive:
                        true);
            }
        }

        private string CreateTradeOpsFile(
            params string[] segments) =>
            CreateTradeOpsFile(
                segments,
                "{}");

        private string CreateTradeOpsFile(
            string first,
            string second,
            string third,
            string fourth,
            string content) =>
            CreateTradeOpsFile(
                new[]
                {
                    first,
                    second,
                    third,
                    fourth
                },
                content);

        private string CreateTradeOpsFile(
            string[] segments,
            string content)
        {
            var path =
                segments.Aggregate(
                    TradeOpsRoot,
                    Path.Combine);
            var directory =
                Path.GetDirectoryName(
                    path)!;
            Directory.CreateDirectory(
                directory);
            File.WriteAllText(
                path,
                content);

            return Path.GetFullPath(
                path);
        }
    }

    private sealed record RunResult(
        int ExitCode,
        string Output,
        string Error);
}
