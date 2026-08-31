using System.Reflection;
using FluNET.Classic.SDK;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class SdkPublicSurfaceTests
{
    [Test]
    public void Sdk_public_signatures_do_not_expose_syntax_or_runtime_implementation_types()
    {
        Assembly sdk = typeof(FluNetModuleTestHarness).Assembly;
        var leaks = new List<string>();

        foreach (Type type in sdk.GetExportedTypes())
        {
            foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                IEnumerable<Type> exposed = member switch
                {
                    MethodInfo method => method.GetParameters().SelectMany(parameter => ContractTypes(parameter.ParameterType)).Concat(ContractTypes(method.ReturnType)),
                    ConstructorInfo constructor => constructor.GetParameters().SelectMany(parameter => ContractTypes(parameter.ParameterType)),
                    PropertyInfo property => ContractTypes(property.PropertyType),
                    FieldInfo field => ContractTypes(field.FieldType),
                    EventInfo @event => ContractTypes(@event.EventHandlerType),
                    _ => Array.Empty<Type>()
                };

                foreach (Type exposedType in exposed.Where(IsImplementationType))
                    leaks.Add($"{type.FullName}.{member.Name} -> {exposedType.FullName}");
            }
        }

        Assert.That(leaks, Is.Empty, string.Join(Environment.NewLine, leaks));
    }

    private static IEnumerable<Type> ContractTypes(Type? type)
    {
        if (type is null)
            yield break;
        if (type.IsByRef || type.IsPointer || type.IsArray)
        {
            foreach (Type nested in ContractTypes(type.GetElementType()))
                yield return nested;
            yield break;
        }
        if (type.IsGenericType)
        {
            foreach (Type nested in type.GetGenericArguments().SelectMany(ContractTypes))
                yield return nested;
            yield break;
        }
        yield return type;
    }

    private static bool IsImplementationType(Type type) =>
        type.Namespace?.Equals("FluNET.Classic.Syntax", StringComparison.Ordinal) == true
        || type.Namespace?.Equals("FluNET.Classic.Runtime", StringComparison.Ordinal) == true;
}
