namespace WingetStore.Models;

public class MetadataItem { public string Key { get; set; } = ""; public string Value { get; set; } = ""; public bool IsUrl { get; set; } public List<MetadataItem> SubItems { get; set; } = []; }
