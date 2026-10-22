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

    public StandardsDataBundle(
        StandardsDataBundleManifest manifest,
        ImmutableArray<CoefficientRecord> records,
        string bundleDirectory = "")
    {
        Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        Records = records;
        BundleDirectory = bundleDirectory;
    }

    /// <summary>Retrieves all records matching the specified parameter category.</summary>
    public ImmutableArray<CoefficientRecord> GetRecordsForCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category)) return [];
        return Records.Where(r => string.Equals(r.ParameterCategory, category, StringComparison.OrdinalIgnoreCase)).ToImmutableArray();
    }

    /// <summary>Finds a single record matching exact categorical conditions, returning null if ambiguous or missing.</summary>
    public CoefficientRecord? FindExactRecord(string category, IDictionary<string, string> categoricalConditions)
    {
        ArgumentNullException.ThrowIfNull(categoricalConditions);
        var candidates = GetRecordsForCategory(category);
        var matches = candidates.Where(c => MatchesCategorical(c.CategoricalConditions, categoricalConditions)).ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    private static bool MatchesCategorical(
        IReadOnlyDictionary<string, string> recordConditions,
        IDictionary<string, string> queryConditions)
    {
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

    /// <summary>Calculates a deterministic canonical content hash across all sorted records.</summary>
    public string ComputeCanonicalHash()
    {
        var sorted = Records.OrderBy(r => r.RecordKey, StringComparer.Ordinal).ToList();
        var sb = new StringBuilder();
        sb.Append(Manifest.BundleId).Append('|').Append(Manifest.Family).Append('|').Append(Manifest.Edition).AppendLine();
        foreach (var r in sorted)
        {
            sb.Append(r.RecordKey).Append('|')
              .Append(r.ParameterCategory).Append('|')
              .Append(r.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|')
              .Append(r.Unit).Append('|')
              .Append(r.Interpolation).AppendLine();
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
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

        var manifestJson = File.ReadAllText(manifestPath);
        var manifest = JsonSerializer.Deserialize<StandardsDataBundleManifest>(manifestJson, JsonOptions)
            ?? throw new InvalidDataException("Failed to deserialize StandardsDataBundleManifest.");

        var recordsBuilder = ImmutableArray.CreateBuilder<CoefficientRecord>();

        foreach (var (relPath, _) in manifest.ComponentFiles)
        {
            var componentFullPath = Path.Combine(directoryPath, relPath);
            if (File.Exists(componentFullPath))
            {
                var componentJson = File.ReadAllText(componentFullPath);
                var componentRecords = JsonSerializer.Deserialize<CoefficientRecord[]>(componentJson, JsonOptions);
                if (componentRecords != null)
                {
                    recordsBuilder.AddRange(componentRecords);
                }
            }
        }

        return new StandardsDataBundle(manifest, recordsBuilder.ToImmutable(), directoryPath);
    }

    /// <summary>Parses an in-memory manifest and component file map into a bundle.</summary>
    public static StandardsDataBundle LoadFromMemory(string manifestJson, IDictionary<string, string> componentFilesJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestJson);
        ArgumentNullException.ThrowIfNull(componentFilesJson);

        var manifest = JsonSerializer.Deserialize<StandardsDataBundleManifest>(manifestJson, JsonOptions)
            ?? throw new InvalidDataException("Failed to deserialize StandardsDataBundleManifest from JSON string.");

        var recordsBuilder = ImmutableArray.CreateBuilder<CoefficientRecord>();

        foreach (var (filename, jsonText) in componentFilesJson)
        {
            if (!string.IsNullOrWhiteSpace(jsonText))
            {
                var componentRecords = JsonSerializer.Deserialize<CoefficientRecord[]>(jsonText, JsonOptions);
                if (componentRecords != null)
                {
                    recordsBuilder.AddRange(componentRecords);
                }
            }
        }

        return new StandardsDataBundle(manifest, recordsBuilder.ToImmutable());
    }
}

