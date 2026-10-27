using System.Collections.Immutable;
using System.Globalization;
using StructuralLoadAssistant.Domain;
namespace StructuralLoadAssistant.Engineering;

/// <summary>Private review selector. No normative numbers or fallback tables live here.</summary>
public sealed class AsNz2021LocalAutoLookupService
{
    public const string Method="FlatRoofAutoReviewV1";
    public const string RoofMethod="RoofFormAutoReviewV2";
    static string F(double v)=>v.ToString("R",CultureInfo.InvariantCulture);
    /// <summary>True only for the exact immutable request issued by this process's local lookup.</summary>
    public static bool HasAuthenticatedLookup(EngineeringCalculationRequest request)=>AsNzAutoLookupReceipts.TryGet(request,out _);
    public AutoLookupValue Select(AsNzAutoSource source,string key,string category,ImmutableDictionary<string,string> conditions,ImmutableDictionary<string,double> numbers)
    {
        AutoLookupValue Fail(AutoLookupState state,string reason)=>new(key,state,null,"",reason,[],"","");
        var manifest=source.Bundle.Manifest;
        if(!AsNzAutoLookupReceipts.HasCoherentBytes(source.Bundle))
            return Fail(AutoLookupState.RightsOrEditionBlocked,"V3 semantic/source-component integrity invalid");
        if(new[]{"Country","Region","System","ProcedureId","Method","RoofForm","LimitState"}.Any(k=>!conditions.TryGetValue(k,out var value)||string.IsNullOrWhiteSpace(value)) ||
            conditions["Country"] is not ("AU" or "NZ") || conditions["System"]!="MWFRS" ||
            conditions["LimitState"] is not ("Uls" or "Sls"))
            return Fail(AutoLookupState.RightsOrEditionBlocked,"Explicit country, region, roof form and procedure scope required");
        if(manifest.Family!=StandardFamily.AsNz1170_2 || manifest.Edition!="AS/NZS 1170.2:2021" ||
            (source.Synthetic ? manifest.Rights!=DataRightsClassification.SyntheticTestData :
            !source.SoftwareUseAuthorized || !AsNzAutoLookupReceipts.IsPrivateSourceVerified(source) || conditions["ProcedureId"]==Method || string.IsNullOrWhiteSpace(source.RightsReference) || string.IsNullOrWhiteSpace(source.AdoptionReference) || manifest.Rights is not (DataRightsClassification.LocalLicensedOnly or DataRightsClassification.Restricted)))
            return Fail(AutoLookupState.RightsOrEditionBlocked,"Rights/edition authorization required");
        if(numbers.Values.Any(v=>!double.IsFinite(v)))return Fail(AutoLookupState.OutsideDomain,"Finite selector operands required");
        AsNzRoofBranch? roofBranch=null;
        if(category=="Cpe" && conditions.GetValueOrDefault("Surface")=="Roof" && conditions["ProcedureId"]!=Method)
        {
            if(!numbers.TryGetValue("Pitch",out var pitch)||!numbers.TryGetValue("hd",out var hd)||hd<=0||
                !conditions.TryGetValue("RoofFace",out var faceText)||!Enum.TryParse<AsNzRoofFace>(faceText,out var face)||!Enum.IsDefined(face))
                return Fail(AutoLookupState.OutsideDomain,"Roof selection requires explicit pitch, positive h/d and face");
            roofBranch=AsNzRoofApplicability.Evaluate(conditions["RoofForm"],pitch,face);
            if(!roofBranch.Supported)return Fail(AutoLookupState.OutsideDomain,roofBranch.Reason);
            if(roofBranch.Table=="Table 5.3(C)"&&pitch>=25&&(!numbers.TryGetValue("bd",out var bd)||bd<=0))
                return Fail(AutoLookupState.OutsideDomain,"Steep Table 5.3(C) branch requires positive breadth/depth (bd)");
        }
        var candidates=source.Bundle.Records.Where(r=>r.ParameterCategory==category && r.CategoricalConditions.Count==conditions.Count && conditions.All(c=>r.CategoricalConditions.GetValueOrDefault(c.Key)==c.Value)
            && r.NumericalConditions.Count==numbers.Count && numbers.Keys.All(r.NumericalConditions.ContainsKey)).ToArray();
        if(candidates.Length==0)return Fail(AutoLookupState.InputRequired,"No exact scoped source rows; no fallback");
        if(candidates.Any(r=>!double.IsFinite(r.Value)||r.NumericalConditions.Values.Any(v=>!double.IsFinite(v))))return Fail(AutoLookupState.RightsOrEditionBlocked,"Nonfinite source record");
        if(candidates.GroupBy(r=>string.Join(";",r.NumericalConditions.OrderBy(v=>v.Key).Select(v=>v.Key+"="+F(v.Value)))).Any(g=>g.Count()>1))return Fail(AutoLookupState.Ambiguous,"Duplicate/conflicting source grid points");
        var exact=candidates.Where(r=>numbers.All(n=>r.NumericalConditions[n.Key]==n.Value)).ToArray();
        CoefficientRecord[] used;double value;
        var weights=new Dictionary<string,double>();
        if(exact.Length==1){used=exact;value=exact[0].Value;}
        else
        {
            if(numbers.Count is <1 or >2)return Fail(AutoLookupState.OutsideDomain,"Only source-declared one/two-dimensional interpolation supported");
            var bounds=new List<(string Key,double Low,double High)>();
            foreach(var n in numbers)
            {
                var axis=candidates.Select(r=>r.NumericalConditions[n.Key]).Distinct().Order().ToArray();
                if(n.Value<axis[0]||n.Value>axis[^1])return Fail(AutoLookupState.OutsideDomain,"Extrapolation forbidden");
                bounds.Add((n.Key,axis.Last(v=>v<=n.Value),axis.First(v=>v>=n.Value)));
            }
            var corners=candidates.Where(r=>bounds.All(b=>r.NumericalConditions[b.Key]==b.Low||r.NumericalConditions[b.Key]==b.High)).ToArray();
            int needed=bounds.Aggregate(1,(n,b)=>n*(b.Low==b.High?1:2));
            if(corners.Length!=needed)return Fail(AutoLookupState.InputRequired,"Missing interpolation endpoint/corner");
            var policy=numbers.Count==1?InterpolationPolicy.Linear1D:InterpolationPolicy.Bilinear2D;
            if(corners.Any(r=>r.Interpolation!=policy)||corners.Select(r=>r.Unit).Distinct().Count()!=1||corners.Select(r=>r.SignConvention).Distinct().Count()!=1||corners.Select(r=>r.TableOrFigure).Distinct().Count()!=1)
                return Fail(AutoLookupState.Ambiguous,"Incompatible source interpolation policy/unit/table/sign");
            // Keep h/d interpolation within a same-sign alternative at each fixed pitch.
            if(category=="Cpe" && bounds.Any(b=>b.Key=="hd"&&b.Low!=b.High) &&
                corners.All(r=>r.TableOrFigure is "Table 5.3(A)" or "Table 5.3(B)" or "Table 5.3(C)") &&
                corners.Any(a=>corners.Any(b=>a.NumericalConditions["hd"]!=b.NumericalConditions["hd"] &&
                    numbers.Keys.Where(k=>k!="hd").All(k=>a.NumericalConditions[k]==b.NumericalConditions[k]) && Math.Sign(a.Value)!=Math.Sign(b.Value))))
                return Fail(AutoLookupState.OutsideDomain,"Roof h/d interpolation between different signs is not permitted; source alternative required");
            used=corners;value=0;
            foreach(var r in corners)
            {
                double weight=1;
                foreach(var b in bounds)if(b.Low!=b.High)weight*=r.NumericalConditions[b.Key]==b.Low?(b.High-numbers[b.Key])/(b.High-b.Low):(numbers[b.Key]-b.Low)/(b.High-b.Low);
                value+=weight*r.Value;
                weights[r.RecordKey]=weight;
            }
        }
        string expectedUnit=category is "VR" or "MinimumSpeed"?"m/s":category=="rho"?"kg/m3":"dimensionless";
        if(used.Any(r=>r.Unit!=expectedUnit))return Fail(AutoLookupState.RightsOrEditionBlocked,"Source unit must be "+expectedUnit+"; no implicit unit conversion");
        if(roofBranch!=null&&used.Any(r=>r.TableOrFigure!=roofBranch.Table))return Fail(AutoLookupState.RightsOrEditionBlocked,"Roof table contradicts form, incidence and pitch");
        if(used.Any(r=>string.IsNullOrWhiteSpace(r.TableOrFigure)||string.IsNullOrWhiteSpace(r.ClauseReference)))return Fail(AutoLookupState.RightsOrEditionBlocked,"Missing source locator");
        if(!source.Synthetic && used.Any(r=>!source.TableDecisions.TryGetValue(r.TableOrFigure,out var decision)||string.IsNullOrWhiteSpace(decision)))return Fail(AutoLookupState.RightsOrEditionBlocked,"AmendmentReviewRequired for selected table");
        string selection=string.Join("; ",conditions.OrderBy(c=>c.Key).Select(c=>c.Key+"="+c.Value).Concat(numbers.OrderBy(n=>n.Key).Select(n=>n.Key+"="+F(n.Value))))+
            "; "+(exact.Length==1?"EXACT weight=1":"INTERPOLATED "+string.Join(",",used.Select(r=>r.RecordKey+"["+string.Join(",",r.NumericalConditions.Select(n=>n.Key+"="+F(n.Value)))+"; value="+F(r.Value)+"; weight="+F(weights[r.RecordKey])+"]")));
        return new(key,AutoLookupState.Resolved,value,used[0].Unit,"AUTO LOOKUP / NOT FOR DESIGN",used.Select(r=>r.RecordKey).ToImmutableArray(),string.Join(";",used.Select(r=>r.TableOrFigure+" / "+r.ClauseReference).Distinct()),selection);
    }

