using FluNET.Language;

namespace FluNET.Binding;

public sealed record ResolutionContext(
    Type ExpectedType,
    string? RoleName = null,
    string? VerbName = null,
    string? Qualifier = null,
    IServiceProvider? Services = null,
    IFormatProvider? FormatProvider = null);

public interface IValueResolver
{
    Type TargetType { get; }

    bool TryResolve(string source, ResolutionContext context, out object? value);
}

public interface IValueResolver<T> : IValueResolver
{
    bool TryResolve(string source, ResolutionContext context, out T? value);
}
