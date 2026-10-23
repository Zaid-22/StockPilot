using Xunit;

using StockPilot.IntegrationTests;
using Xunit.Abstractions;
using Xunit.Sdk;

// Disable file watching for this short-lived verification process.
Environment.SetEnvironmentVariable("DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE", "false");
Console.WriteLine("Starting in-process xUnit runner.");
var sink = new ResultSink();
using var framework = new XunitTestFramework(sink);
using var executor = framework.GetExecutor(typeof(AuthenticationTests).Assembly.GetName());
var options = new RunnerOptions();
options.SetValue("xunit.execution.DisableParallelization", true);
Console.WriteLine("Discovering and running tests.");
executor.RunAll(sink, options, options);
var failures = await sink.Completion.Task.WaitAsync(TimeSpan.FromMinutes(5));
return failures == 0 ? 0 : 1;

sealed class RunnerOptions : ITestFrameworkDiscoveryOptions, ITestFrameworkExecutionOptions
{
    private readonly Dictionary<string, object?> values = [];
    public T GetValue<T>(string name) => values.TryGetValue(name, out var value) ? (T)value! : default!;
    public void SetValue<T>(string name, T value) => values[name] = value;
}

sealed class ResultSink : LongLivedMarshalByRefObject, IMessageSink
{
    private int errors;
    public TaskCompletionSource<int> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool OnMessage(IMessageSinkMessage message)
    {
        if (message is ITestStarting starting) Console.WriteLine($"RUN {starting.Test.DisplayName}");
        if (message is ITestPassed passed) Console.WriteLine($"PASS {passed.Test.DisplayName}");
        if (message is ITestFailed failed) Console.WriteLine($"FAIL {failed.Test.DisplayName}: {string.Join("; ", failed.Messages)}");
        if (message is IErrorMessage error)
        {
            Interlocked.Increment(ref errors);
            Console.WriteLine($"ERROR {string.Join("; ", error.Messages)}");
        }
        if (message is ITestAssemblyFinished finished)
        {
            Console.WriteLine($"Result: {finished.TestsRun} run, {finished.TestsFailed} failed, {finished.TestsSkipped} skipped.");
            Completion.TrySetResult(finished.TestsFailed + errors + (finished.TestsRun == 0 ? 1 : 0));
        }
        return true;
    }
}
