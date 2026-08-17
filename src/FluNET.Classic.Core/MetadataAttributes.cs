namespace FluNET.Classic.Core;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Constructor | AttributeTargets.Parameter, Inherited = true, AllowMultiple = false)]
public sealed class StableIdAttribute(string id) : Attribute
{
    public string Id { get; } = id;
}

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
    public RoleDirection? Direction { get; set; }
}

public sealed class WhatAttribute : RoleAttribute { public WhatAttribute() : base("WHAT") { } }
public sealed class FromAttribute : RoleAttribute { public FromAttribute() : base("FROM") { } }
public sealed class ToAttribute : RoleAttribute { public ToAttribute() : base("TO") { } }
public sealed class UsingAttribute : RoleAttribute { public UsingAttribute() : base("USING") { } }
public sealed class WithAttribute : RoleAttribute { public WithAttribute() : base("WITH") { } }
public sealed class AsAttribute : RoleAttribute { public AsAttribute() : base("AS") { } }
public sealed class InAttribute : RoleAttribute { public InAttribute() : base("IN") { } }
public sealed class AtAttribute : RoleAttribute { public AtAttribute() : base("AT") { } }
public sealed class ForAttribute : RoleAttribute { public ForAttribute() : base("FOR") { } }
public sealed class UntilAttribute : RoleAttribute { public UntilAttribute() : base("UNTIL") { } }
public sealed class ByAttribute : RoleAttribute { public ByAttribute() : base("BY") { } }
public sealed class ThenAttribute : RoleAttribute { public ThenAttribute() : base("THEN") { } }

[AttributeUsage(AttributeTargets.Parameter, Inherited = false, AllowMultiple = true)]
public sealed class RoleAliasAttribute(string alias) : Attribute
{
    public string Alias { get; } = alias;
}

[AttributeUsage(AttributeTargets.Parameter, Inherited = false, AllowMultiple = false)]
public sealed class RoleDirectionAttribute(RoleDirection direction) : Attribute
{
    public RoleDirection Direction { get; } = direction;
}

/// <summary>Projects an output role from a named public property or field of the verb result.</summary>
[AttributeUsage(AttributeTargets.Parameter, Inherited = false, AllowMultiple = false)]
public sealed class OutputMemberAttribute(string member) : Attribute
{
    public string Member { get; } = member;
}

/// <summary>Projects an output role from a zero-based tuple/list position of the verb result.</summary>
[AttributeUsage(AttributeTargets.Parameter, Inherited = false, AllowMultiple = false)]
public sealed class OutputIndexAttribute(int index) : Attribute
{
    public int Index { get; } = index;
}

[AttributeUsage(AttributeTargets.Parameter, Inherited = false, AllowMultiple = false)]
public sealed class FromServicesAttribute : Attribute { }

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
