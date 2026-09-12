namespace WingetStore.Models;

public class PackageStatusChangedMessage(WingetPackage package) : CommunityToolkit.Mvvm.Messaging.Messages.ValueChangedMessage<WingetPackage>(package);
public record PackageId(string Value) { public override string ToString() => Value; public static implicit operator string(PackageId id) => id?.Value ?? ""; public static implicit operator PackageId(string val) => new(val ?? ""); }
public record PackageVersion(string Value) { public override string ToString() => Value; public static implicit operator string(PackageVersion ver) => ver?.Value ?? ""; public static implicit operator PackageVersion(string val) => new(val ?? ""); }
