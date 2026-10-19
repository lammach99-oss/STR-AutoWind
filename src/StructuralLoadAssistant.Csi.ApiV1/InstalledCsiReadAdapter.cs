using System.Collections.Immutable;
using System.Globalization;
using StructuralLoadAssistant.Csi.Abstractions;
using StructuralLoadAssistant.Domain;
namespace StructuralLoadAssistant.Csi.ApiV1;

public static class CsiUnitConverter
{
 public static CsiUnits FromId(int id) {
  (double length,double force,string name)=id switch {
   1=>(.0254,4.4482216152605,"lb_in_F"),2=>(.3048,4.4482216152605,"lb_ft_F"),3=>(.0254,4448.2216152605,"kip_in_F"),4=>(.3048,4448.2216152605,"kip_ft_F"),
   5=>(.001,1000,"kN_mm_C"),6=>(1,1000,"kN_m_C"),7=>(.001,9.80665,"kgf_mm_C"),8=>(1,9.80665,"kgf_m_C"),9=>(.001,1,"N_mm_C"),10=>(1,1,"N_m_C"),11=>(.001,9806.65,"Ton_mm_C"),12=>(1,9806.65,"Ton_m_C"),13=>(.01,1000,"kN_cm_C"),14=>(.01,9.80665,"kgf_cm_C"),15=>(.01,1,"N_cm_C"),16=>(.01,9806.65,"Ton_cm_C"),_=>throw new ArgumentOutOfRangeException(nameof(id),"Unknown CSI present units; geometry and loads cannot be interpreted.")};
  return new(id.ToString(CultureInfo.InvariantCulture),length,force,name);
 }
}

