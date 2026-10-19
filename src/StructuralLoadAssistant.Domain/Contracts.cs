using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StructuralLoadAssistant.Domain;

public readonly record struct Vec3(double X, double Y, double Z)
{
    public double Dot(Vec3 other) => X * other.X + Y * other.Y + Z * other.Z;
    public double Length => Math.Sqrt(Dot(this));
    public Vec3 Unit()
    {
        if (!double.IsFinite(Length) || Length < 1e-12) throw new ArgumentException("Invalid vector");
        return this * (1 / Length);
    }
    public static Vec3 operator *(Vec3 v, double a) => new(v.X * a, v.Y * a, v.Z * a);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
}
public enum WindDirection { PositiveX, NegativeX, PositiveY, NegativeY }
public enum LimitState { Uls, Sls }
public sealed record WindState(WindDirection Direction, string InternalPressure, string RoofCase, LimitState LimitState)
{
    public string Id => $"{Direction}/{InternalPressure}/{RoofCase}/{LimitState}";
}
public enum StandardDataVerificationStatus { Missing, DraftUnverified, Reviewed, VerifiedForUse }
public enum DataProvenance { SyntheticDemoFixture, Unverified, Verified }
public sealed record StandardReference(string Document, string Clause, string Source);
public sealed record StandardManifest(string Id, string Edition, ImmutableArray<string> Revisions,
    StandardDataVerificationStatus Status, ImmutableArray<StandardReference> References)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Hash => SnapshotJson.Hash(this);
}
public sealed record CalculationTraceItem(string FormulaId, string InputsWithUnits, string ResultWithUnits, string Reference, string Warning);
public sealed record PressureSample(double ElevationM, double PressurePa);
public sealed record WindCalculationRun(string Id, DateTimeOffset CreatedAt, string ManifestHash,
    DataProvenance Provenance, WindState State, ImmutableArray<PressureSample> Profile,
    ImmutableArray<CalculationTraceItem> Trace)
{
    public bool IsEngineeringVerified => Provenance == DataProvenance.Verified;
}
public sealed record Project(string Id, string Name, string UnitSystem);
public sealed record FrameGeometry(string Id, string FacadeId, Vec3 Start, Vec3 End);
public sealed record ShellGeometry(string Id, string RoofPlaneId, ImmutableArray<Vec3> Vertices, Vec3 Local3, Vec3 OutwardNormal);
public sealed record EngineeringOverride(string ObjectId, string Field, string OldValue, string NewValue,
    string User, DateTimeOffset CreatedAt, string Reason);
public sealed record ExistingLoad(string ObjectId, string Pattern, string Kind, double ValueSI, string Owner);
public sealed record CsiModelSnapshot(string Id, string ModelId, string Product, string Version,
    string GeometryFingerprint, string ExistingLoadsFingerprint, ImmutableArray<string> Patterns,
    ImmutableArray<string> Cases, ImmutableArray<string> Combinations,
    ImmutableArray<FrameGeometry> Frames, ImmutableArray<ShellGeometry> Shells,
    ImmutableArray<ExistingLoad> ExistingLoads);
public static class SnapshotJson
{
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value);
    public static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Serialize(value))));
}
