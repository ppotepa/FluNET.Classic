using FluNET.Binding;
using FluNET.Language;

namespace FluNET.Runtime;

public sealed class VerbActivator
{
    private readonly IServiceProvider? _services;

    public VerbActivator(IServiceProvider? services = null)
    {
        _services = services;
    }

    public object Create(BoundSentence sentence, RuntimeState state)
    {
        ArgumentNullException.ThrowIfNull(sentence);
        ArgumentNullException.ThrowIfNull(state);

        ConstructorDescriptor constructor = sentence.Pattern.Constructor;
        object?[] arguments = new object?[constructor.Parameters.Count];

        foreach (ParameterDescriptor parameter in constructor.Parameters)
        {
            if (parameter.IsService)
            {
                arguments[parameter.Position] = _services?.GetService(parameter.ParameterType)
                    ?? throw new InvalidOperationException(
                        $"Service '{parameter.ParameterType}' required by {sentence.Implementation.ImplementationType.Name} is not registered.");
                continue;
            }

            BoundRole? role = sentence.Roles.FirstOrDefault(r =>
                r.Slot.Position == parameter.Position &&
                string.Equals(r.Slot.ParameterName, parameter.Name, StringComparison.OrdinalIgnoreCase));

            if (role is null)
            {
                arguments[parameter.Position] = parameter.IsOptional
                    ? parameter.DefaultValue
                    : CreateDefault(parameter.ParameterType);
                continue;
            }

            arguments[parameter.Position] = MaterializeRole(role, parameter, state);
        }

        return constructor.Activator(arguments);
    }

    private static object? MaterializeRole(
        BoundRole role,
        ParameterDescriptor parameter,
        RuntimeState state)
    {
        if (role.Slot.Direction == RoleDirection.Output)
        {
            return CreateDefault(parameter.ParameterType);
        }

        object?[] values = role.Values.Select(v => MaterializeValue(v, state)).ToArray();

        if (parameter.IsParamArray || role.Slot.Cardinality is RoleCardinality.OneOrMore or RoleCardinality.ZeroOrMore)
        {
            Type elementType = parameter.TypeShape.ElementType
                ?? throw new InvalidOperationException($"Variadic parameter '{parameter.Name}' has no element type.");
            Array array = Array.CreateInstance(elementType, values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                array.SetValue(values[i], i);
            }
            return array;
        }

        return values.FirstOrDefault();
    }

    private static object? MaterializeValue(BoundValue value, RuntimeState state) => value switch
    {
        BoundConstantValue constant => constant.Value,
        BoundVariableValue variable when !variable.IsOutput =>
            state.TryGetVariable(variable.Name, out object? resolved)
                ? resolved
                : throw new KeyNotFoundException($"Variable '{variable.Name}' is not defined."),
        BoundPipelineValue => state.PipelineValue,
        BoundPropertyValue property => ReadProperty(property, state),
        BoundInterpolatedValue interpolated => string.Concat(interpolated.Parts.Select(p => MaterializeValue(p, state)?.ToString() ?? string.Empty)),
        _ => null
    };

    private static object? ReadProperty(BoundPropertyValue property, RuntimeState state)
    {
        object? target = MaterializeValue(property.Target, state);
        if (target is null)
        {
            return null;
        }

        return target.GetType().GetProperty(property.Property)?.GetValue(target);
    }

    private static object? CreateDefault(Type type) =>
        type.IsValueType ? Activator.CreateInstance(type) : null;
}
