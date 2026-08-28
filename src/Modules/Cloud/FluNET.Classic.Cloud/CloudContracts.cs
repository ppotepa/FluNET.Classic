using FluNET.Classic.Core;

namespace FluNET.Classic.Cloud;

public sealed record CloudRegion(string Provider, string Name);
public sealed record CloudResourceIdentifier(string Provider, string Value);
public sealed record CredentialReference(string Provider, string Name) : ISensitiveValue { public string RedactedText => $"{Provider}:***"; public override string ToString() => RedactedText; }
public sealed record CloudResource(CloudResourceIdentifier Id, CloudRegion Region, IReadOnlyDictionary<string, string>? Tags = null) : IExistenceState { public bool Exists => true; }

public sealed class CloudModule : LanguageModule { public override string Name => "cloud"; }
