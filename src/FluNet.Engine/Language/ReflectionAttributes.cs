namespace FluNET.Language;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class VerbAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class AliasAttribute(string alias) : Attribute
{
    public string Alias { get; } = alias;
}

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public abstract class RoleAttribute(string name, RoleDirection direction = RoleDirection.Input) : Attribute
{
    public string Name { get; } = name;
    public RoleDirection Direction { get; } = direction;
}

public sealed class WhatAttribute(RoleDirection direction = RoleDirection.Input) : RoleAttribute("WHAT", direction);
public sealed class FromAttribute() : RoleAttribute("FROM");
public sealed class ToAttribute() : RoleAttribute("TO");
public sealed class UsingAttribute() : RoleAttribute("USING");
public sealed class WithAttribute() : RoleAttribute("WITH");
public sealed class ThenAttribute() : RoleAttribute("THEN");

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class RoleDirectionAttribute(RoleDirection direction) : Attribute
{
    public RoleDirection Direction { get; } = direction;
}

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class FromServicesAttribute : Attribute;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = true)]
public sealed class RequiresCapabilityAttribute(string capability) : Attribute
{
    public string Capability { get; } = capability;
}
