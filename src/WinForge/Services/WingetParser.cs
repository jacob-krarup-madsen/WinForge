using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using WingetStore.Models;

namespace WingetStore.Services;

public static partial class WingetParser
{
    [GeneratedRegex(@"^\(\d+(?:/\d+)?\)\s+(.+)\s+\[([^\]]+)\]$", RegexOptions.Compiled)]
    private static partial Regex HeaderRegex { get; }

    [GeneratedRegex(@"(\d+)%")]
    private static partial Regex PercentRegex { get; }

    [GeneratedRegex(@"\d+%")]
    private static partial Regex HasPercentRegex { get; }

    public static List<Dictionary<string, string>> ParseTable(string output)
    {
        var list = new List<Dictionary<string, string>>(); var lines = output.Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries); if (lines.Length < 3) return list;
        int headerIndex = FindHeaderLine(lines); if (headerIndex < 0 || !TryParseColumnPositions(lines[headerIndex], out var pos)) return list;
        for (int i = headerIndex + 2; i < lines.Length; i++) { string line = lines[i]; if (string.IsNullOrWhiteSpace(line) || line.Contains("upgrades available", StringComparison.OrdinalIgnoreCase) || line.Contains("upgrade available", StringComparison.OrdinalIgnoreCase)) continue; try { list.Add(ParseTableRow(line, pos)); } catch (Exception ex) { Debug.WriteLine($"ParseTable row failed: {ex.Message}"); } }
        return list;
    }

    internal static int FindHeaderLine(string[] lines) { for (int i = 0; i < lines.Length; i++) { if (lines[i].Contains("---")) return i - 1; } return -1; }

    internal static bool TryParseColumnPositions(string headerLine, out (int namePos, int idPos, int versionPos, int sourcePos, int matchPos, int availablePos) pos)
    {
        int idPos = headerLine.IndexOf("Id", StringComparison.OrdinalIgnoreCase), versionPos = headerLine.IndexOf("Version", StringComparison.OrdinalIgnoreCase);
        if (idPos == -1 || versionPos == -1 || idPos >= versionPos) { pos = default; return false; }
        pos = (0, idPos, versionPos, headerLine.IndexOf("Source", StringComparison.OrdinalIgnoreCase), headerLine.IndexOf("Match", StringComparison.OrdinalIgnoreCase), headerLine.IndexOf("Available", StringComparison.OrdinalIgnoreCase));
        return true;
    }

    internal static Dictionary<string, string> ParseTableRow(string line, (int namePos, int idPos, int versionPos, int sourcePos, int matchPos, int availablePos) pos)
    {
        string name = GetSubstring(line, pos.namePos, pos.idPos), id = GetSubstring(line, pos.idPos, pos.versionPos), version, extra = "", extraKey = "";
        if (pos.sourcePos != -1) { version = GetSubstring(line, pos.versionPos, pos.sourcePos); extra = GetSubstring(line, pos.sourcePos, line.Length); extraKey = "Source"; }
        else if (pos.matchPos != -1) { version = GetSubstring(line, pos.versionPos, pos.matchPos); extra = GetSubstring(line, pos.matchPos, line.Length); extraKey = "Match"; }
        else if (pos.availablePos != -1) { version = GetSubstring(line, pos.versionPos, pos.availablePos); extra = GetSubstring(line, pos.availablePos, line.Length); extraKey = "Available"; }
        else { version = GetSubstring(line, pos.versionPos, line.Length); }
        var dict = new Dictionary<string, string> { { "Name", name }, { "Id", id }, { "Version", version } };
        if (!string.IsNullOrEmpty(extraKey)) dict[extraKey] = extra;
        return dict;
    }

    public static List<WingetPackage> ParseDetailsList(string output)
    {
        var list = new List<WingetPackage>(); WingetPackage? current = null;
        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var match = HeaderRegex.Match(line.Trim());
            if (match.Success) { if (current != null && !string.IsNullOrEmpty(current.Id) && !current.Id.StartsWith("ARP\\", StringComparison.OrdinalIgnoreCase)) list.Add(current); current = new WingetPackage { Name = match.Groups[1].Value.Trim(), Id = match.Groups[2].Value.Trim(), Status = PackageStatus.Installed }; }
            else if (current != null)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("Publisher:", StringComparison.OrdinalIgnoreCase)) current.Publisher = trimmed["Publisher:".Length..].Trim();
                else if (trimmed.StartsWith("Version:", StringComparison.OrdinalIgnoreCase)) current.Version = trimmed["Version:".Length..].Trim();
                else if (trimmed.StartsWith("Origin Source:", StringComparison.OrdinalIgnoreCase)) current.Source = trimmed["Origin Source:".Length..].Trim();
            }
        }
        if (current != null && !string.IsNullOrEmpty(current.Id) && !current.Id.StartsWith("ARP\\", StringComparison.OrdinalIgnoreCase)) list.Add(current);
        return list;
    }

    public static WingetPackage ParsePackageDetails(string output, string packageId)
    {
        var package = new WingetPackage { Id = packageId }; var lines = output.Split(["\r\n", "\r", "\n"], StringSplitOptions.None); MetadataItem? currentParent = null;
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue; string trimmed = line.Trim(); if (TryParseFoundLine(trimmed, package)) continue;
            int indent = 0; while (indent < line.Length && line[indent] == ' ') indent++; int colonIdx = trimmed.IndexOf(':');
            if (indent >= 2) currentParent = ParseSubItem(trimmed, colonIdx, currentParent, package); else currentParent = ParseRootItem(trimmed, colonIdx, package);
        }
        return package;
    }

    internal static bool TryParseFoundLine(string trimmed, WingetPackage package) { if (!trimmed.StartsWith("Found ", StringComparison.OrdinalIgnoreCase)) return false; int bracketStart = trimmed.IndexOf('['); if (bracketStart != -1) package.Name = trimmed["Found ".Length..bracketStart].Trim(); return true; }
    private static MetadataItem? ParseSubItem(string trimmed, int colonIdx, MetadataItem? currentParent, WingetPackage package)
    {
        if (currentParent == null) return currentParent;
        if (colonIdx != -1) { string subKey = trimmed[..colonIdx].Trim(), subVal = trimmed[(colonIdx + 1)..].Trim(); currentParent.SubItems.Add(new MetadataItem { Key = subKey, Value = subVal, IsUrl = IsUrl(subVal) }); if (string.Equals(currentParent.Key, "Installer", StringComparison.OrdinalIgnoreCase)) { if (string.Equals(subKey, "Installer Type", StringComparison.OrdinalIgnoreCase)) package.InstallerType = subVal; else if (string.Equals(subKey, "Installer Url", StringComparison.OrdinalIgnoreCase)) package.InstallerUrl = subVal; } }
        else { currentParent.SubItems.Add(new MetadataItem { Value = trimmed }); if (string.Equals(currentParent.Key, "Tags", StringComparison.OrdinalIgnoreCase)) package.Tags.Add(trimmed); }
        return currentParent;
    }

    private static MetadataItem ParseRootItem(string trimmed, int colonIdx, WingetPackage package)
    {
        if (colonIdx != -1) { string key = trimmed[..colonIdx].Trim(), val = trimmed[(colonIdx + 1)..].Trim(); SetPackageField(package, key, val); var item = new MetadataItem { Key = key, Value = val, IsUrl = IsUrl(val) }; package.Details.Add(item); return item; }
        else { var item = new MetadataItem { Key = trimmed.Replace(":", "") }; package.Details.Add(item); return item; }
    }

    internal static void SetPackageField(WingetPackage package, string key, string val) { switch (key) { case "Name": package.Name = val; break; case "Version": package.Version = val; break; case "Publisher": package.Publisher = val; break; case "Publisher Url": package.PublisherUrl = val; break; case "Description": package.Description = val; break; case "Homepage": package.Homepage = val; break; case "License": package.License = val; break; case "Release Notes": package.ReleaseNotes = val; break; } }
    internal static bool IsUrl(string val) => val.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || val.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    public static double ParseProgressFromOutput(string line) { if (string.IsNullOrEmpty(line)) return 0; var match = PercentRegex.Match(line); if (match.Success && double.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out double val)) return val; if (line.Contains("Downloading", StringComparison.OrdinalIgnoreCase)) return 20; if (line.Contains("Verifying", StringComparison.OrdinalIgnoreCase)) return 60; if (line.Contains("Installing", StringComparison.OrdinalIgnoreCase)) return 80; return 0; }
    public static string ParseStatusTextFromOutput(string line) { if (string.IsNullOrEmpty(line)) return ""; string clean = line.Trim(); if (clean.Contains("Downloading", StringComparison.OrdinalIgnoreCase)) return "Downloading installer..."; if (clean.Contains("Successfully verified installer hash", StringComparison.OrdinalIgnoreCase)) return "Verifying installer..."; if (clean.Contains("Starting package install", StringComparison.OrdinalIgnoreCase)) return "Installing..."; if (clean.Contains("Successfully installed", StringComparison.OrdinalIgnoreCase)) return "Completed"; if (clean.Contains("Successfully uninstalled", StringComparison.OrdinalIgnoreCase)) return "Uninstalled"; if (HasPercentRegex.IsMatch(clean)) return ""; return clean.Length > 40 ? clean[..37] + "..." : clean; }
    public static List<string> ParseTagsFromShowOutput(string output) { var tags = new List<string>(); bool inTagSection = false; foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)) { string trimmed = line.Trim(); if (trimmed.StartsWith("Tags:", StringComparison.OrdinalIgnoreCase)) { inTagSection = true; continue; } if (inTagSection) { if (line.StartsWith("  ") || line.StartsWith('\t')) { if (!string.IsNullOrEmpty(trimmed)) tags.Add(trimmed); } else break; } } return tags; }
    public static string GetSubstring(string line, int start, int endExclusive) { if (string.IsNullOrEmpty(line) || start < 0 || start >= line.Length) return ""; if (endExclusive <= start) return ""; int length = Math.Min(endExclusive, line.Length) - start; return line.Substring(start, length).Trim(); }
}

