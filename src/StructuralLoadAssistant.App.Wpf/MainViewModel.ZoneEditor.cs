using System.Collections.Immutable;
using StructuralLoadAssistant.Application;
using StructuralLoadAssistant.RoofMapping;
using StructuralLoadAssistant.Domain;

namespace StructuralLoadAssistant.App.Wpf;

public sealed partial class MainViewModel
{
    public RoofZoneWorkspace ZoneEditor { get; }=new();
    private string zoneGeometryHash=SnapshotJson.Hash(Array.Empty<EditableRoofZone>());
    private void ZoneGeometryEdited()
    {
        var next=SnapshotJson.Hash(ZoneEditor.Zones);
        inspection.RecordZoneGeometryEdit(zoneGeometryHash,next,User,Reason);zoneGeometryHash=next;
        inspection.InvalidateGeometryReview();HasUnsavedChanges=true;Refresh();
    }
    public void RestoreZoneEditor(RoofMappingDraft? draft)
    {
        ZoneEditor.LoadSource(inspection.Shells3D,ActiveWindState.Id);
        if(draft is not null)ZoneEditor.Restore(draft.ZonePolygons);
        zoneGeometryHash=SnapshotJson.Hash(ZoneEditor.Zones);
        ZoneEditor.Editable=!IsDemo && inspection.ReviewStatus is not (GeometryReviewStatus.StaleNeedsRescan or GeometryReviewStatus.ScopeReviewRequired);
    }
    public void ValidateZoneIntersections()
    {
        if(IsDemo)throw new InvalidOperationException("Use current read-only roof geometry.");
        var results=ZoneEditor.Validate(inspection.Exclusions);
        inspection.InvalidateGeometryReview();
        inspection.SetIntersectionMappings(results,ActiveWindState.Id);
        if(results.All(r=>r.Classification is GeometryClassification.FullyInsideZone or GeometryClassification.ExcludedWithReason))
            inspection.ValidateGeometry();
        HasUnsavedChanges=true;Refresh();
    }
    public void AcceptSelectionScopeMerge()
    {
        var pending=inspection.PendingScopeDraft??throw new InvalidOperationException("No selection scope change pending.");
        inspection.AcceptScopeMerge(User,Reason);
        RestoreZoneEditor(pending);HasUnsavedChanges=true;Refresh();
    }
    public void ClearZoneEditor()
    {ZoneEditor.LoadSource([],ActiveWindState.Id);ZoneEditor.Editable=false;}
}
