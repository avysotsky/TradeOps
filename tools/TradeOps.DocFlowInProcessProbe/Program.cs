using System.Diagnostics;
using Python.Runtime;

// Feasibility probe only. Never register this with production TradeOps DI.
// Requires explicit PYTHONNET_PYDLL and Python 3.11 shared library.
var pythonDll = Environment.GetEnvironmentVariable("PYTHONNET_PYDLL");
if (string.IsNullOrWhiteSpace(pythonDll))
{
    Console.Error.WriteLine("ARCH04: missing explicit PYTHONNET_PYDLL");
    return 2;
}

try
{
    Runtime.PythonDLL = pythonDll;
    PythonEngine.Initialize();
    try
    {
        using (Py.GIL())
        {
            using var os = Py.Import("os");
            using var pidObject = os.InvokeMethod("getpid");
            var pythonPid = pidObject.As<int>();
            var dotnetPid = Environment.ProcessId;
            if (pythonPid != dotnetPid)
                throw new InvalidOperationException("Python and .NET PIDs differ.");
            PythonEngine.Exec("import asyncio\nasync def arch04_probe_coroutine():\n    return os.getpid()");
            using var asyncPidObject = PythonEngine.Eval("asyncio.run(arch04_probe_coroutine())");
            var asyncPid = asyncPidObject.As<int>();
            if (asyncPid != dotnetPid)
                throw new InvalidOperationException("Python coroutine escaped the .NET process.");
            Console.WriteLine($"ARCH04 SAME_PID PASS dotnet={dotnetPid} python={pythonPid} asyncPython={asyncPid}");
        }
    }
    finally
    {
        PythonEngine.Shutdown();
    }

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"ARCH04 probe failed: {ex.GetType().Name}");
    return 1;
}
