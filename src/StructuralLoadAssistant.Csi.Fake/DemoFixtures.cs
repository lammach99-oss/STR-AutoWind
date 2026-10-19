using System.Collections.Immutable;
using StructuralLoadAssistant.Domain;

namespace StructuralLoadAssistant.Csi.Fake;

public static class DemoFixtures
{
    public static readonly DateTimeOffset FixtureTime = new(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);
    public static WindState State => new(WindDirection.PositiveX, "IP_POS", "RoofCase1", LimitState.Uls);
    public static StandardManifest Manifest(string edition = "ASCE/SEI 7-22") => new(
        edition == "ASCE/SEI 7-22" ? "ASCE7_22-missing" : "ASNZ1170_2_2021-missing", edition,
        edition == "ASCE/SEI 7-22" ? ["Applicable supplements/errata: review pending"] : ["Amendment 1:2023 - unverified", "Amendment 2:2024 - unverified"],
        StandardDataVerificationStatus.Missing,
        [new(edition, "Normative tables/equations not loaded", "Licensed engineer-reviewed source required")]);
    public static WindCalculationRun Calculation(string edition = "ASCE/SEI 7-22") => new(
        "demo-pressure-" + Manifest(edition).Id, FixtureTime, Manifest(edition).Hash,
        DataProvenance.SyntheticDemoFixture, State,
        [new(3, 1200), new(6, 1600), new(9, 1800)],
        [new("SYNTHETIC", "z: 3, 6, 9 m", "p: 1200, 1600, 1800 Pa", "Demo fixture only", "NOT FOR ENGINEERING DESIGN")]);
    public static ImmutableArray<Vec3> Rectangle(double x1, double y1, double x2, double y2, bool south = true)
    {
        double Z(double y) => south ? 9 + .75 * y : 15 - .75 * y;
        return [new(x1,y1,Z(y1)),new(x2,y1,Z(y1)),new(x2,y2,Z(y2)),new(x1,y2,Z(y2))];
    }
    public static CsiModelSnapshot Model(bool includeCrossing = true)
    {
        ImmutableArray<FrameGeometry> frames = [
            new("B1","X+",new(0,0,3),new(0,10,3)),
            new("B2","X+",new(0,0,6),new(0,10,6)),
            new("B3","X+",new(0,0,9),new(0,10,9)),
            new("B-invalid","X+",new(0,20,6),new(0,25,6))];
        Vec3 south = new(0,-.6,.8), north = new(0,.6,.8);
        ImmutableArray<ShellGeometry> shells = [
            new("R1","Roof",Rectangle(0,0,5,4),south,south),
            new("R2","Roof",Rectangle(0,4,5,8,false),north * -1,north)];
        if (includeCrossing) shells = shells.Add(new("RX","Roof",Rectangle(4,8,6,10,false),north,north));
        ImmutableArray<string> patterns = ["WX_POS_IP_POS","WX_POS_IP_NEG","WX_NEG_IP_POS","WX_NEG_IP_NEG",
            "WY_POS_IP_POS","WY_POS_IP_NEG","WY_NEG_IP_POS","WY_NEG_IP_NEG","WALL"];
        ImmutableArray<ExistingLoad> loads = [new("R1","WX_POS_IP_POS","AreaLocal3",250,"Manual"), new("B1","WALL","FrameGlobalZ",-750,"Manual")];
        string fingerprint = SnapshotJson.Hash(new { frames, shells });
        return new("demo-scan-" + fingerprint[..12],"fictional-building","FAKE CSI","Simulation 1",fingerprint,
            SnapshotJson.Hash(loads),patterns,["CASE_WX","CASE_DEAD"],["COMBO_ULS","COMBO_SLS"],frames,shells,loads);
    }
}
