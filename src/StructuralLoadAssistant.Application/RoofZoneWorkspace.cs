using System.Collections.Immutable;
using StructuralLoadAssistant.RoofMapping;
using StructuralLoadAssistant.Domain;

namespace StructuralLoadAssistant.Application;

public sealed class RoofZoneWorkspace
{
    private ImmutableArray<RoofShell3D> shells=[];
    private readonly Dictionary<string,RoofPlaneCoordinateSystem> shellPlanes=new(StringComparer.Ordinal);
    private readonly Stack<ImmutableArray<EditableRoofZone>> undo=new(),redo=new();
    private readonly Dictionary<string,EditableRoofZone> auditHeads=new(StringComparer.Ordinal);
    private string state="";
    public event Action? Changed;
    public IReadOnlyList<RoofPlaneCoordinateSystem> Planes { get; private set; }=[];
    public ImmutableArray<EditableRoofZone> Zones { get; private set; }=[];
    public ImmutableArray<ShellZoneIntersection> Classifications { get; private set; }=[];
    public bool Editable { get; set; }
    public bool CanUndo=>undo.Count>0;
    public bool CanRedo=>redo.Count>0;
    public void ClearClassifications()=>Classifications=[];
    public void LoadSource(ImmutableArray<RoofShell3D> source,string windState)
    {
        shells=source;state=windState;shellPlanes.Clear();
        var planes=new List<RoofPlaneCoordinateSystem>();
        foreach(var shell in source.Where(s=>s.IsValid).OrderBy(s=>s.ShellId,StringComparer.Ordinal))
        {
            var candidate=RoofPlaneCoordinateSystem.Create("candidate",shell.Vertices);
            var plane=planes.FirstOrDefault(p=>Math.Abs(p.Normal.Dot(candidate.Normal)-1)<1e-9 && Math.Abs((p.Origin-candidate.Origin).Dot(p.Normal))<=1e-6);
            if(plane is null)
            {
                var id="PL-"+SnapshotJson.Hash(new {X=Math.Round(candidate.Normal.X,8),Y=Math.Round(candidate.Normal.Y,8),Z=Math.Round(candidate.Normal.Z,8),D=Math.Round(candidate.Origin.Dot(candidate.Normal),6)})[..12];
                plane=candidate with {Id=id};planes.Add(plane);
            }
            shellPlanes[shell.ShellId]=plane;
        }
        Planes=planes;Zones=[];Classifications=[];undo.Clear();redo.Clear();auditHeads.Clear();Editable=source.Any(s=>s.IsValid);
    }
    public RoofPlaneCoordinateSystem PlaneForShell(string id)=>shellPlanes.TryGetValue(id,out var plane)?plane:throw new InvalidOperationException("Shell has no valid roof surface coordinates.");
    public string[] ShellIds(string planeId)=>shellPlanes.Where(k=>k.Value.Id==planeId).Select(k=>k.Key).ToArray();
    public ImmutableArray<UvPoint> Footprint(string id)=>shells.Single(s=>s.ShellId==id).Vertices.Select(PlaneForShell(id).Project).ToImmutableArray();
    public void Restore(ImmutableArray<EditableRoofZone> zones)
    {
        var value=zones.IsDefault?[]:zones;
        if(value.Any(z=>z.WindStateId!=state))throw new InvalidOperationException("Zone wind-state identity mismatch.");
        if(value.Select(z=>z.Id).Distinct(StringComparer.Ordinal).Count()!=value.Length)throw new InvalidOperationException("Duplicate zone identity.");
        Zones=value;Classifications=[];undo.Clear();redo.Clear();auditHeads.Clear();foreach(var z in value)auditHeads[z.Id]=z;
    }
    public EditableRoofZone Create(string label,string planeId,ImmutableArray<UvPoint> points,string user,string reason)
    {
        RequireEditor(user,reason);RequireCoordinates(points);
        if(string.IsNullOrWhiteSpace(label))throw new ArgumentException("Zone label is required.");
        var plane=Planes.Single(p=>p.Id==planeId);var now=DateTimeOffset.UtcNow;
        var zone=new EditableRoofZone(Guid.NewGuid().ToString("N"),label,planeId,state,points,null,1,plane.GeometryFingerprint,now,now,
            ZonePolygonEngine.Validate(points) is null?ZoneGeometryStatus.Draft:ZoneGeometryStatus.Invalid,[new("Create",user,reason,now)]);
        Commit(Zones.Add(zone));return zone;
    }
    public EditableRoofZone CreateFromShells(string label,IEnumerable<string> ids,string user,string reason)
    {
        var selected=ids.Distinct(StringComparer.Ordinal).ToArray();
        if(selected.Length==0)throw new InvalidOperationException("Select one or more coplanar shells.");
        var plane=PlaneForShell(selected[0]);
        if(selected.Any(id=>PlaneForShell(id).Id!=plane.Id))throw new InvalidOperationException("A zone cannot bridge separate roof planes.");
        return Create(label,plane.Id,ZonePolygonEngine.Union(selected.Select(Footprint)),user,reason);
    }
    public void MoveVertex(string id,int index,UvPoint point,string user,string reason)
    {var z=Zones.Single(z=>z.Id==id);Replace(z,z.Vertices.SetItem(index,point),"MoveVertex",user,reason);}
    public void InsertVertex(string id,int after,UvPoint point,string user,string reason)
    {var z=Zones.Single(z=>z.Id==id);Replace(z,z.Vertices.Insert(after+1,point),"InsertVertex",user,reason);}
    public void DeleteVertex(string id,int index,string user,string reason)
    {var z=Zones.Single(z=>z.Id==id);if(z.Vertices.Length<=3)throw new InvalidOperationException("A zone requires three vertices.");Replace(z,z.Vertices.RemoveAt(index),"DeleteVertex",user,reason);}
    public void Translate(string id,UvPoint delta,string user,string reason)
    {var z=Zones.Single(z=>z.Id==id);Replace(z,z.Vertices.Select(p=>new UvPoint(p.U+delta.U,p.V+delta.V)).ToImmutableArray(),"MovePolygon",user,reason);}
    public void SetMetadata(string id,string label,string? notes,string user,string reason)
    {
        RequireEditor(user,reason);if(string.IsNullOrWhiteSpace(label))throw new ArgumentException("Zone label is required.");
        var z=Zones.Single(z=>z.Id==id);Commit(Zones.SetItem(Zones.IndexOf(z),Edited(z,"Metadata",user,reason) with{Label=label,Notes=notes}));
    }
    public void Delete(string id,string user,string reason){RequireEditor(user,reason);Commit(Zones.RemoveAll(z=>z.Id==id));}
    public UvPoint Snap(string planeId,UvPoint point,double toleranceM)=>ZonePolygonEngine.Snap(point,ShellIds(planeId).Select(Footprint),toleranceM);
    public void Undo(string user,string reason)=>Navigate(undo,redo,"Undo",user,reason);
    public void Redo(string user,string reason)=>Navigate(redo,undo,"Redo",user,reason);
    public ImmutableArray<ShellZoneIntersection> Validate(IEnumerable<RoofScopeExclusion> exclusions)
    {
        if(!Editable)throw new InvalidOperationException("Resolve source geometry or selection scope before validation.");
        var excluded=exclusions.ToDictionary(e=>e.ShellId,StringComparer.Ordinal);
        Classifications=shells.Select(s=>
        {
            if(excluded.TryGetValue(s.ShellId,out var e)&&!string.IsNullOrWhiteSpace(e.User)&&!string.IsNullOrWhiteSpace(e.Reason))
                return new ShellZoneIntersection(s.ShellId,GeometryClassification.ExcludedWithReason,s.SurfaceAreaM2,[],false,"Audited exclusion: "+e.Reason);
            if(!shellPlanes.TryGetValue(s.ShellId,out var p))return new ShellZoneIntersection(s.ShellId,GeometryClassification.InvalidGeometry,s.SurfaceAreaM2,[],false,s.GeometryError??"Invalid source geometry");
            return ZonePolygonEngine.Classify(s.ShellId,Footprint(s.ShellId),Zones.Where(z=>z.RoofPlaneId==p.Id&&z.WindStateId==state));
        }).ToImmutableArray();
        Zones=Zones.Select(z=>z with{ValidationStatus=ZonePolygonEngine.Validate(z.Vertices) is null?ZoneGeometryStatus.Valid:ZoneGeometryStatus.Invalid}).ToImmutableArray();
        return Classifications;
    }
    private void Replace(EditableRoofZone z,ImmutableArray<UvPoint> points,string action,string user,string reason)
    {
        RequireEditor(user,reason);RequireCoordinates(points);
        Commit(Zones.SetItem(Zones.IndexOf(z),Edited(z,action,user,reason) with{Vertices=points,
            ValidationStatus=ZonePolygonEngine.Validate(points) is null?ZoneGeometryStatus.Draft:ZoneGeometryStatus.Invalid}));
    }
    private void RequireEditor(string user,string reason)
    {
        if(!Editable||string.IsNullOrWhiteSpace(state))throw new InvalidOperationException("Select current, valid roof geometry and a wind state.");
        if(string.IsNullOrWhiteSpace(user)||string.IsNullOrWhiteSpace(reason))throw new ArgumentException("Reviewer and edit reason are required.");
    }
    private static void RequireCoordinates(ImmutableArray<UvPoint> p)
    {if(p.IsDefault||p.Length<3||p.Length>10000||p.Any(v=>!double.IsFinite(v.U)||!double.IsFinite(v.V)))throw new ArgumentException("Use 3 to 10000 finite UV vertices.");}
    private static EditableRoofZone Edited(EditableRoofZone z,string action,string user,string reason)=>z with{
        Revision=z.Revision+1,ModifiedAt=DateTimeOffset.UtcNow,ValidationStatus=ZoneGeometryStatus.Draft,Audit=z.Audit.Add(new(action,user,reason,DateTimeOffset.UtcNow))};
    private void Commit(ImmutableArray<EditableRoofZone> zones)
    {undo.Push(Zones);redo.Clear();Zones=zones;foreach(var z in zones)auditHeads[z.Id]=z;Classifications=[];Changed?.Invoke();}
    private void Navigate(Stack<ImmutableArray<EditableRoofZone>> from,Stack<ImmutableArray<EditableRoofZone>> to,string action,string user,string reason)
    {
        RequireEditor(user,reason);if(from.Count==0)throw new InvalidOperationException("No geometry edit to "+action);
        to.Push(Zones);
        Zones=from.Pop().Select(z=>
        {
            var head=auditHeads.GetValueOrDefault(z.Id,z);
            var restored=z with{Revision=head.Revision,Audit=head.Audit};
            return Edited(restored,action,user,reason);
        }).ToImmutableArray();
        foreach(var z in Zones)auditHeads[z.Id]=z;
        Classifications=[];Changed?.Invoke();
    }
}
