namespace FluNET.Runtime;

public interface ICapabilityPolicy
{
    bool IsAllowed(string capability);
}

public sealed class AllowAllCapabilityPolicy : ICapabilityPolicy
{
    public bool IsAllowed(string capability) => true;
}

public sealed class CapabilitySetPolicy : ICapabilityPolicy
{
    private readonly HashSet<string> _allowed;

    public CapabilitySetPolicy(IEnumerable<string> allowed)
    {
        ArgumentNullException.ThrowIfNull(allowed);
        _allowed = new HashSet<string>(allowed, StringComparer.OrdinalIgnoreCase);
    }

    public bool IsAllowed(string capability) => _allowed.Contains(capability);
}
