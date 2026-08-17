using System.Collections;
using System.Globalization;
using FluNET.Classic.Binding;
using FluNET.Classic.Core;

namespace FluNET.Classic.Runtime;

public sealed record RuntimeDiagnostic(string Code, string Message);
public sealed record RuntimeResult(RuntimeState State, IReadOnlyList<RuntimeDiagnostic> Diagnostics)
{
    public bool Success => Diagnostics.Count == 0;
    public object? Result => State.PipelineValue;
}

public sealed class BoundExecutor
{
    private readonly IServiceProvider? _services;
    private readonly ValueConversionRegistry _conversions;
    private readonly ICapabilityPolicy _capabilities;

    public BoundExecutor(ValueConversionRegistry conversions, ICapabilityPolicy capabilities, IServiceProvider? services = null)
    {
        _conversions = conversions;
        _capabilities = capabilities;
        _services = services;
    }

    public async ValueTask<RuntimeResult> ExecuteAsync(BoundScript script, RuntimeState? state = null, CancellationToken cancellationToken = default)
    {
        state ??= new RuntimeState();
        var diagnostics = script.Diagnostics.Select(x => new RuntimeDiagnostic(x.Code, x.Message)).ToList();
        if (diagnostics.Count > 0) return new(state, diagnostics);

        try
        {
            foreach (BoundStatement statement in script.Statements)
                await ExecuteStatement(statement, state, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            diagnostics.Add(new("FLU-RUN-001", ex.Message));
        }
        return new(state, diagnostics);
    }

    private async ValueTask ExecuteStatement(BoundStatement statement, RuntimeState state, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        switch (statement)
        {
            case BoundPipeline pipeline:
                foreach (BoundStage stage in pipeline.Stages)
                {
                    ct.ThrowIfCancellationRequested();
                    if (stage is BoundSentence sentence) await ExecuteSentence(sentence, state, ct).ConfigureAwait(false);
                    else if (stage is BoundFilter filter) ExecuteFilter(filter, state);
                }
                break;
            case BoundIf conditional:
                bool condition = Convert.ToBoolean(EvaluateExpression(conditional.Condition, state, null), CultureInfo.InvariantCulture);
                if (condition) await ExecuteBlock(conditional.Then, state, ct).ConfigureAwait(false);
                else if (conditional.Else is not null) await ExecuteBlock(conditional.Else, state, ct).ConfigureAwait(false);
                break;
            case BoundForEach loop:
                object? source = Materialize(loop.Source, state);
                if (source is not IEnumerable enumerable) throw new InvalidOperationException("FOR EACH source is not enumerable.");
                foreach (object? item in enumerable)
                {
                    using IDisposable scope = state.PushScope();
                    state.SetVariable(loop.Variable, item);
                    await ExecuteBlock(loop.Body, state, ct).ConfigureAwait(false);
                }
                break;
        }
    }

    private async ValueTask ExecuteBlock(BoundBlock block, RuntimeState state, CancellationToken ct)
    {
        foreach (BoundStatement statement in block.Statements) await ExecuteStatement(statement, state, ct).ConfigureAwait(false);
    }

    private async ValueTask ExecuteSentence(BoundSentence sentence, RuntimeState state, CancellationToken ct)
    {
        foreach (string capability in sentence.Implementation.Capabilities)
            if (!_capabilities.IsAllowed(capability)) throw new UnauthorizedAccessException($"Capability '{capability}' is required by {sentence.Verb.Name}.");

        object?[] args = new object?[sentence.Pattern.Constructor.Parameters.Count];
        foreach (ParameterDescriptor parameter in sentence.Pattern.Constructor.Parameters)
        {
            if (parameter.IsService)
            {
                args[parameter.Position] = _services?.GetService(parameter.ParameterType) ?? throw new InvalidOperationException($"Service '{parameter.ParameterType.Name}' is not registered.");
                continue;
            }
            BoundRole? role = sentence.Roles.FirstOrDefault(x => x.Slot.Position == parameter.Position);
            if (role is null)
            {
                args[parameter.Position] = parameter.IsOptional ? parameter.DefaultValue : Default(parameter.ParameterType);
                continue;
            }
            args[parameter.Position] = MaterializeRole(role, parameter, state);
        }

        object verb = sentence.Pattern.Constructor.Activator(args);
        var context = new VerbExecutionContext(_services, state.Variables, state.PipelineValue);
        object? result = await sentence.Implementation.Invoker(verb, context, ct).ConfigureAwait(false);
        state.PipelineValue = result;
        StoreOutputs(sentence, result, state);
        if (sentence.ResultAlias is { Length: > 0 } alias) state.SetVariable(alias, result);
    }

    private object? MaterializeRole(BoundRole role, ParameterDescriptor parameter, RuntimeState state)
    {
        if (role.Slot.Direction == RoleDirection.Output) return Default(parameter.ParameterType);
        object?[] values = role.Values.Select(x => Materialize(x, state)).ToArray();
        if (parameter.IsParamArray || role.Slot.Cardinality is RoleCardinality.ZeroOrMore or RoleCardinality.OneOrMore)
        {
            Type elementType = parameter.TypeShape.ElementType ?? throw new InvalidOperationException($"Variadic parameter '{parameter.Name}' has no element type.");
            Array array = Array.CreateInstance(elementType, values.Length);
            for (int i = 0; i < values.Length; i++) array.SetValue(values[i], i);
            return array;
        }
        return values.FirstOrDefault();
    }

    private object? Materialize(BoundValue value, RuntimeState state) => value switch
    {
        BoundConstantValue constant => constant.Value,
        BoundVariableValue variable when !variable.IsOutput => state.TryGetVariable(variable.Name, out object? resolved) ? resolved : throw new KeyNotFoundException($"Variable '{variable.Name}' is not defined."),
        BoundPipelineValue => state.PipelineValue,
        BoundPropertyValue property => property.Accessor(Materialize(property.Target, state) ?? throw new NullReferenceException($"Cannot access '{property.Property}' on null.")),
        BoundInterpolatedValue interpolated => string.Concat(interpolated.Parts.Select(x => Materialize(x, state)?.ToString() ?? string.Empty)),
        BoundConversionValue conversion => ConvertValue(Materialize(conversion.Source, state), conversion.TargetType),
        _ => null
    };

    private object? ConvertValue(object? value, Type target)
    {
        if (_conversions.TryConvert(value, target, out ConversionResult? converted)) return converted!.Value;
        throw new InvalidCastException($"Cannot convert {value?.GetType().Name ?? "null"} to {target.Name}.");
    }

    private void ExecuteFilter(BoundFilter filter, RuntimeState state)
    {
        object? value = Materialize(filter.Source, state);
        if (value is not IEnumerable enumerable) throw new InvalidOperationException("FILTER source is not enumerable.");
        var matches = new List<object?>();
        foreach (object? item in enumerable)
            if (Convert.ToBoolean(EvaluateExpression(filter.Predicate, state, item), CultureInfo.InvariantCulture)) matches.Add(item);
        Array result = Array.CreateInstance(filter.ElementType, matches.Count);
        for (int i = 0; i < matches.Count; i++) result.SetValue(matches[i], i);
        state.PipelineValue = result;
        if (filter.ResultAlias is { Length: > 0 } alias) state.SetVariable(alias, result);
    }

    private object? EvaluateExpression(BoundExpression expression, RuntimeState state, object? item) => expression switch
    {
        BoundValueExpression value => Materialize(value.Value, state),
        BoundItemPropertyExpression property => item is null ? null : property.Accessor(item),
        BoundUnaryExpression unary => unary.Operator == "NOT" ? !Convert.ToBoolean(EvaluateExpression(unary.Operand, state, item), CultureInfo.InvariantCulture) : EvaluateExpression(unary.Operand, state, item),
        BoundBinaryExpression binary => EvaluateBinary(binary.Operator, EvaluateExpression(binary.Left, state, item), EvaluateExpression(binary.Right, state, item)),
        _ => null
    };

    private static object EvaluateBinary(string op, object? left, object? right)
    {
        if (op == "AND") return Convert.ToBoolean(left, CultureInfo.InvariantCulture) && Convert.ToBoolean(right, CultureInfo.InvariantCulture);
        if (op == "OR") return Convert.ToBoolean(left, CultureInfo.InvariantCulture) || Convert.ToBoolean(right, CultureInfo.InvariantCulture);
        if (op is "=" or "==" or "IS") return EqualsNormalized(left, right);
        if (op is "!=" or "IS NOT") return !EqualsNormalized(left, right);
        int comparison = Compare(left, right);
        return op switch { ">" => comparison > 0, "<" => comparison < 0, ">=" => comparison >= 0, "<=" => comparison <= 0, _ => false };
    }

    private static bool EqualsNormalized(object? left, object? right)
    {
        if (left is null || right is null) return left is null && right is null;
        if (IsNumber(left) && IsNumber(right)) return Convert.ToDecimal(left, CultureInfo.InvariantCulture) == Convert.ToDecimal(right, CultureInfo.InvariantCulture);
        return Equals(left, right) || string.Equals(left.ToString(), right.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static int Compare(object? left, object? right)
    {
        if (left is null && right is null) return 0;
        if (left is null) return -1;
        if (right is null) return 1;
        if (IsNumber(left) && IsNumber(right)) return Convert.ToDecimal(left, CultureInfo.InvariantCulture).CompareTo(Convert.ToDecimal(right, CultureInfo.InvariantCulture));
        if (left is IComparable comparable && left.GetType().IsInstanceOfType(right)) return comparable.CompareTo(right);
        return string.Compare(left.ToString(), right.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNumber(object value) => Type.GetTypeCode(value.GetType()) is TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal;

    private static void StoreOutputs(BoundSentence sentence, object? result, RuntimeState state)
    {
        BoundVariableValue[] outputs = sentence.Roles.Where(x => x.Slot.Direction is RoleDirection.Output or RoleDirection.InputOutput).SelectMany(x => x.Values).OfType<BoundVariableValue>().Where(x => x.IsOutput).ToArray();
        if (outputs.Length == 0) return;
        if (outputs.Length == 1) { state.SetVariable(outputs[0].Name, result); return; }
        for (int i = 0; i < outputs.Length; i++) state.SetVariable(outputs[i].Name, Project(result, outputs[i].Name, i));
    }

    private static object? Project(object? result, string name, int index)
    {
        if (result is null) return null;
        if (result is IDictionary dictionary && dictionary.Contains(name)) return dictionary[name];
        var property = result.GetType().GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
        if (property is not null) return property.GetValue(result);
        if (result is System.Runtime.CompilerServices.ITuple tuple && index < tuple.Length) return tuple[index];
        if (result is IList list && index < list.Count) return list[index];
        throw new InvalidOperationException($"Cannot project output '{name}' from {result.GetType().Name}.");
    }

    private static object? Default(Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;
}
