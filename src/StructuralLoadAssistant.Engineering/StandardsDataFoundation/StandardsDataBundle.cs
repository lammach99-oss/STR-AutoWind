using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StructuralLoadAssistant.Engineering;

/// <summary>Represents a loaded, in-memory standards dataset bundle with typed records and provenance.</summary>
public sealed class StandardsDataBundle
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public StandardsDataBundleManifest Manifest { get; }
    public ImmutableArray<CoefficientRecord> Records { get; }
    public string BundleDirectory { get; }
    public ImmutableDictionary<string, ImmutableArray<byte>> RawComponentBytes { get; }
    public bool IsBytesVerified { get; }

    public StandardsDataBundle(
        StandardsDataBundleManifest manifest,
        ImmutableArray<CoefficientRecord> records,
        string bundleDirectory = "",
        ImmutableDictionary<string, ImmutableArray<byte>>? rawComponentBytes = null,
        bool isBytesVerified = false)
    {
        Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        Records = records;
        BundleDirectory = bundleDirectory;
        RawComponentBytes = rawComponentBytes ?? ImmutableDictionary<string, ImmutableArray<byte>>.Empty;
        IsBytesVerified = isBytesVerified;
    }

    /// <summary>Validates that a relative path does not attempt traversal or use absolute/rooted schemes.</summary>
    public static void ValidateSafeRelativePath(string relPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relPath);
        if (Path.IsPathRooted(relPath))
            throw new ArgumentException($"Component path '{relPath}' cannot be rooted / absolute.", nameof(relPath));
        if (relPath.Contains("..", StringComparison.Ordinal) || relPath.StartsWith('/') || relPath.StartsWith('\\') || relPath.Contains(':'))
            throw new ArgumentException($"Component path '{relPath}' contains illegal traversal or characters.", nameof(relPath));
    }

    /// <summary>Retrieves all records matching the specified parameter category.</summary>
    public ImmutableArray<CoefficientRecord> GetRecordsForCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category)) return [];
        return Records.Where(r => string.Equals(r.ParameterCategory, category, StringComparison.OrdinalIgnoreCase)).ToImmutableArray();
    }

    /// <summary>Finds a single record matching exact categorical and numerical conditions and system type, returning null if ambiguous or missing.</summary>
    public CoefficientRecord? FindExactRecord(
        string category,
        IDictionary<string, string> categoricalConditions,
        IDictionary<string, double>? numericalConditions = null,
        string? systemType = null)
    {
        ArgumentNullException.ThrowIfNull(categoricalConditions);
        var candidates = GetRecordsForCategory(category);
        var matches = candidates.Where(c => MatchesCategorical(c.CategoricalConditions, categoricalConditions) &&
                                            MatchesNumerical(c.NumericalConditions, numericalConditions) &&
                                            MatchesSystem(c, systemType)).ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    private static bool MatchesCategorical(
        IReadOnlyDictionary<string, string> recordConditions,
        IDictionary<string, string> queryConditions)
    {
        // Allow procedure queries to omit redundant "System" condition if handled by MatchesSystem
        int expectedCount = recordConditions.ContainsKey("System") && !queryConditions.ContainsKey("System")
            ? recordConditions.Count - 1
            : recordConditions.Count;

        if (expectedCount != queryConditions.Count) return false;

        foreach (var (k, v) in queryConditions)
        {
            if (!recordConditions.TryGetValue(k, out var val) ||
                !string.Equals(val, v, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        return true;
    }

    private static bool MatchesNumerical(
        IReadOnlyDictionary<string, double> recordConditions,
        IDictionary<string, double>? queryConditions)
    {
        // Bidirectional exact matching:
        // If record has no numerical conditions, query must also have no numerical conditions.
        if (recordConditions.Count == 0)
        {
            return queryConditions == null || queryConditions.Count == 0;
        }

        // If record has numerical conditions, query must supply exact same count
        if (queryConditions == null || recordConditions.Count != queryConditions.Count)
        {
            return false;
        }

        foreach (var (k, v) in queryConditions)
        {
            if (!double.IsFinite(v)) return false;
            if (!recordConditions.TryGetValue(k, out var val) || !double.IsFinite(val))
            {
                return false;
            }

            // Strict exact equality
            if (BitConverter.DoubleToInt64Bits(val) != BitConverter.DoubleToInt64Bits(v))
            {
                return false;
            }
        }
        return true;
    }

    private static bool MatchesSystem(CoefficientRecord record, string? systemType)
    {
        if (string.IsNullOrWhiteSpace(systemType)) return true;
        if (record.CategoricalConditions.TryGetValue("System", out var s))
        {
            if (systemType.Equals("MWFRS", StringComparison.OrdinalIgnoreCase))
            {
                return s.Equals("MWFRS", StringComparison.OrdinalIgnoreCase) ||
                       s.Equals("Directional", StringComparison.OrdinalIgnoreCase) ||
                       s.Equals("Envelope", StringComparison.OrdinalIgnoreCase);
            }
            if (systemType.Equals("CC", StringComparison.OrdinalIgnoreCase) || systemType.Equals("ComponentsAndCladding", StringComparison.OrdinalIgnoreCase))
            {
                return s.Equals("CC", StringComparison.OrdinalIgnoreCase) ||
                       s.Equals("ComponentsAndCladding", StringComparison.OrdinalIgnoreCase);
            }
            return string.Equals(s, systemType, StringComparison.OrdinalIgnoreCase);
        }

        // Recognized system-neutral categories applicable across procedures
        if (record.ParameterCategory.Equals("InternalPressure", StringComparison.OrdinalIgnoreCase) ||
            record.ParameterCategory.Equals("VelocityPressure", StringComparison.OrdinalIgnoreCase) ||
            record.ParameterCategory.Equals("RegionalWindSpeed", StringComparison.OrdinalIgnoreCase) ||
            record.ParameterCategory.Equals("TerrainHeightMultiplier", StringComparison.OrdinalIgnoreCase))
        {
            return systemType.Equals("MWFRS", StringComparison.OrdinalIgnoreCase) ||
                   systemType.Equals("CC", StringComparison.OrdinalIgnoreCase) ||
                   systemType.Equals("ComponentsAndCladding", StringComparison.OrdinalIgnoreCase);
        }

        // Unknown or unmatched system type fails closed
        return false;
    }

    /// <summary>
    /// Finds a single record applicable to an explicit ProcedureSelection, enforcing standard family,
    /// edition, and procedure-scoped system isolation.
    /// </summary>
    public CoefficientRecord? FindRecordForProcedure(
        ProcedureSelection procedure,
        string category,
        IDictionary<string, string> categoricalConditions,
        IDictionary<string, double>? numericalConditions = null)
    {
        ArgumentNullException.ThrowIfNull(procedure);
        ArgumentNullException.ThrowIfNull(categoricalConditions);

        if (procedure.Family != Manifest.Family) return null;
        if (!string.IsNullOrWhiteSpace(procedure.Edition) &&
            !string.Equals(procedure.Edition, Manifest.Edition, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return FindExactRecord(category, categoricalConditions, numericalConditions, procedure.SystemType);
    }

    /// <summary>Calculates a deterministic canonical content hash across all sorted records.</summary>
    public string ComputeCanonicalHash()
    {
        var sorted = Records.OrderBy(r => r.RecordKey, StringComparer.Ordinal).ToList();

        if (string.Equals(Manifest.CanonicalHashAlgorithmVersion, "3.0", StringComparison.Ordinal))
        {
            using var ms = new MemoryStream();
            void WritePrefixed(string val)
            {
                var utf8 = Encoding.UTF8.GetBytes(val ?? string.Empty);
                var header = Encoding.UTF8.GetBytes($"{utf8.Length}:");
                ms.Write(header);
                ms.Write(utf8);
                ms.WriteByte((byte)';');
            }
            void WritePrefixedInt(int i) => WritePrefixed(i.ToString(System.Globalization.CultureInfo.InvariantCulture));
            void WritePrefixedBool(bool b) => WritePrefixed(b ? "T" : "F");
            void WritePrefixedDouble(double d)
            {
                if (!double.IsFinite(d)) throw new InvalidOperationException($"Non-finite double '{d}' cannot be encoded in canonical hash.");
                if (d == 0.0 || BitConverter.DoubleToInt64Bits(d) == unchecked((long)0x8000000000000000)) d = 0.0;
                WritePrefixed(d.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            }

            WritePrefixed("CANONICAL_STANDARDS_BUNDLE_V3");
            WritePrefixed(Manifest.BundleId);
            WritePrefixed(Manifest.Family.ToString());
            WritePrefixed(Manifest.Edition);
            WritePrefixed(Manifest.Rights.ToString());
            WritePrefixedInt(Manifest.Amendments.Length);
            foreach (var amd in Manifest.Amendments.OrderBy(a => a, StringComparer.Ordinal))
            {
                WritePrefixed(amd);
            }
            WritePrefixedInt(Manifest.CoverageMatrix.Length);
            foreach (var cov in Manifest.CoverageMatrix.OrderBy(c => c.CategoryKey, StringComparer.Ordinal))
            {
                WritePrefixed(cov.CategoryKey);
                WritePrefixed(cov.Description);
                WritePrefixedBool(cov.IsRequired);
                WritePrefixedInt(cov.AvailableRecordCount);
            }

            WritePrefixedInt(sorted.Count);
            foreach (var r in sorted)
            {
                WritePrefixed(r.RecordKey);
                WritePrefixed(r.ParameterCategory);
                WritePrefixed(r.TableOrFigure);
                WritePrefixed(r.ClauseReference);
                WritePrefixed(r.SignConvention);
                WritePrefixed(r.Unit);
                WritePrefixed(r.Interpolation.ToString());
                WritePrefixedDouble(r.Value);
                WritePrefixed(r.ApplicabilityNotes ?? string.Empty);

                WritePrefixedInt(r.CategoricalConditions.Count);
                foreach (var (k, v) in r.CategoricalConditions.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                {
                    WritePrefixed(k);
                    WritePrefixed(v);
                }

                WritePrefixedInt(r.NumericalConditions.Count);
                foreach (var (k, v) in r.NumericalConditions.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                {
                    WritePrefixed(k);
                    WritePrefixedDouble(v);
                }
            }

            return Convert.ToHexString(SHA256.HashData(ms.ToArray()));
        }

        if (string.Equals(Manifest.CanonicalHashAlgorithmVersion, "2.0", StringComparison.Ordinal))
        {
            using var ms = new MemoryStream();
            void WritePrefixed(string val)
            {
                var utf8 = Encoding.UTF8.GetBytes(val ?? string.Empty);
                var header = Encoding.UTF8.GetBytes($"{utf8.Length}:");
                ms.Write(header);
                ms.Write(utf8);
                ms.WriteByte((byte)';');
            }

            WritePrefixed("CANONICAL_STANDARDS_BUNDLE_V2");
            WritePrefixed(Manifest.BundleId);
            WritePrefixed(Manifest.Family.ToString());
            WritePrefixed(Manifest.Edition);
            WritePrefixed(Manifest.Rights.ToString());
            WritePrefixed(string.Join(",", Manifest.Amendments.OrderBy(a => a, StringComparer.Ordinal)));
            WritePrefixed(string.Join(",", Manifest.CoverageMatrix.OrderBy(c => c.CategoryKey, StringComparer.Ordinal).Select(c => $"{c.CategoryKey}:{c.AvailableRecordCount}:{(c.IsRequired ? "R" : "O")}")));

            foreach (var r in sorted)
            {
                var sortedCat = string.Join(",", r.CategoricalConditions.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}={kv.Value}"));
                var sortedNum = string.Join(",", r.NumericalConditions.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}={kv.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}"));

                WritePrefixed(r.RecordKey);
                WritePrefixed(r.ParameterCategory);
                WritePrefixed(r.TableOrFigure);
                WritePrefixed(r.ClauseReference);
                WritePrefixed(r.SignConvention);
                WritePrefixed(r.Unit);
                WritePrefixed(r.Interpolation.ToString());
                WritePrefixed(r.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                WritePrefixed(sortedCat);
                WritePrefixed(sortedNum);
                WritePrefixed(r.ApplicabilityNotes ?? string.Empty);
            }

            return Convert.ToHexString(SHA256.HashData(ms.ToArray()));
        }

        if (string.Equals(Manifest.CanonicalHashAlgorithmVersion, "1.0", StringComparison.Ordinal))
        {
            var sb = new StringBuilder();
        sb.Append("CANONICAL_STANDARDS_BUNDLE_V1|")
          .Append(Manifest.BundleId).Append('|')
          .Append(Manifest.Family).Append('|')
          .Append(Manifest.Edition).Append('|')
          .Append(Manifest.Rights).Append('|')
          .Append(string.Join(",", Manifest.Amendments.OrderBy(a => a, StringComparer.Ordinal))).Append('|')
          .Append(string.Join(",", Manifest.CoverageMatrix.OrderBy(c => c.CategoryKey, StringComparer.Ordinal).Select(c => $"{c.CategoryKey}:{c.AvailableRecordCount}:{(c.IsRequired ? "R" : "O")}")))
          .AppendLine();

        foreach (var r in sorted)
        {
            var sortedCat = string.Join(",", r.CategoricalConditions.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}={kv.Value}"));
            var sortedNum = string.Join(",", r.NumericalConditions.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}={kv.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}"));

            sb.Append(r.RecordKey).Append('|')
              .Append(r.ParameterCategory).Append('|')
              .Append(r.TableOrFigure).Append('|')
              .Append(r.ClauseReference).Append('|')
              .Append(r.SignConvention).Append('|')
              .Append(r.Unit).Append('|')
              .Append(r.Interpolation).Append('|')
              .Append(r.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|')
              .Append(sortedCat).Append('|')
              .Append(sortedNum).Append('|')
              .Append(r.ApplicabilityNotes ?? string.Empty).AppendLine();
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
        }

        throw new NotSupportedException(
            $"Unsupported canonical hash algorithm version '{Manifest.CanonicalHashAlgorithmVersion}'. Only 1.0, 2.0, and 3.0 are supported.");
    }

    /// <summary>Loads and parses a standards dataset bundle from a folder containing manifest.json.</summary>
    public static StandardsDataBundle LoadFromDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"Standards dataset directory not found at '{directoryPath}'.");

        var manifestPath = Path.Combine(directoryPath, "manifest.json");
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException($"Dataset manifest not found at '{manifestPath}'.", manifestPath);

        var manifestJson = File.ReadAllText(manifestPath).TrimStart('\uFEFF');
        var manifest = JsonSerializer.Deserialize<StandardsDataBundleManifest>(manifestJson, JsonOptions)
            ?? throw new InvalidDataException("Failed to deserialize StandardsDataBundleManifest.");

        var recordsBuilder = ImmutableArray.CreateBuilder<CoefficientRecord>();
        var bytesBuilder = ImmutableDictionary.CreateBuilder<string, ImmutableArray<byte>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (relPath, _) in manifest.ComponentFiles)
        {
            ValidateSafeRelativePath(relPath);
            var componentFullPath = Path.Combine(directoryPath, relPath);
            if (File.Exists(componentFullPath))
            {
                var bytes = File.ReadAllBytes(componentFullPath);
                bytesBuilder[relPath] = bytes.ToImmutableArray();
                var componentJson = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
                var componentRecords = JsonSerializer.Deserialize<CoefficientRecord[]>(componentJson, JsonOptions);
                if (componentRecords != null)
                {
                    recordsBuilder.AddRange(componentRecords);
                }
            }
        }

        return new StandardsDataBundle(
            manifest,
            recordsBuilder.ToImmutable(),
            directoryPath,
            bytesBuilder.ToImmutable(),
            isBytesVerified: bytesBuilder.Count == manifest.ComponentFiles.Count);
    }

    /// <summary>Parses an in-memory manifest and component file map into a bundle.</summary>
    public static StandardsDataBundle LoadFromMemory(string manifestJson, IDictionary<string, string> componentFilesJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestJson);
        ArgumentNullException.ThrowIfNull(componentFilesJson);

        var manifest = JsonSerializer.Deserialize<StandardsDataBundleManifest>(manifestJson, JsonOptions)
            ?? throw new InvalidDataException("Failed to deserialize StandardsDataBundleManifest from JSON string.");

        var recordsBuilder = ImmutableArray.CreateBuilder<CoefficientRecord>();
        var bytesBuilder = ImmutableDictionary.CreateBuilder<string, ImmutableArray<byte>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (filename, jsonText) in componentFilesJson)
        {
            ValidateSafeRelativePath(filename);
            var bytes = Encoding.UTF8.GetBytes(jsonText ?? string.Empty);
            bytesBuilder[filename] = bytes.ToImmutableArray();

            if (!string.IsNullOrWhiteSpace(jsonText))
            {
                var componentRecords = JsonSerializer.Deserialize<CoefficientRecord[]>(jsonText, JsonOptions);
                if (componentRecords != null)
                {
                    recordsBuilder.AddRange(componentRecords);
                }
            }
        }

        return new StandardsDataBundle(
            manifest,
            recordsBuilder.ToImmutable(),
            string.Empty,
            bytesBuilder.ToImmutable(),
            isBytesVerified: bytesBuilder.Count == manifest.ComponentFiles.Count);
    }

    /// <summary>Parses an in-memory manifest and component raw bytes map into a bundle.</summary>
    public static StandardsDataBundle LoadFromMemory(string manifestJson, IDictionary<string, byte[]> componentFilesBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestJson);
        ArgumentNullException.ThrowIfNull(componentFilesBytes);

        var manifest = JsonSerializer.Deserialize<StandardsDataBundleManifest>(manifestJson, JsonOptions)
            ?? throw new InvalidDataException("Failed to deserialize StandardsDataBundleManifest from JSON string.");

        var recordsBuilder = ImmutableArray.CreateBuilder<CoefficientRecord>();
        var bytesBuilder = ImmutableDictionary.CreateBuilder<string, ImmutableArray<byte>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (filename, bytes) in componentFilesBytes)
        {
            ValidateSafeRelativePath(filename);
            var immBytes = (bytes ?? []).ToImmutableArray();
            bytesBuilder[filename] = immBytes;

            if (bytes != null && bytes.Length > 0)
            {
                var jsonText = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
                var componentRecords = JsonSerializer.Deserialize<CoefficientRecord[]>(jsonText, JsonOptions);
                if (componentRecords != null)
                {
                    recordsBuilder.AddRange(componentRecords);
                }
            }
        }

        return new StandardsDataBundle(
            manifest,
            recordsBuilder.ToImmutable(),
            string.Empty,
            bytesBuilder.ToImmutable(),
            isBytesVerified: bytesBuilder.Count == manifest.ComponentFiles.Count);
    }

    /// <summary>Deserializes coefficient records from declared raw component bytes map.</summary>
    public static ImmutableArray<CoefficientRecord> DeserializeRecordsFromBytes(
        ImmutableDictionary<string, ImmutableArray<byte>> componentBytes,
        IEnumerable<string>? orderedComponentKeys = null)
    {
        var recordsBuilder = ImmutableArray.CreateBuilder<CoefficientRecord>();
        var keys = orderedComponentKeys ?? componentBytes.Keys;

        foreach (var key in keys)
        {
            if (componentBytes.TryGetValue(key, out var bytes) && !bytes.IsDefaultOrEmpty)
            {
                var json = Encoding.UTF8.GetString(bytes.ToArray()).TrimStart('\uFEFF');
                var records = JsonSerializer.Deserialize<CoefficientRecord[]>(json, JsonOptions);
                if (records != null)
                {
                    recordsBuilder.AddRange(records);
                }
            }
        }
        return recordsBuilder.ToImmutable();
    }
}

