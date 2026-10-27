using System.Collections.Immutable;
namespace StructuralLoadAssistant.Engineering;

/// <summary>Single evidenced roof-face selection; does not manufacture whole-building roof geometry.</summary>
public static class AsNzRoofCoefficientLookup
{
    public static AutoLookupValue Select(AsNzAutoSource source,string key,AsNzRoofFace face,
        ImmutableDictionary<string,string> conditions,ImmutableDictionary<string,double> numbers)
    {
        AutoLookupValue Block(string reason)=>new(key,AutoLookupState.InputRequired,null,"",reason,[],"","");
        if(!numbers.TryGetValue("Pitch",out var pitch)||!numbers.TryGetValue("hd",out var hd)||!double.IsFinite(hd)||hd<=0)
            return Block("Explicit pitch and positive h/d required");
        var branch=AsNzRoofApplicability.Evaluate(conditions.GetValueOrDefault("RoofForm",""),pitch,face);
        if(!branch.Supported||conditions.GetValueOrDefault("RoofFace")!=face.ToString())return Block(branch.Reason);
        if(branch.Table=="Table 5.3(C)"&&pitch>=25&&(!numbers.TryGetValue("bd",out var bd)||!double.IsFinite(bd)||bd<=0))
            return Block("Table 5.3(C) steep-slope branch requires explicit breadth/depth (bd), not db");
        var value=new AsNz2021LocalAutoLookupService().Select(source,key,"Cpe",conditions,numbers);
        if(value.State!=AutoLookupState.Resolved)return value;
        if(source.Bundle.Records.Where(r=>value.RecordIds.Contains(r.RecordKey)).Any(r=>r.TableOrFigure!=branch.Table))
            return Block("Selected coefficient table contradicts roof form, wind incidence and exact pitch boundary");
        return value;
    }
}