/// <summary>Optional installed ETABS 23 / SAP2000 27 read adapter. Signature inspection is not physical qualification.</summary>
public sealed class InstalledCsiReadAdapter : ICsiSessionReadAdapter
{
 private readonly Func<CsiConnectionOptions,ICsiApiSession> factory;
 private readonly CsiDispatcher dispatcher=new();
 private ICsiApiSession? session;
 private CsiProduct product;
 private string version="",modelId=""; private string unsavedIdentity=Guid.NewGuid().ToString("N");
 private CsiCapabilities capabilities=new(false,false,false,false);
 private int disposed;
 public InstalledCsiReadAdapter(Func<CsiConnectionOptions,ICsiApiSession>? factory=null)=>this.factory=factory ?? (o=>new InstalledSession(o));
 private object? Get(string component,string method,params object?[] args){try{return session!.Read(component,method,args);}catch(NotSupportedException){capabilities=method switch{"GetStories"=>capabilities with{Stories=false},"GetLoadDistributed"=>capabilities with{FrameDistributedLoads=false},"GetLoadUniform"=>capabilities with{AreaUniformLoads=false},"GetTransformationMatrix"=>capabilities with{LocalAxes=false},_=>capabilities};throw;}}
 private void Check(string component,string method,object?[] args){var code=Convert.ToInt32(Get(component,method,args),CultureInfo.InvariantCulture);if(code!=0)throw new ReadFailure("CSI_RETURN_CODE",component+"."+method,"CSI getter returned nonzero status.",code);}
 public Task<CsiConnectionResult> ConnectAsync(CsiConnectionOptions options,CancellationToken cancellationToken=default)=>dispatcher.Invoke(()=>{
  session?.Dispose();session=null;version="";modelId="";product=options.Product; capabilities=new(false,false,false,false);
  try {if(!Enum.IsDefined(options.Product))throw new ArgumentOutOfRangeException(nameof(options.Product)); unsavedIdentity=Guid.NewGuid().ToString("N");session=factory(options);object?[] args={"",0d};Check("","GetVersion",args);version=(string)args[0]!;
   var expected=product==CsiProduct.Etabs?23:27;
   if(!int.TryParse(version.Split('.')[0],out var major)||major!=expected)throw new ReadFailure("UNSUPPORTED_VERSION","GetVersion",$"Only inspected {product} {expected} signatures are supported; physical qualification remains pending.");
   modelId=(string)Get("","GetModelFilename",true)!;
   _=CsiUnitConverter.FromId(Convert.ToInt32(Get("","GetPresentUnits"),CultureInfo.InvariantCulture));
   capabilities=new(product==CsiProduct.Etabs,true,true,true);
   return new CsiConnectionResult(true,product.ToString(),version,capabilities,[new("PHYSICAL_QUALIFICATION_PENDING","Connect","Installed signatures inspected; live model read qualification is not established.",CsiIssueSeverity.Information)]);
  }catch(OperationCanceledException){session?.Dispose();session=null;throw;}catch(Exception e){session?.Dispose();session=null;return new CsiConnectionResult(false,product.ToString(),version,capabilities,[Issue(e,"Connect")]);}
 },cancellationToken);
 public Task<CsiReadScan> RefreshSelectionAsync(CancellationToken cancellationToken=default)=>dispatcher.Invoke(()=>Scan(cancellationToken),cancellationToken);
 public async Task<CsiModelSnapshot> ScanModelAsync(CancellationToken cancellationToken=default)=>(await RefreshSelectionAsync(cancellationToken)).Model;
 public async Task<ImmutableArray<ExistingLoad>> GetExistingLoadsAsync(CancellationToken cancellationToken=default)=>(await RefreshSelectionAsync(cancellationToken)).Model.ExistingLoads;
 private CsiReadScan Scan(CancellationToken token)
 {
  var issues=new List<CsiReadIssue>();var frames=new List<FrameGeometry>();var shells=new List<ShellGeometry>();var metadata=new List<CsiAreaReadInfo>();var stories=new List<CsiStory>();var loads=new List<ExistingLoad>();
  var units=new CsiUnits("unknown",0,0,"Not read");bool locked=false;ImmutableArray<string> patterns=[],cases=[],combos=[];
  try {
   if(session==null)throw new ReadFailure("DISCONNECTED","RefreshSelection","Attach a running supported CSI instance first.");
   token.ThrowIfCancellationRequested();units=CsiUnitConverter.FromId(Convert.ToInt32(Get("","GetPresentUnits"),CultureInfo.InvariantCulture));locked=(bool)Get("","GetModelIsLocked")!;
   var beforeModel=(string)Get("","GetModelFilename",true)!;
   patterns=Names("LoadPatterns",issues);cases=Names("LoadCases",issues,true);combos=Names("RespCombo",issues);
   if(capabilities.Stories)Try(()=>{object?[] a={0,null,null,null,null,null,null,null};Check("Story","GetStories",a);int n=checked((int)a[0]!+1);var names=ArrayOf<string>(a[1],n);var elev=ArrayOf<double>(a[2],n);var h=ArrayOf<double>(a[3],n);for(int i=0;i<n;i++)stories.Add(new(names[i],Finite(elev[i])*units.LengthToM,Finite(h[i])*units.LengthToM));},issues);
   object?[] selected={0,null,null};Check("SelectObj","GetSelected",selected);int count=(int)selected[0]!;var types=ArrayOf<int>(selected[1],count);var names=ArrayOf<string>(selected[2],count);
   foreach(var item in Enumerable.Range(0,count).OrderBy(i=>names[i],StringComparer.Ordinal)) {
    token.ThrowIfCancellationRequested();var id=names[item];
    if(types[item]==2)Try(()=>{object?[] a={id,"",""};Check("FrameObj","GetPoints",a);var start=Point((string)a[1]!,units);var end=Point((string)a[2]!,units);if((end-start).Length<1e-9)throw new InvalidDataException("Zero-length frame."); ReadAxesOrUnknown("FrameObj",id,issues); frames.Add(new(id,"unassigned",start,end));ReadLoads("FrameObj",id,units,loads);},issues);
    else if(types[item]==5)Try(()=>{object?[] a={id,0,null};Check("AreaObj","GetPoints",a);int n=(int)a[1]!;if(n<3)throw new InvalidDataException("Area requires at least three points.");var vertices=ArrayOf<string>(a[2],n).Select(p=>Point(p,units)).ToImmutableArray();ValidatePolygon(vertices);var axis=ReadAxesOrUnknown("AreaObj",id,issues);var key=axis.Length<1e-9?"unknown-plane:"+id:PlaneKey(axis,vertices[0]);shells.Add(new(id,key,vertices,axis,default));metadata.Add(new(id,key,null,"Unknown physical outward normal; local 3 is only an object axis."));ReadLoads("AreaObj",id,units,loads);},issues);
   }
   // Detect units/model changes during a scan without ever changing present units.
   if((string?)Get("","GetModelFilename",true)!=beforeModel || Convert.ToInt32(Get("","GetPresentUnits"),CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)!=units.NativeUnitId)throw new ReadFailure("STALE_SCAN","RefreshSelection","Model identity or units changed during the scan; refresh again.");
   modelId=string.IsNullOrWhiteSpace(beforeModel)?"unsaved:"+unsavedIdentity:beforeModel;
  } catch(OperationCanceledException){throw;} catch(Exception e){issues.Add(Issue(e,"RefreshSelection"));frames.Clear();shells.Clear();metadata.Clear();loads.Clear();stories.Clear();patterns=[];cases=[];combos=[];units=new("unknown",0,0,"Scan invalidated");locked=false;modelId="";}
  var geometry=SnapshotJson.Hash(new{frames,shells});var loadHash=SnapshotJson.Hash(loads);var snapshot=new CsiModelSnapshot(SnapshotJson.Hash(new{modelId,geometry,loadHash}),modelId,product.ToString(),version,geometry,loadHash,patterns,cases,combos,frames.ToImmutableArray(),shells.ToImmutableArray(),loads.ToImmutableArray());
  var outcome = issues.Any(i => i.Severity == CsiIssueSeverity.Error)
      ? CsiScanOutcome.Failed
      : (frames.Count == 0 && shells.Count == 0)
          ? CsiScanOutcome.EmptySelection
          : issues.Any(i => i.Severity == CsiIssueSeverity.Warning)
              ? CsiScanOutcome.PartialScan
              : CsiScanOutcome.Succeeded;
  return new(snapshot,units,locked,capabilities,stories.ToImmutableArray(),metadata.ToImmutableArray(),issues.ToImmutableArray(),outcome);
 }
 private ImmutableArray<string> Names(string component,List<CsiReadIssue> issues,bool caseType=false){ImmutableArray<string> result=[];Try(()=>{object?[] a=caseType?[0,null,0]:[0,null];Check(component,"GetNameList",a);result=ArrayOf<string>(a[1],(int)a[0]!).OrderBy(x=>x,StringComparer.Ordinal).ToImmutableArray();},issues);return result;}
 private Vec3 Point(string id,CsiUnits u){object?[] a={id,0d,0d,0d,"Global"};Check("PointObj","GetCoordCartesian",a);return new(Finite((double)a[1]!)*u.LengthToM,Finite((double)a[2]!)*u.LengthToM,Finite((double)a[3]!)*u.LengthToM);}
   private Vec3 ReadAxesOrUnknown(string component,string id,List<CsiReadIssue> issues){try{return ReadAxes(component,id);}catch(OperationCanceledException){throw;}catch(Exception e){capabilities=capabilities with{LocalAxes=false};issues.Add(new("UNSUPPORTED_GETTER",component+".GetTransformationMatrix",e.Message,CsiIssueSeverity.Warning));return default;}}
 private Vec3 ReadAxes(string component,string id){object?[] a={id,null,true};Check(component,"GetTransformationMatrix",a);var m=ArrayOf<double>(a[1],9);foreach(var x in m)Finite(x);var x1=new Vec3(m[0],m[3],m[6]);var x2=new Vec3(m[1],m[4],m[7]);var x3=new Vec3(m[2],m[5],m[8]);if(Math.Abs(x1.Length-1)>1e-5||Math.Abs(x2.Length-1)>1e-5||Math.Abs(x3.Length-1)>1e-5||Math.Abs(x1.Dot(x2))>1e-5||Math.Abs(x1.Dot(x3))>1e-5||Math.Abs(x2.Dot(x3))>1e-5 || Math.Abs((x1.X*(x2.Y*x3.Z-x2.Z*x3.Y)-x1.Y*(x2.X*x3.Z-x2.Z*x3.X)+x1.Z*(x2.X*x3.Y-x2.Y*x3.X))-1)>1e-5)throw new InvalidDataException("Invalid transformation matrix.");return x3;}
 private void ReadLoads(string component,string id,CsiUnits u,List<ExistingLoad> loads){bool frame=component=="FrameObj";object?[] a=frame?[id,0,null,null,null,null,null,null,null,null,null,null,null,0]:[id,0,null,null,null,null,null,0];Check(component,frame?"GetLoadDistributed":"GetLoadUniform",a);var n=(int)a[1]!;if(n==0)return;var ids=ArrayOf<string>(a[2],n);var pats=ArrayOf<string>(a[3],n);if(frame){var types=ArrayOf<int>(a[4],n);var csys=ArrayOf<string>(a[5],n);var dirs=ArrayOf<int>(a[6],n);var d1=ArrayOf<double>(a[9],n);var d2=ArrayOf<double>(a[10],n);var v1=ArrayOf<double>(a[11],n);var v2=ArrayOf<double>(a[12],n);for(int i=0;i<n;i++){var scale=types[i] switch{1=>u.ForceToN/u.LengthToM,2=>u.ForceToN,_=>throw new InvalidDataException("Unknown distributed load type.")};var kind=$"FrameDistributed:type={types[i]};csys={csys[i]};dir={dirs[i]};startM={Finite(d1[i])*u.LengthToM:R};endM={Finite(d2[i])*u.LengthToM:R}";loads.Add(new(ids[i],pats[i],kind+";endpoint=1",Finite(v1[i])*scale,"Unknown"));loads.Add(new(ids[i],pats[i],kind+";endpoint=2",Finite(v2[i])*scale,"Unknown"));}}else {var csys=ArrayOf<string>(a[4],n);var dirs=ArrayOf<int>(a[5],n);var values=ArrayOf<double>(a[6],n);for(int i=0;i<n;i++)loads.Add(new(ids[i],pats[i],$"AreaUniform:csys={csys[i]};dir={dirs[i]}",Finite(values[i])*u.ForceToN/(u.LengthToM*u.LengthToM),"Unknown"));}}
 private static void ValidatePolygon(ImmutableArray<Vec3> vertices){var origin=vertices[0];double area=0;for(int i=1;i<vertices.Length-1;i++){var a=vertices[i]-origin;var b=vertices[i+1]-origin;area+=new Vec3(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X).Length;}if(!double.IsFinite(area)||area<1e-12)throw new InvalidDataException("Degenerate area polygon.");}
 private static string PlaneKey(Vec3 normal,Vec3 point){if(normal.X< -1e-8 || Math.Abs(normal.X)<1e-8 && (normal.Y< -1e-8 || Math.Abs(normal.Y)<1e-8 && normal.Z<0))normal=normal*-1;double Clean(double value,int decimals){var rounded=Math.Round(value,decimals);return rounded==0?0:rounded;} return FormattableString.Invariant($"plane:{Clean(normal.X,5):F5},{Clean(normal.Y,5):F5},{Clean(normal.Z,5):F5};d={Clean(normal.Dot(point),4):F4}");}
 private static T[] ArrayOf<T>(object? value,int n){if(n<0||n>1_000_000)throw new InvalidDataException("Invalid CSI item count.");if(n==0)return Array.Empty<T>();if(value is not T[] values || values.Length<n)throw new InvalidDataException("CSI getter returned inconsistent array lengths.");return values.Take(n).ToArray();}
 private static double Finite(double x)=>double.IsFinite(x)?x:throw new InvalidDataException("CSI returned a nonfinite number.");
 private static void Try(Action action,List<CsiReadIssue> issues){try{action();}catch(OperationCanceledException){throw;}catch(Exception e){issues.Add(Issue(e,"ReadObject"));}}
 private static CsiReadIssue Issue(Exception e,string operation)=>e is ReadFailure f?new(f.Code,f.Operation,f.Message,CsiIssueSeverity.Error,f.ReturnCode):new(e is CsiNotRunningException?"DISCONNECTED":e is ArgumentOutOfRangeException?"UNSUPPORTED_UNITS":e is InvalidDataException?"INVALID_DATA":e is PlatformNotSupportedException?"UNSUPPORTED_PLATFORM":e is NotSupportedException?"UNSUPPORTED_GETTER":"CSI_READ_FAILURE",operation,e.Message);
 public async ValueTask DisposeAsync(){if(Interlocked.Exchange(ref disposed,1)!=0)return;await dispatcher.Invoke(()=>{session?.Dispose();session=null;return true;});await dispatcher.DisposeAsync();}
 private sealed class ReadFailure(string code,string operation,string message,int? returnCode=null):Exception(message){public string Code=>code;public string Operation=>operation;public int? ReturnCode=>returnCode;}
}
