using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using StructuralLoadAssistant.Csi.Abstractions;
namespace StructuralLoadAssistant.Csi.ApiV1;

/// <summary>Read-only boundary. Implementations update only by-reference getter outputs.</summary>
public interface ICsiApiSession : IDisposable { object? Read(string component, string getter, object?[] args); }
public interface ICsiApiProcessSession : ICsiApiSession { int? ProcessId { get; } }

internal sealed class InstalledSession : ICsiApiProcessSession
{
    private readonly Dictionary<string,(object Instance,Type Contract)> objects=new();
    private readonly List<object> releases=new();
    private readonly Assembly assembly;
    private readonly string ns;
    private readonly DateTimeOffset? processStartTime;
    private readonly CsiProduct product;
    public int? ProcessId { get; }
    private static readonly HashSet<string> Allowed=new(StringComparer.Ordinal) {"GetVersion","GetModelFilename","GetPresentUnits","GetModelIsLocked","GetNameList","GetStories","GetSelected","GetPoints","GetCoordCartesian","GetTransformationMatrix","GetLoadDistributed","GetLoadUniform"};
    public InstalledSession(CsiConnectionOptions options)
    {
        if(!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("CSI running-instance attachment requires Windows.");
        product=options.Product;
        var path=Path.GetFullPath(options.AssemblyPath);
        ns=options.Product==CsiProduct.Etabs?"ETABSv1":"SAP2000v1";
        // A CSiAPIv1 installation path is accepted, but the inspected product-specific interop
        // contract is loaded beside it; never bind an uninspected generic interface version.
        if(Path.GetFileName(path).Equals("CSiAPIv1.dll",StringComparison.OrdinalIgnoreCase)) path=Path.Combine(Path.GetDirectoryName(path)!,ns+".dll");
        assembly=Assembly.LoadFrom(path);
        if(assembly.GetName().Name!=ns)throw new NotSupportedException("Assembly does not match the selected CSI product.");
        try {
            var helper=Activator.CreateInstance(assembly.GetType(ns+".Helper",true)!)!; releases.Add(helper);
            var progId=options.Product==CsiProduct.Etabs?"CSI.ETABS.API.ETABSObject":"CSI.SAP2000.API.SapObject";
            object? api=null;
            if(options.ProcessId.HasValue)
            {
                CsiProcessDiscovery.ValidateProcessActive(options.Product, options.ProcessId.Value);
                var getObjProc=assembly.GetType(ns+".cHelper",true)!.GetMethod("GetObjectProcess");
                if(getObjProc!=null) api=getObjProc.Invoke(helper, new object[]{progId, options.ProcessId.Value});
                else throw new NotSupportedException("GetObjectProcess is not available in installed helper.");
                if(api==null) throw new CsiNotRunningException();
                ProcessId=options.ProcessId.Value;
            }
            else
            {
                var running=CsiProcessDiscovery.GetRunningProcesses(options.Product);
                if(running.Length==0) throw new CsiNotRunningException();
                if(running.Length>1) throw new InvalidOperationException($"Multiple {options.Product} instances are running (PIDs: {string.Join(", ", running.Select(p=>p.ProcessId))}). Explicitly select a process ID to attach safely.");
                var targetPid=running[0].ProcessId;
                var getObjProc=assembly.GetType(ns+".cHelper",true)!.GetMethod("GetObjectProcess");
                if(getObjProc!=null) api=getObjProc.Invoke(helper, new object[]{progId, targetPid});
                if(api==null) api=assembly.GetType(ns+".cHelper",true)!.GetMethod("GetObject")!.Invoke(helper,new object[]{progId});
                if(api==null) throw new CsiNotRunningException();
                ProcessId=targetPid;
            }
            if(ProcessId.HasValue)
            {
                try { using var p=System.Diagnostics.Process.GetProcessById(ProcessId.Value); processStartTime=(DateTimeOffset)p.StartTime; }
                catch { /* process info cache best effort */ }
            }
            releases.Add(api);
            var model=assembly.GetType(ns+".cOAPI",true)!.GetProperty("SapModel")!.GetValue(api)!; releases.Add(model);
            objects.Add("",(model,assembly.GetType(ns+".cSapModel",true)!));
        } catch { Dispose(); throw; }
    }
    public object? Read(string component,string getter,object?[] args)
    {
        if(!Allowed.Contains(getter)) throw new InvalidOperationException("Only inspected read getters are permitted.");
        if(ProcessId.HasValue) CsiProcessDiscovery.ValidateProcessActive(product, ProcessId.Value, processStartTime);
        if(!objects.TryGetValue(component,out var target)) {
            var model=objects[""]; var p=model.Contract.GetProperty(component) ?? throw new NotSupportedException(component);
            var instance=p.GetValue(model.Instance)!; releases.Add(instance); target=(instance,p.PropertyType); objects.Add(component,target);
        }
        var m=target.Contract.GetMethod(getter) ?? throw new NotSupportedException(component+"."+getter);
        var parameters=m.GetParameters();
        ValidateSignature(component,getter,parameters);
        if(parameters.Length!=args.Length)throw new NotSupportedException("Installed getter parameter count differs from the inspected API contract.");
        for(var i=0;i<args.Length;i++) if(parameters[i].ParameterType.IsEnum && args[i] is int n) args[i]=Enum.ToObject(parameters[i].ParameterType,n);
        try{return m.Invoke(target.Instance,args);}catch(TargetInvocationException e) when(e.InnerException!=null){throw e.InnerException;}
    }
    private static void ValidateSignature(string component,string getter,ParameterInfo[] parameters)
    {
        string expected=getter switch {
            "GetVersion"=>"String&,Double&", "GetModelFilename"=>"Boolean", "GetPresentUnits" or "GetModelIsLocked"=>"",
            "GetNameList"=>component=="LoadCases"?"Int32&,String[]&,eLoadCaseType":"Int32&,String[]&",
            "GetStories"=>"Int32&,String[]&,Double[]&,Double[]&,Boolean[]&,String[]&,Boolean[]&,Double[]&",
            "GetSelected"=>"Int32&,Int32[]&,String[]&",
            "GetPoints"=>component=="FrameObj"?"String,String&,String&":"String,Int32&,String[]&",
            "GetCoordCartesian"=>"String,Double&,Double&,Double&,String",
            "GetTransformationMatrix"=>"String,Double[]&,Boolean",
            "GetLoadDistributed"=>"String,Int32&,String[]&,String[]&,Int32[]&,String[]&,Int32[]&,Double[]&,Double[]&,Double[]&,Double[]&,Double[]&,Double[]&,eItemType",
            "GetLoadUniform"=>"String,Int32&,String[]&,String[]&,String[]&,Int32[]&,Double[]&,eItemType",
            _=>throw new NotSupportedException("Uninspected getter.")
        };
        if(string.Join(",",parameters.Select(p=>p.ParameterType.Name))!=expected)throw new NotSupportedException("Installed getter signature differs from the inspected API contract.");
    }
    public void Dispose() { for(var i=releases.Count-1;i>=0;i--) if(OperatingSystem.IsWindows() && Marshal.IsComObject(releases[i]))Marshal.ReleaseComObject(releases[i]); releases.Clear();objects.Clear(); }
}

/// <summary>Owns the COM apartment, including connection, getters and release. No COM work uses ThreadPool.</summary>
internal sealed class CsiDispatcher : IAsyncDisposable
{
    private readonly BlockingCollection<Action> work=new();
    private readonly Thread thread;
    private int disposed;
    public CsiDispatcher(){thread=new Thread(Run){IsBackground=true,Name="CSI read-only STA"};if(OperatingSystem.IsWindows())thread.SetApartmentState(ApartmentState.STA);thread.Start();}
    private void Run(){while(!work.IsCompleted){if(work.TryTake(out var action,15))action();if(OperatingSystem.IsWindows())while(PeekMessage(out var msg,IntPtr.Zero,0,0,1)){TranslateMessage(ref msg);DispatchMessage(ref msg);}}}
    public Task<T> Invoke<T>(Func<T> action,CancellationToken token=default){if(Volatile.Read(ref disposed)!=0)throw new ObjectDisposedException(nameof(CsiDispatcher));var done=new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);work.Add(()=>{if(token.IsCancellationRequested){done.TrySetCanceled(token);return;}try{done.TrySetResult(action());}catch(Exception e){done.TrySetException(e);}});return done.Task;}
    public ValueTask DisposeAsync(){if(Interlocked.Exchange(ref disposed,1)==0){work.CompleteAdding();thread.Join();work.Dispose();}return ValueTask.CompletedTask;}
    [StructLayout(LayoutKind.Sequential)] private struct Message { public IntPtr Hwnd;public uint Id;public UIntPtr WParam;public IntPtr LParam;public uint Time;public int X,Y;public uint Private; }
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool PeekMessage(out Message message,IntPtr hwnd,uint min,uint max,uint remove);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref Message message);
}



internal sealed class CsiNotRunningException : Exception { public CsiNotRunningException():base("No running CSI instance was found."){} }