    public AsNzAutoResult Resolve(EngineeringCalculationRequest geometry,AsNzAutoSite site,AsNzAutoSource? source)
    {
        var values=ImmutableArray.CreateBuilder<AutoLookupValue>();var coefficients=ImmutableDictionary.CreateBuilder<string,CoefficientInputEvidence>();
        void Block(string key,string reason,AutoLookupState state=AutoLookupState.InputRequired)=>values.Add(new(key,state,null,"",reason,[],"",""));
        void Supply(string key,double v,string unit,string reason)
        {
            coefficients[key]=new(key,v,unit,source?.Synthetic==true?DataProvenance.SyntheticDemoFixture:DataProvenance.Unverified,"Engineer selected input / assumption","Explicit input",reason,DateTimeOffset.UnixEpoch);
            values.Add(new(key,AutoLookupState.Resolved,v,unit,reason,[],"Explicit engineer input",reason));
        }
        bool flat=geometry.RoofForm=="Flat"&&geometry.RoofSlopeDegrees==0;
        bool singleSlope=geometry.RoofForm is "Monoslope" or "LowSlope";
        bool legacy=flat&&source?.Synthetic==true&&source.Bundle.Records.Any(r=>r.CategoricalConditions.GetValueOrDefault("ProcedureId")==Method);
        bool supported=flat || (singleSlope&&geometry.RoofSlopeDegrees>0&&geometry.RoofSlopeDegrees<90 && (geometry.RoofForm!="LowSlope"||geometry.RoofSlopeDegrees<10));
        if(!supported)Block("RoofMethod","METHOD_NOT_SUPPORTED / individual roof-face geometry required for gable/hip; no single-plane substitution",AutoLookupState.OutsideDomain);
        if(!site.TrueNorthReviewed)Block("TrueNorth","Verified model-to-cardinal orientation required");
        if(singleSlope&&(!site.SlopeBearingDegrees.HasValue||!double.IsFinite(site.SlopeBearingDegrees.Value)))Block("SlopeBearing","Explicit reviewed uphill bearing in model XY required");
        if(new[]{geometry.BuildingLengthM,geometry.BuildingWidthM,geometry.ReferenceHeightM,geometry.EaveHeightM,geometry.RidgeHeightM,geometry.GroundElevationM,geometry.WindAngleDegrees}.Any(v=>!double.IsFinite(v)) || geometry.BuildingLengthM<=0 || geometry.BuildingWidthM<=0 || geometry.ReferenceHeightM<=0 || geometry.EaveHeightM<=geometry.GroundElevationM || geometry.RidgeHeightM<geometry.EaveHeightM || Math.Abs(geometry.ReferenceHeightM-((geometry.RidgeHeightM+geometry.EaveHeightM)/2-geometry.GroundElevationM))>1e-8 || (flat&&geometry.RidgeHeightM!=geometry.EaveHeightM))
            Block("GeometryConsistency","Roof elevations, mean height and datum must agree",AutoLookupState.OutsideDomain);
        bool usable=source!=null;
        if(source==null&&!flat)Block("RoofMethod","No source-scoped sloped method available",AutoLookupState.OutsideDomain);
        if(source==null)Block("Source","LOCAL SOURCE DATA UNAVAILABLE / OWNER RIGHTS OR AMENDMENT EVIDENCE REQUIRED",AutoLookupState.RightsOrEditionBlocked);
        else
        {
            var m=source.Bundle.Manifest;
            var preflight=StandardsDataBundleValidator.GenerateIntakePreflight(source.Bundle);
            usable=preflight.SemanticHashValid&&preflight.ComponentBytesCoherent&&m.CanonicalHashAlgorithmVersion=="3.0"&&m.Family==StandardFamily.AsNz1170_2&&m.Edition==geometry.StandardEdition
                && m.Amendments.Order().SequenceEqual(geometry.SelectedAmendments.Order())
                && (source.Synthetic?m.Rights==DataRightsClassification.SyntheticTestData:source.SoftwareUseAuthorized&&AsNzAutoLookupReceipts.IsPrivateSourceVerified(source)&&!string.IsNullOrWhiteSpace(source.RightsReference)&&!string.IsNullOrWhiteSpace(source.AdoptionReference)&&m.Rights is DataRightsClassification.LocalLicensedOnly or DataRightsClassification.Restricted);
            if(!usable)Block("Source","Source rights, edition/amendment identity or V3 byte/semantic integrity invalid",AutoLookupState.RightsOrEditionBlocked);
        }
        if(site.Country is not ("AU" or "NZ")||string.IsNullOrWhiteSpace(site.Region))Block("Region","Select country and exact eligible region; no regional default");
        if(!double.IsFinite(site.Terrain)||site.Terrain<1||site.Terrain>4)Block("Terrain","Unsupported terrain domain",AutoLookupState.OutsideDomain);
        if(!site.UniformTerrain)Block("TerrainProfile","Mixed terrain averaging requires segment evidence; this selector supports confirmed uniform terrain only");
        if(site.Region=="A0")Block("A0","A0 terrain exception selector incomplete",AutoLookupState.OutsideDomain);
        if(site.Region.StartsWith("C",StringComparison.Ordinal)||site.Region.StartsWith("D",StringComparison.Ordinal))
            if(!site.CoastDistanceKm.HasValue||!double.IsFinite(site.CoastDistanceKm.Value)||site.CoastDistanceKm.Value<0)Block("Coast","Smoothed-coastline distance/boundary evidence required");
        if(!site.GeometryReviewed||string.IsNullOrWhiteSpace(site.GeometryReason))Block("Geometry","Engineer-reviewed geometry/datum/orientation reason required");
        bool internalContext=site.Enclosure=="Enclosed"&&!string.IsNullOrWhiteSpace(site.Openings)&&!string.IsNullOrWhiteSpace(site.Debris);
        if(site.Dynamics!="RigidAssessed")Block("Dynamics","Dynamic sensitivity assessment required; advanced dynamic selector incomplete");
        if(site.TributaryAreaM2<=0||!double.IsFinite(site.TributaryAreaM2)||site.ParapetHeightM<0||!double.IsFinite(site.ParapetHeightM)||!double.IsFinite(site.WindwardHeightM)||site.WindwardHeightM<=0||site.WindwardHeightM>geometry.ReferenceHeightM)Block("AreaHeight","Positive tributary area, assessed wall height and nonnegative parapet required");
        if(double.IsFinite(site.WindwardHeightM)&&site.WindwardHeightM!=geometry.ReferenceHeightM)
            Block("WallHeightBasis","Separate external wall-height/internal pressure speed bases require further engineering implementation",AutoLookupState.OutsideDomain);
        if(site.System!="MWFRS")Block("System","C&C selector incomplete; MWFRS only",AutoLookupState.OutsideDomain);
        bool contextOk=!values.Any(v=>v.State!=AutoLookupState.Resolved);
        if(site.NoShieldingBenefit){Supply("Ms",1,"dimensionless","ASSUMPTION: no shielding benefit claimed");Supply("Uls_Ms",1,"dimensionless","ASSUMPTION: no shielding benefit claimed");Supply("Sls_Ms",1,"dimensionless","ASSUMPTION: no shielding benefit claimed");}else if(!site.ShieldingEligibilityReviewed||!site.ShieldingParameter.HasValue||!double.IsFinite(site.ShieldingParameter.Value))Block("Ms","Shielding eligibility and finite parameter required");
        if(site.NoTopographicAmplification)Supply("Mt",1,"dimensionless","ASSUMPTION: engineer explicitly declares no topographic amplification applicable");else Block("Mt","Topographic assessment required; no automatic default");
        if(flat)Supply("SlopeBearing",0,"deg","Flat roof has no slope bearing; model orientation supplied separately");
        else if(site.SlopeBearingDegrees.HasValue&&double.IsFinite(site.SlopeBearingDegrees.Value))Supply("SlopeBearing",site.SlopeBearingDegrees.Value,"deg","Engineer reviewed uphill bearing in model XY; true-north rotation separate");
        if(site.UlsYears>0&&site.SlsYears>0&&double.IsFinite(site.UlsYears)&&double.IsFinite(site.SlsYears)){Supply("R_Uls",site.UlsYears,"years","Owner selected ULS return period");Supply("R_Sls",site.SlsYears,"years","Owner selected SLS return period");}else {Block("ReturnPeriod","Positive finite return periods required");contextOk=false;}
        AutoLookupValue Lookup(string key,string category,LimitState state,Dictionary<string,string>? more=null,Dictionary<string,double>? numeric=null)
        {
            var cat=new Dictionary<string,string>{{"Country",site.Country},{"Region",site.Region},{"System",site.System},{"ProcedureId",legacy?Method:RoofMethod},{"Method",legacy?"FlatRoof":"RoofFormScoped"},{"RoofForm",geometry.RoofForm},{"LimitState",state.ToString()}};
            if(more!=null)foreach(var item in more)cat[item.Key]=item.Value;
            var v=Select(source!,key,category,cat.ToImmutableDictionary(),(numeric??[]).ToImmutableDictionary());values.Add(v);
            if(v.State==AutoLookupState.Resolved)
                coefficients[key]=new(key,v.Value!.Value,v.Unit,source!.Synthetic?DataProvenance.SyntheticDemoFixture:DataProvenance.Unverified,source.Bundle.Manifest.BundleId+" / "+source.Bundle.Manifest.ContentHash,v.Source,"AUTO LOOKUP; records "+string.Join(",",v.RecordIds)+"; "+v.Selection,DateTimeOffset.UnixEpoch);
            return v;
        }
        var branches=new List<object>();
        if(!internalContext)Block("Enclosure","Net pressure blocked: explicit enclosure/openings/debris applicability required; external review remains separate");
        if(usable&&contextOk&&supported)
        {
            foreach(var state in new[]{LimitState.Uls,LimitState.Sls})
            {
                string S=state.ToString();
                var vrNumbers=new Dictionary<string,double>{{"R",state==LimitState.Uls?site.UlsYears:site.SlsYears}};
                if(site.Region.StartsWith("C",StringComparison.Ordinal)||site.Region.StartsWith("D",StringComparison.Ordinal))vrNumbers["CoastKm"]=site.CoastDistanceKm!.Value;
                Lookup("VR_"+S,"VR",state,numeric:vrNumbers);
                Lookup(S+"_Mc","Mc",state);
                if(!site.NoShieldingBenefit&&site.ShieldingEligibilityReviewed&&site.ShieldingParameter.HasValue) {var shield=Lookup(S+"_Ms","Ms",state,numeric:new(){{"Parameter",site.ShieldingParameter.Value},{"Height",geometry.ReferenceHeightM}});if(shield.Value.HasValue)Supply(S+"_Ms",shield.Value.Value,"dimensionless","Source-declared shielding eligibility/interpolation: "+shield.Source+" "+shield.Selection);}
                Lookup(S+"_Mz_cat","Mz",state,legacy?new(){{"Terrain",F(site.Terrain)}}:null,legacy?new(){{"Height",geometry.ReferenceHeightM}}:new(){{"Height",geometry.ReferenceHeightM},{"Terrain",site.Terrain}});
                for(int bearing=0;bearing<360;bearing+=45)Lookup(S+"_Md_"+bearing,"Md",state,numeric:new(){{"Bearing",bearing}});
                Lookup(S+"_Cdyn","Cdyn",state,new(){{"Dynamics",site.Dynamics}});
                Lookup(S+"_rho","rho",state);
                if(state==LimitState.Uls)Lookup("UlsMinimumSpeed","MinimumSpeed",state);
                bool cyclonic=(site.Region.StartsWith("C",StringComparison.Ordinal)||site.Region.StartsWith("D",StringComparison.Ordinal))&&state==LimitState.Uls&&geometry.ReferenceHeightM<25;
                if(internalContext)foreach(string sign in new[]{"Positive","Negative"})Lookup(S+"_Cpi_"+sign,"Cpi",state,new(){{"Enclosure",site.Enclosure},{"Openings",site.Openings},{"Debris",site.Debris},{"InternalBranch",cyclonic?"CyclonicLowRiseUls":"General"},{"Sign",sign}},new(){{"Height",geometry.ReferenceHeightM}});
                foreach(var direction in Enum.GetValues<WindDirection>())
                {
                    bool x=direction is WindDirection.PositiveX or WindDirection.NegativeX;double depth=x?geometry.BuildingLengthM:geometry.BuildingWidthM,breadth=x?geometry.BuildingWidthM:geometry.BuildingLengthM;
                    foreach(string surface in new[]{"Windward","Leeward","SideLeft","SideRight","Roof"})
                    for(int c=surface=="Roof"?1:0;c<=(surface=="Roof"?2:0);c++)
                    for(int z=0;z<(surface=="Roof"?5:surface.StartsWith("Side",StringComparison.Ordinal)?4:1);z++)
                    {
                        string prefix=$"{S}_{direction}_{surface}_{c}_{z}";
                        bool roof=surface=="Roof";
                        AsNzRoofBranch? branch=null;
                        double theta=direction switch {WindDirection.PositiveX=>0,WindDirection.PositiveY=>90,WindDirection.NegativeX=>180,_=>270};
                        var face=flat?AsNzRoofFace.Upwind:AsNzRoofApplicability.FaceForWind(site.SlopeBearingDegrees??double.NaN,theta);
                        if(roof&&!legacy)
                        {
                            branch=face.HasValue?AsNzRoofApplicability.Evaluate(geometry.RoofForm,geometry.RoofSlopeDegrees,face.Value):null;
                            if(state==LimitState.Uls&&c==1&&z==0)branches.Add(new {Direction=direction.ToString(),Face=face?.ToString()??"Oblique",Table=branch?.Table??"",Status=branch?.Status??"BLOCKED",Reason=branch?.Reason??"Oblique incidence requires source applicability",WindIncidence=theta,Pitch=geometry.RoofSlopeDegrees});
                            if(branch?.Supported!=true){Block(prefix+"_Cpe",branch?.Reason??"Oblique roof incidence unsupported",AutoLookupState.NotApplicable);continue;}
                            if(branch.Table is "Table 5.3(B)" or "Table 5.3(C)" && z>0)continue;
                        }
                        if(surface=="Leeward"&&singleSlope){Block(prefix+"_Cpe","Table 5.2(B) does not establish monoslope leeward applicability",AutoLookupState.NotApplicable);continue;}
                        var more=new Dictionary<string,string>{{"Surface",surface},{"Case",c.ToString()},{"Zone",z.ToString()}};
                        var nums=new Dictionary<string,double>();
                        if(surface=="Windward"){nums["Height"]=site.WindwardHeightM;if(!legacy){more["Support"]=site.WindwardSupport;more["SpeedBasis"]=site.WindwardSpeedBasis;}}
                        if(surface=="Leeward"){nums["db"]=depth/breadth;if(!legacy){nums["Pitch"]=geometry.RoofSlopeDegrees;more["Incidence"]=face?.ToString()??"Oblique";}}
                        if(roof){nums["hd"]=geometry.ReferenceHeightM/depth;if(!legacy){more["RoofFace"]=face!.Value.ToString();nums["Pitch"]=geometry.RoofSlopeDegrees;}}
                        var cp=Lookup(prefix+"_Cpe","Cpe",state,more,nums);
                        var factorScope=new Dictionary<string,string>{{"Surface",surface},{"Case",c.ToString()},{"Zone",z.ToString()}};
                        if(branch!=null&&cp.State==AutoLookupState.Resolved)
                        {
                            var rows=source!.Bundle.Records.Where(r=>cp.RecordIds.Contains(r.RecordKey));
                            if(rows.Any(r=>r.TableOrFigure!=branch.Table)) {coefficients.Remove(prefix+"_Cpe");Block(prefix+"_Cpe","Roof coefficient source table disagrees with form/incidence/pitch route",AutoLookupState.RightsOrEditionBlocked);}
                        }
                        foreach(string factor in new[]{"Ka","Kce","Kl","Kp","Kci","Kv"})Lookup(prefix+"_"+factor,factor,state,factorScope,
                            factor=="Ka"?new(){{"Area",site.TributaryAreaM2}}:factor=="Kl"?new(){{"Parapet",site.ParapetHeightM}}:null);
                        if(surface=="Windward")Lookup(prefix+"_Mz_cat","Mz",state,legacy?new(){{"Terrain",F(site.Terrain)}}:null,legacy?new(){{"Height",site.WindwardHeightM}}:new(){{"Height",site.WindwardHeightM},{"Terrain",site.Terrain}});
                    }
                }
            }
        }
        bool complete=usable&&contextOk&&values.All(v=>v.State==AutoLookupState.Resolved);
        var options=geometry.Options.SetItem("AutoLookup","true").SetItem("RegionCode",site.Region).SetItem("Jurisdiction",site.Country).SetItem("AutoLookupComplete",complete.ToString()).SetItem("GeometrySource","ENGINEER REVIEW INPUT").SetItem("GeometryOverrideReason",site.GeometryReason)
            .SetItem("AutoLookupSite",System.Text.Json.JsonSerializer.Serialize(site,new System.Text.Json.JsonSerializerOptions {NumberHandling=System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals})).SetItem("LookupRightsReference",source?.RightsReference??"MISSING").SetItem("LookupAdoptionReference",source?.AdoptionReference??"MISSING").SetItem("LookupTableDecisions",System.Text.Json.JsonSerializer.Serialize(source?.TableDecisions)).SetItem("ParapetHeightM",F(site.ParapetHeightM))
            .SetItem("LookupDatasetHash",source?.Bundle.Manifest.ContentHash??"MISSING").SetItem("LookupDatasetId",source?.Bundle.Manifest.BundleId??"MISSING")
            .SetItem("RoofBranchDecisions",System.Text.Json.JsonSerializer.Serialize(branches)).SetItem("SourceRevisionStatus","BASE-2021 / AMENDMENT APPLICABILITY UNCONFIRMED").SetItem("AutoLookupDiagnostics",System.Text.Json.JsonSerializer.Serialize(values.ToImmutable())).SetItem("AutoLookupStatus",source?.Synthetic==true?"SYNTHETIC AUTO LOOKUP / NOT FOR DESIGN":"PRIVATE LOCAL REVIEW / NOT QUALIFIED FOR DESIGN");
        var request=geometry with {ProcedureName=AsNz2021ReviewCalculation.Procedure,ExplicitCoefficients=coefficients.ToImmutable(),ExposureOrTerrain="TC"+F(site.Terrain),EnclosureClassification=site.Enclosure,Options=options};
        AsNzAutoLookupReceipts.Bind(request,complete,source?.Synthetic==true,usable&&contextOk);
        return new(request,values.ToImmutable(),complete,complete?(source!.Synthetic?"SYNTHETIC AUTO LOOKUP / NOT FOR DESIGN":"PRIVATE SOURCE REVIEW / NOT QUALIFIED FOR DESIGN"):"LOCAL SOURCE DATA UNAVAILABLE / OWNER RIGHTS OR AMENDMENT EVIDENCE REQUIRED / INPUTS BLOCKED");
    }
}
