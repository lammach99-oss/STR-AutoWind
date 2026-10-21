using System.Collections.Immutable;
using System.Diagnostics;
using StructuralLoadAssistant.Csi.Abstractions;

namespace StructuralLoadAssistant.Csi.ApiV1;

public static class CsiProcessDiscovery
{
    public static ImmutableArray<CsiProcessInfo> GetRunningProcesses(CsiProduct product)
    {
        if (!OperatingSystem.IsWindows()) return [];
        var processName = product switch
        {
            CsiProduct.Etabs => "ETABS",
            CsiProduct.Sap2000 => "SAP2000",
            _ => throw new ArgumentOutOfRangeException(nameof(product))
        };

        var results = new List<CsiProcessInfo>();
        foreach (var proc in Process.GetProcessesByName(processName))
        {
            try
            {
                if (!proc.HasExited)
                {
                    results.Add(new CsiProcessInfo(proc.Id, product, proc.MainWindowTitle, proc.StartTime));
                }
            }
            catch
            {
                // Process may have exited between GetProcesses and accessing properties
            }
            finally
            {
                proc.Dispose();
            }
        }
        return results.OrderBy(p => p.ProcessId).ToImmutableArray();
    }

    public static void ValidateProcessActive(CsiProduct product, int processId, DateTimeOffset? expectedStartTime = null)
    {
        if (!OperatingSystem.IsWindows()) return;
        var expectedName = product switch
        {
            CsiProduct.Etabs => "ETABS",
            CsiProduct.Sap2000 => "SAP2000",
            _ => throw new ArgumentOutOfRangeException(nameof(product))
        };

        Process proc;
        try
        {
            proc = Process.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException($"The connected {product} process (PID {processId}) has terminated.");
        }

        using (proc)
        {
            if (proc.HasExited)
            {
                throw new InvalidOperationException($"The connected {product} process (PID {processId}) has exited.");
            }

            if (!string.Equals(proc.ProcessName, expectedName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Process ID {processId} is no longer {expectedName} (PID was reused by '{proc.ProcessName}').");
            }

            if (expectedStartTime.HasValue)
            {
                var actualStart = (DateTimeOffset)proc.StartTime;
                if (Math.Abs((actualStart - expectedStartTime.Value).TotalSeconds) > 2.0)
                {
                    throw new InvalidOperationException($"Process ID {processId} start time mismatch (PID was reused).");
                }
            }
        }
    }
}
