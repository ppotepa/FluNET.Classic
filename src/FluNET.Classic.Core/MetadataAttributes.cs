namespace FluNET.Classic.Core;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = true, AllowMultiple = false)]
public sealed class VerbAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = true, AllowMultiple = true)]
public sealed class AliasAttribute(string alias) : Attribute
{
    public string Alias { get; } = alias;
}

[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
public sealed class QualifierAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

[AttributeUsage(AttributeTargets.Parameter, Inherited = false, AllowMultiple = false)]
public abstract class RoleAttribute(string name) : Attribute
{
    public string Name { get; } = name;
    public RoleDirection? Direction { get; init; }
}

public sealed class WhatAttribute() : RoleAttribute("WHAT");
public sealed class FromAttribute() : RoleAttribute("FROM");
public sealed class ToAttribute() : RoleAttribute("TO");
public sealed class UsingAttribute() : RoleAttribute("USING");
public sealed class WithAttribute() : RoleAttribute("WITH");
public sealed class ThenAttribute() : RoleAttribute("THEN");

[AttributeUsage(AttributeTargets.Parameter, Inherited = false, AllowMultiple = false)]
public sealed class RoleDirectionAttribute(RoleDirection direction) : Attribute
{
    public RoleDirection Direction { get; } = direction;
}

[AttributeUsage(AttributeTargets.Parameter, Inherited = false, AllowMultiple = false)]
public sealed class FromServicesAttribute : Attribute;

[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
public sealed class RequiresCapabilityAttribute(string capability) : Attribute
{
    public string Capability { get; } = capability;
}

[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
public sealed class ExecutionTraitAttribute(ExecutionTrait trait) : Attribute
{
    public ExecutionTrait Trait { get; } = trait;
}
