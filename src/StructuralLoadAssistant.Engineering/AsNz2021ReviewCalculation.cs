using System.Collections.Immutable;
using StructuralLoadAssistant.Domain;
namespace StructuralLoadAssistant.Engineering;

public sealed record AsNz2021Case(string Id, WindDirection Direction, LimitState LimitState, string Surface,
    int RoofCase, int Zone, double? FromM, double? ToM, double BearingDegrees, double? DesignSpeed,
    double? Cpe, double? ExternalShape, double? InternalShape, double? ExternalPa, double? InternalPa,
    double? NetPa, Vec3? OutwardNormal, Vec3? PressureVectorPa, string Status, string SourceReference)
{
    public AsNz2021MethodDecision? MethodDecision {get;init;}
    public ImmutableDictionary<string,CoefficientInputEvidence> FactorEvidence {get;init;}=ImmutableDictionary<string,CoefficientInputEvidence>.Empty;
    public double? PressureBasisPa {get;init;}
}

public static class AsNz2021ReviewCalculation
{
    public const string Procedure = "ASNZ2021_EXPLICIT_MATRIX";
    public static EngineeringCalculationRun Execute(EngineeringCalculationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (new[] { request.BasicWindSpeed, request.BuildingLengthM, request.BuildingWidthM, request.ReferenceHeightM, request.RoofSlopeDegrees, request.WindAngleDegrees, request.GroundElevationM, request.EaveHeightM, request.RidgeHeightM }.Any(v => !double.IsFinite(v)) || request.ExplicitCoefficients.Values.Any(e => !double.IsFinite(e.Value))) throw new ArgumentException("Finite request geometry and coefficient values required.");
        var rows = ImmutableArray.CreateBuilder<AsNz2021Case>();
        var traces = ImmutableArray.CreateBuilder<CalculationTraceNode>();
        var issues = ImmutableArray.CreateBuilder<CalculationIssue>();
        var quantities = ImmutableDictionary.CreateBuilder<string, double>();
        var cache = new Dictionary<string,double>();
        double Input(string key, bool positive = true)
        {
            if (cache.TryGetValue(key, out var cached)) return cached;
            if (request.ExplicitCoefficients.TryGetValue(key, out var e) && double.IsFinite(e.Value)
                && (!positive || e.Value > 0) && !string.IsNullOrWhiteSpace(e.Document) && !string.IsNullOrWhiteSpace(e.ClauseOrTable))
            {
                traces.Add(new("INPUT-" + key, "Explicit input", key, "Explicit engineer input", $"{key} = {e.Value:R}",
                    $"{e.Value:R} {e.Unit}", e.Value, e.Unit, e.Document + " " + e.ClauseOrTable,
                    e.Provenance + ": " + e.SourceNotes, [], []));
                cache[key] = e.Value; return e.Value;
            }
            issues.Add(new("MISSING-" + key, CalculationIssueSeverity.Error, "AS/NZS matrix", "INPUT REQUIRED: " + key));
            cache[key] = double.NaN; return double.NaN;
        }
        bool geometryOk = request.StandardEdition == "AS/NZS 1170.2:2021" && Enum.IsDefined(request.LimitState) && Enum.IsDefined(request.WindDirection) && new[] { request.BuildingLengthM, request.BuildingWidthM, request.ReferenceHeightM }.All(v => double.IsFinite(v) && v > 0)
            && double.IsFinite(request.WindAngleDegrees) && double.IsFinite(request.RoofSlopeDegrees) && request.RoofSlopeDegrees >= 0 && request.RoofSlopeDegrees < 90
            && request.EaveHeightM > request.GroundElevationM && request.RidgeHeightM >= request.EaveHeightM
            && AsNz2021MethodDecision.Evaluate(request.RoofForm,request.RoofSlopeDegrees,0,0,request.ReferenceHeightM/request.BuildingLengthM,request.BuildingLengthM/request.BuildingWidthM,"UNKNOWN","UNKNOWN",1,[]).ReviewGeometrySupported;
        if (!geometryOk) issues.Add(new("UNSUPPORTED_GEOMETRY", CalculationIssueSeverity.Error, "Geometry", "Only explicit rectangular flat/monoslope geometry is supported. Ground datum, roof plane and applicability require engineer review."));
        issues.Add(new("GEOMETRY_REVIEW_REQUIRED", CalculationIssueSeverity.Warning,"Geometry","Supplied footprint, average height, pitch and roof run require engineering adjudication; no physical roof plane or full-span pitch/rise verified."));
        double mz = Input("Mz_cat"), ms = Input("Ms"), mt = Input("Mt"), mc = Input("Mc"), rho = Input("rho"), dyn = Input("Cdyn");
        double slopeBearing = Input("SlopeBearing", false);
        foreach (var state in new[] { LimitState.Uls, LimitState.Sls })
        {
            double vr = Input("VR_" + state), period = Input("R_" + state);
            double[] site = Enumerable.Range(0, 8).Select(i => vr * Input("Md_" + i * 45) * mz * ms * mt * mc).ToArray();
            for (int i = 0; i < 8; i++) if (double.IsFinite(site[i])) {
                string id=$"Vsit_{state}_{i*45}"; quantities[id]=site[i];
                traces.Add(new(id,"Site speed",id,"VR Md Mz,cat Ms Mt Mc",$"{vr:R} × {Input("Md_"+i*45):R} × {mz:R} × {ms:R} × {mt:R} × {mc:R}",$"{site[i]:R} m/s",site[i],"m/s","AS/NZS 1170.2:2021 Eq 2.2","Explicit supplied regional and direction inputs",["INPUT-VR_"+state,"INPUT-Md_"+i*45,"INPUT-Mz_cat","INPUT-Ms","INPUT-Mt","INPUT-Mc"],[]));
            }
            foreach (var direction in Enum.GetValues<WindDirection>())
            {
                double theta = direction switch { WindDirection.PositiveX => 0, WindDirection.PositiveY => 90, WindDirection.NegativeX => 180, _ => 270 };
                double bearing = ((request.WindAngleDegrees + theta) % 360 + 360) % 360;
                double speed = site.All(v => double.IsFinite(v) && v > 0) ? AsNz2021MatrixEngine.DesignSpeed(site, bearing, state) : double.NaN;
                if (double.IsFinite(speed)) {
                    string id=$"Vdes_{state}_{direction}";quantities[id]=speed;
                    traces.Add(new(id,"Design speed",id,"max interpolated Vsit in θ ±45°; ULS >=30 m/s",$"bearing {bearing:R}°, sector [{bearing-45:R},{bearing+45:R}], eight Vsit operands [{string.Join(",",site.Select(v=>v.ToString("R")))}]",$"{speed:R} m/s",speed,"m/s","AS/NZS 1170.2:2021 Cl 2.3","Sector boundary interpolation and cardinal maxima; orientation relative true North",Enumerable.Range(0,8).Select(i=>$"Vsit_{state}_{i*45}").ToImmutableArray(),[]));
                }
                double depth = direction is WindDirection.PositiveX or WindDirection.NegativeX ? request.BuildingLengthM : request.BuildingWidthM;
                double breadth = direction is WindDirection.PositiveX or WindDirection.NegativeX ? request.BuildingWidthM : request.BuildingLengthM;
                Vec3 along = direction switch { WindDirection.PositiveX => new(1,0,0), WindDirection.NegativeX => new(-1,0,0), WindDirection.PositiveY => new(0,1,0), _ => new(0,-1,0) };
                void Surface(string surface, int roofCase, int zone, double from, double to, Vec3 normal)
                {
                    string prefix = $"{direction}_{surface}_{roofCase}_{zone}";
                    bool outside = geometryOk && to <= from;
                    double cpe = outside ? double.NaN : Input(prefix + "_Cpe", false);
                    double ka = outside ? double.NaN : Input(prefix + "_Ka"), kce = outside ? double.NaN : Input(prefix + "_Kce"), kl = outside ? double.NaN : Input(prefix + "_Kl"), kp = outside ? double.NaN : Input(prefix + "_Kp");
                    double kci = outside ? double.NaN : Input(prefix + "_Kci"), kv = outside ? double.NaN : Input(prefix + "_Kv");
                    foreach (string sign in new[] { "Positive", "Negative" })
                    {
                        double cpi = outside ? double.NaN : Input("Cpi_" + sign, false);
                        var method = AsNz2021MethodDecision.Evaluate(request.RoofForm,request.RoofSlopeDegrees,slopeBearing,bearing,request.ReferenceHeightM/depth,depth/breadth,request.Options.GetValueOrDefault("Jurisdiction","UNKNOWN"),request.Options.GetValueOrDefault("RegionCode","UNKNOWN"),period,request.SelectedAmendments);
                        bool valid = geometryOk && method.ReviewGeometrySupported && new[] { speed,rho,dyn,cpe,ka,kce,kl,kp,cpi,kci,kv,slopeBearing,period }.All(double.IsFinite);
                        if (valid && ((sign == "Positive" && cpi < 0) || (sign == "Negative" && cpi > 0))) valid = false;
                        var p = valid ? AsNz2021MatrixEngine.Pressure(speed,rho,dyn,cpe,ka,kce,kl,kp,cpi,kci,kv) : (double.NaN,double.NaN,double.NaN);
                        string id = $"{state}_{prefix}_{sign}";
                        string status = outside ? "NOT_APPLICABLE" : valid ? "EXPLICIT REVIEW ARITHMETIC / APPLICABILITY UNVERIFIED" : "INPUT_REQUIRED";
                        double? Finite(double v) => double.IsFinite(v) ? v : null;
                        rows.Add(new(id,direction,state,surface,roofCase,zone,Finite(from),Finite(to),bearing,Finite(speed),Finite(cpe),Finite(cpe*ka*kce*kl*kp),Finite(cpi*kci*kv),
                            Finite(p.Item1),Finite(p.Item2),Finite(p.Item3), valid ? normal : null,valid ? new Vec3(-p.Item3*normal.X,-p.Item3*normal.Y,-p.Item3*normal.Z) : null,status,
                            "AS/NZS 1170.2:2021 Cl 2.2, 2.3, 2.4(1), 5.2(1)/(2); surface applicability and selected coefficients require review")
                        {
                            PressureBasisPa=valid?0.5*rho*speed*speed*dyn:null,
                            MethodDecision=method,
                            FactorEvidence=request.ExplicitCoefficients.Where(e=>e.Key.StartsWith(prefix+"_")||e.Key=="Cpi_"+sign||e.Key is "rho" or "Cdyn").ToImmutableDictionary()
                        });
                        if (valid) traces.Add(new(id,"Surface pressure",id,"pe = 0.5 rho Vdes² Cpe Ka Kce Kl Kp Cdyn; pi = 0.5 rho Vdes² Cpi Kci Kv Cdyn; pn = pe - pi",
                            $"pe = 0.5 × {rho:R} × {speed:R}² × {cpe:R} × {ka:R} × {kce:R} × {kl:R} × {kp:R} × {dyn:R}; pi = 0.5 × {rho:R} × {speed:R}² × {cpi:R} × {kci:R} × {kv:R} × {dyn:R}; pn = {p.Item1:R} - ({p.Item2:R})",
                            $"{p.Item3:R} Pa",p.Item3,"Pa","AS/NZS 1170.2:2021 Eqs 2.4(1), 5.2(1)/(2)","Explicit-input review arithmetic; positive net acts inward. Selected coefficient applicability is not qualified.",
                            new[]{ $"Vdes_{state}_{direction}","INPUT-rho","INPUT-Cdyn","INPUT-Cpi_"+sign }.Concat(new[]{"Cpe","Ka","Kce","Kl","Kp","Kci","Kv"}.Select(f=>"INPUT-"+prefix+"_"+f)).ToImmutableArray(),[]));
                    }
                }
                Surface("Windward",0,0,0,depth,new(-along.X,-along.Y,0));
                Surface("Leeward",0,0,0,depth,along);
                double h = request.ReferenceHeightM;
                double[] side = [0,h,2*h,3*h,depth];
                for (int z=0;z<4;z++) foreach (string face in new[] { "SideLeft", "SideRight" })
                    Surface(face,0,z,Math.Min(depth,side[z]),Math.Min(depth,side[z+1]),face == "SideLeft" ? new(-along.Y,along.X,0) : new(along.Y,-along.X,0));
                double[] roof = [0,0.5*h,h,2*h,3*h,depth];
                double a = request.RoofSlopeDegrees*Math.PI/180, b = slopeBearing*Math.PI/180;
                Vec3 roofNormal = new(-Math.Sin(a)*Math.Cos(b),-Math.Sin(a)*Math.Sin(b),Math.Cos(a));
                for (int c=1;c<=2;c++) for (int z=0;z<5;z++) Surface("Roof",c,z,Math.Min(depth,roof[z]),Math.Min(depth,roof[z+1]),roofNormal);
            }
        }
        bool success = !issues.Any(i => i.Severity == CalculationIssueSeverity.Error) && rows.Any(r => r.NetPa.HasValue) && rows.All(r => r.Status != "INPUT_REQUIRED");
        bool synthetic = request.ExplicitCoefficients.Values.Any(e => e.Provenance == DataProvenance.SyntheticDemoFixture);
        return new("asnz-matrix-"+Guid.NewGuid().ToString("N"),DateTimeOffset.UtcNow,request,
            synthetic ? DataProvenance.SyntheticDemoFixture : DataProvenance.Unverified,success,false,false,false,false,
            !success ? "BLOCKED / INPUTS OR GEOMETRY REQUIRED" : synthetic ? "SYNTHETIC - NOT FOR DESIGN" : "SOURCE RIGHTS / VERIFIED TABLE DATA REQUIRED",[],[],traces.DistinctBy(t=>t.NodeId).ToImmutableArray(),issues.DistinctBy(i=>i.Code).ToImmutableArray(),quantities.ToImmutable())
            { AsNz2021Cases = rows.ToImmutable() };
    }
}


