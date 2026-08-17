using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using FluNET.Classic.Binding;
using FluNET.Classic.Core;

namespace FluNET.Classic.Runtime;

public sealed record RuntimeDiagnostic(string Code, string Message);
public sealed record RuntimeResult(RuntimeState State, IReadOnlyList<RuntimeDiagnostic> Diagnostics, IReadOnlyList<ExecutionTraceEntry>? Trace = null)
{
    public bool Success => Diagnostics.Count == 0;
    public object? Result => State.PipelineValue;
}

public sealed class BoundExecutor
{
    private readonly IServiceProvider? _services; private readonly ValueConversionRegistry _conversions; private readonly PredicateRegistry _predicates; private readonly ICapabilityPolicy _capabilities; private readonly ExecutionPolicy _policy; private readonly List<ExecutionTraceEntry> _trace = []; private int _sequence;
    public BoundExecutor(ValueConversionRegistry conversions, PredicateRegistry predicates, ICapabilityPolicy capabilities, IServiceProvider? services = null, ExecutionPolicy? policy = null) { _conversions = conversions; _predicates = predicates; _capabilities = capabilities; _services = services; _policy = policy ?? new ExecutionPolicy(); }

    public async ValueTask<RuntimeResult> ExecuteAsync(BoundScript script, RuntimeState? state = null, CancellationToken cancellationToken = default)
    {
        state ??= new RuntimeState(); _trace.Clear(); _sequence = 0; var diagnostics = script.Diagnostics.Select(x => new RuntimeDiagnostic(x.Code, x.Message)).ToList(); if (diagnostics.Count > 0) return new(state, diagnostics, _trace.ToArray());
        foreach (string capability in CollectCapabilities(script).Distinct(StringComparer.OrdinalIgnoreCase)) if (!_capabilities.IsAllowed(capability)) diagnostics.Add(new("FLU-RUN-010", $"Capability '{capability}' is required by the program.")); if (diagnostics.Count > 0) return new(state, diagnostics, _trace.ToArray());
        try { foreach (BoundStatement statement in script.Statements) await ExecuteStatement(statement, state, cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { diagnostics.Add(new("FLU-RUN-020", "Execution was cancelled or timed out.")); } catch (UnauthorizedAccessException ex) { diagnostics.Add(new("FLU-RUN-010", ex.Message)); } catch (InvalidCastException ex) { diagnostics.Add(new("FLU-RUN-030", ex.Message)); } catch (KeyNotFoundException ex) { diagnostics.Add(new("FLU-RUN-031", ex.Message)); } catch (Exception ex) { diagnostics.Add(new("FLU-RUN-001", ex.Message)); }
        return new(state, diagnostics, _trace.ToArray());
    }
    private async ValueTask ExecuteStatement(BoundStatement statement, RuntimeState state, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); switch (statement)
        {
            case BoundPipeline pipeline:
                foreach (BoundStage stage in pipeline.Stages)
                {
                    ct.ThrowIfCancellationRequested();
                    switch (stage)
                    {
                        case BoundSentence sentence: await ExecuteSentence(sentence, state, ct).ConfigureAwait(false); break;
                        case BoundFilter filter: await ExecuteFilterAsync(filter, state, ct).ConfigureAwait(false); break;
                        case BoundCheck check: ExecuteCheck(check, state); break;
                        case BoundCollection collection: await ExecuteCollectionAsync(collection, state, ct).ConfigureAwait(false); break;
                    }
                }
                break;
            case BoundIf conditional:
            {
                var promoted = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                using (state.PushScope())
                {
                    if (ToBoolean(EvaluateExpression(conditional.Condition, state, null))) await ExecuteBlock(conditional.Then, state, ct).ConfigureAwait(false);
                    else if (conditional.Else is not null) await ExecuteBlock(conditional.Else, state, ct).ConfigureAwait(false);
                    foreach (BoundFlowVariable variable in conditional.PromotedVariables)
                        if (state.TryGetVariable(variable.Name, out object? value)) promoted[variable.Name] = value;
                }
                foreach ((string name, object? value) in promoted) state.SetVariable(name, value);
                state.PipelineValue = null;
                break;
            }
            case BoundForEach loop:
            {
                object? source = Materialize(loop.Source, state);
                if (source is IEnumerable enumerable)
                {
                    foreach (object? item in enumerable) await ExecuteLoopItem(loop, state, item, ct).ConfigureAwait(false);
                }
                else if (source is not null && AsyncSequenceAdapter.CanEnumerate(source))
                {
                    await AsyncSequenceAdapter.ForEachAsync(source, item => ExecuteLoopItem(loop, state, item, ct), ct).ConfigureAwait(false);
                }
                else throw new InvalidOperationException("FOR EACH source is not enumerable.");
                state.PipelineValue = null;
                break;
            }
        }
    }
    private async ValueTask ExecuteLoopItem(BoundForEach loop, RuntimeState state, object? item, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using IDisposable scope = state.PushScope();
        state.SetVariable(loop.Variable, item);
        await ExecuteBlock(loop.Body, state, ct).ConfigureAwait(false);
    }
    private async ValueTask ExecuteBlock(BoundBlock block, RuntimeState state, CancellationToken ct) { foreach (BoundStatement statement in block.Statements) await ExecuteStatement(statement, state, ct).ConfigureAwait(false); }
    private async ValueTask ExecuteSentence(BoundSentence sentence, RuntimeState state, CancellationToken ct)
    {
        foreach (string capability in sentence.Implementation.Capabilities) if (!_capabilities.IsAllowed(capability)) throw new UnauthorizedAccessException($"Capability '{capability}' is required by {sentence.Verb.Name}."); object?[] args = new object?[sentence.Pattern.Constructor.Parameters.Count];
        foreach (ParameterDescriptor parameter in sentence.Pattern.Constructor.Parameters) { if (parameter.IsService) { args[parameter.Position] = _services?.GetService(parameter.ParameterType) ?? throw new InvalidOperationException($"Service '{parameter.ParameterType.Name}' is not registered."); continue; } BoundRole? role = sentence.Roles.FirstOrDefault(x => x.Slot.Position == parameter.Position); args[parameter.Position] = role is null ? parameter.IsOptional ? parameter.DefaultValue : Default(parameter.ParameterType) : MaterializeRole(role, parameter, state); }
        ValidateScopedCapabilities(sentence, args); int attempts = _policy.AttemptsFor(sentence.Implementation.Traits); int used = 0; Exception? last = null; DateTimeOffset started = DateTimeOffset.UtcNow; var timer = System.Diagnostics.Stopwatch.StartNew();
        for (int attempt = 1; attempt <= attempts; attempt++) { used = attempt; try { using CancellationTokenSource? timeout = CreateTimeout(ct, _policy.TimeoutFor(sentence.Implementation.Traits)); CancellationToken token = timeout?.Token ?? ct; object? result = await InvokeSentenceAttempt(sentence, args, state, token).ConfigureAwait(false); state.PipelineValue = result; StoreOutputs(sentence, result, state); if (sentence.ResultAlias is { Length: > 0 } alias) state.SetVariable(alias, result); timer.Stop(); _trace.Add(new(++_sequence, "sentence", sentence.Verb.Name, sentence.Implementation.ImplementationType.FullName, started, timer.Elapsed, true, used, sentence.ResultType.FullName, sentence.Implementation.Capabilities, sentence.Implementation.Traits)); return; } catch (Exception ex) when (attempt < attempts && ex is not OperationCanceledException && ex is not UnauthorizedAccessException) { last = ex; if (_policy.RetryDelay > TimeSpan.Zero) await Task.Delay(_policy.RetryDelay, ct).ConfigureAwait(false); } catch (Exception ex) { last = ex; break; } }
        timer.Stop(); _trace.Add(new(++_sequence, "sentence", sentence.Verb.Name, sentence.Implementation.ImplementationType.FullName, started, timer.Elapsed, false, used, sentence.ResultType.FullName, sentence.Implementation.Capabilities, sentence.Implementation.Traits, last?.Message)); throw last ?? new InvalidOperationException("Execution failed.");
    }
    private async ValueTask<object?> InvokeSentenceAttempt(BoundSentence sentence, object?[] args, RuntimeState state, CancellationToken token)
    {
        IExecutionTransaction? transaction = null; if (sentence.Implementation.Traits.Contains(ExecutionTrait.Transactional)) { ITransactionCoordinator? coordinator = _services?.GetService(typeof(ITransactionCoordinator)) as ITransactionCoordinator; if (coordinator is null && _policy.RequireTransactionCoordinatorForTransactional) throw new InvalidOperationException($"Transactional implementation '{sentence.Implementation.ImplementationType.FullName}' requires ITransactionCoordinator."); if (coordinator is not null) transaction = await coordinator.BeginAsync(sentence.Implementation.StableId, token).ConfigureAwait(false); }
        try { object verb = sentence.Pattern.Constructor.Activator(args); var context = new VerbExecutionContext(_services, state.Variables, state.PipelineValue, transaction); object? result = await sentence.Implementation.Invoker(verb, context, token).ConfigureAwait(false); if (transaction is not null) await transaction.CommitAsync(token).ConfigureAwait(false); return result; }
        catch { if (transaction is not null) { try { await transaction.RollbackAsync(token).ConfigureAwait(false); } catch { } } throw; }
        finally { if (transaction is not null) await transaction.DisposeAsync().ConfigureAwait(false); }
    }
    private void ValidateScopedCapabilities(BoundSentence sentence, object?[] args) { if (_capabilities is not IScopedCapabilityPolicy scoped) return; foreach (string capability in sentence.Implementation.Capabilities) { if (scoped.IsAllowed(capability, null)) continue; bool allowed = sentence.Pattern.Constructor.Parameters.Where(parameter => !parameter.IsService).Select(parameter => args[parameter.Position]).Any(resource => ResourceAllowed(scoped, capability, resource)); if (!allowed) throw new UnauthorizedAccessException($"Capability '{capability}' is not allowed for resources used by {sentence.Verb.Name}."); } }
    private static bool ResourceAllowed(IScopedCapabilityPolicy scoped, string capability, object? resource) { if (resource is null) return false; if (scoped.IsAllowed(capability, resource)) return true; if (resource is string or byte[] or ReadOnlyMemory<byte>) return false; if (resource is IEnumerable enumerable) foreach (object? item in enumerable) if (ResourceAllowed(scoped, capability, item)) return true; return false; }
    private static CancellationTokenSource? CreateTimeout(CancellationToken parent, TimeSpan? duration) { if (duration is null || duration <= TimeSpan.Zero) return null; var cts = CancellationTokenSource.CreateLinkedTokenSource(parent); cts.CancelAfter(duration.Value); return cts; }
    private object? MaterializeRole(BoundRole role, ParameterDescriptor parameter, RuntimeState state) { if (role.Slot.Direction == RoleDirection.Output) return Default(parameter.ParameterType); object?[] values = role.Values.Select(x => Materialize(x, state)).ToArray(); if (parameter.IsParamArray || role.Slot.Cardinality is RoleCardinality.ZeroOrMore or RoleCardinality.OneOrMore) { Type elementType = parameter.TypeShape.ElementType ?? throw new InvalidOperationException($"Variadic parameter '{parameter.Name}' has no element type."); Array array = Array.CreateInstance(elementType, values.Length); for (int i = 0; i < values.Length; i++) array.SetValue(values[i], i); return array; } return values.FirstOrDefault(); }
    private object? Materialize(BoundValue value, RuntimeState state) => value switch { BoundConstantValue constant => constant.Value, BoundVariableValue variable when !variable.IsOutput => state.TryGetVariable(variable.Name, out object? resolved) ? resolved : throw new KeyNotFoundException($"Variable '{variable.Name}' is not defined."), BoundPipelineValue => state.PipelineValue, BoundPropertyValue property => property.Accessor(Materialize(property.Target, state) ?? throw new NullReferenceException($"Cannot access '{property.Property}' on null.")), BoundInterpolatedValue interpolated => string.Concat(interpolated.Parts.Select(x => SensitiveValueFormatter.Format(Materialize(x, state)))), BoundExpressionValue expression => EvaluateExpression(expression.Expression, state, null), BoundConversionValue conversion => ConvertValue(Materialize(conversion.Source, state), conversion.TargetType), _ => null };
    private object? ConvertValue(object? value, Type target) { if (_conversions.TryConvert(value, target, out ConversionResult? converted)) return converted!.Value; throw new InvalidCastException($"Cannot convert {value?.GetType().Name ?? "null"} to {target.Name}."); }
    private async ValueTask ExecuteFilterAsync(BoundFilter filter, RuntimeState state, CancellationToken ct)
    {
        object? source = Materialize(filter.Source, state);
        if (source is not null && AsyncSequenceAdapter.CanEnumerate(source))
        {
            RuntimeState captured = SnapshotState(state);
            object result = AsyncSequenceAdapter.Where(source, item => ToBoolean(EvaluateExpression(filter.Predicate, captured, item)));
            state.PipelineValue = result;
            if (filter.ResultAlias is { Length: > 0 } alias) state.SetVariable(alias, result);
            return;
        }

        List<object?> items = await MaterializeSequenceAsync(source, "FILTER", ct).ConfigureAwait(false);
        var matches = new List<object?>();
        foreach (object? item in items) if (ToBoolean(EvaluateExpression(filter.Predicate, state, item))) matches.Add(item);
        Array array = ToTypedArray(filter.ElementType, matches);
        state.PipelineValue = array;
        if (filter.ResultAlias is { Length: > 0 } syncAlias) state.SetVariable(syncAlias, array);
    }
    private void ExecuteCheck(BoundCheck check, RuntimeState state) { bool result = ToBoolean(EvaluateExpression(check.Condition, state, null)); state.PipelineValue = result; if (check.ResultAlias is { Length: > 0 } alias) state.SetVariable(alias, result); }
    private async ValueTask ExecuteCollectionAsync(BoundCollection operation, RuntimeState state, CancellationToken ct)
    {
        object? source = Materialize(operation.Source, state);
        if (source is not null && AsyncSequenceAdapter.CanEnumerate(source))
        {
            RuntimeState captured = SnapshotState(state);
            object? asyncResult = operation.Operation switch
            {
                "COUNT" => await AsyncSequenceAdapter.CountAsync(source, ct).ConfigureAwait(false),
                "TAKE" => AsyncSequenceAdapter.Take(source, Convert.ToInt32(EvaluateExpression(operation.Argument!, captured, null), CultureInfo.InvariantCulture)),
                "SKIP" => AsyncSequenceAdapter.Skip(source, Convert.ToInt32(EvaluateExpression(operation.Argument!, captured, null), CultureInfo.InvariantCulture)),
                "DISTINCT" => AsyncSequenceAdapter.Distinct(
                    source,
                    item => operation.Argument is null ? item : EvaluateExpression(operation.Argument, captured, item),
                    EqualsNormalized),
                "SORT" or "GROUP" => await ExecuteMaterializingCollectionAsync(operation, source, captured, ct).ConfigureAwait(false),
                _ => throw new InvalidOperationException($"Unknown collection operation '{operation.Operation}'.")
            };
            state.PipelineValue = asyncResult;
            if (operation.ResultAlias is { Length: > 0 } asyncAlias) state.SetVariable(asyncAlias, asyncResult);
            return;
        }

        object? result = await ExecuteMaterializingCollectionAsync(operation, source, state, ct).ConfigureAwait(false);
        state.PipelineValue = result;
        if (operation.ResultAlias is { Length: > 0 } alias) state.SetVariable(alias, result);
    }
    private async ValueTask<object?> ExecuteMaterializingCollectionAsync(BoundCollection operation, object? source, RuntimeState state, CancellationToken ct)
    {
        List<object?> items = await MaterializeSequenceAsync(source, operation.Operation, ct).ConfigureAwait(false);
        return operation.Operation switch
        {
            "COUNT" => items.Count,
            "TAKE" => ToTypedArray(operation.ElementType, items.Take(Math.Max(0, Convert.ToInt32(EvaluateExpression(operation.Argument!, state, null), CultureInfo.InvariantCulture))).ToList()),
            "SKIP" => ToTypedArray(operation.ElementType, items.Skip(Math.Max(0, Convert.ToInt32(EvaluateExpression(operation.Argument!, state, null), CultureInfo.InvariantCulture))).ToList()),
            "SORT" => Sort(items, operation, state),
            "DISTINCT" => Distinct(items, operation, state),
            "GROUP" => Group(items, operation, state),
            _ => throw new InvalidOperationException($"Unknown collection operation '{operation.Operation}'.")
        };
    }
    private object Sort(List<object?> items, BoundCollection operation, RuntimeState state)
    {
        CollectionSortDirection direction = operation.Strategy is null
            ? CollectionSortDirection.ASCENDING
            : (CollectionSortDirection)(Materialize(operation.Strategy, state) ?? CollectionSortDirection.ASCENDING);
        int multiplier = direction == CollectionSortDirection.DESCENDING ? -1 : 1;
        items.Sort((a, b) => multiplier * Compare(EvaluateExpression(operation.Argument!, state, a), EvaluateExpression(operation.Argument!, state, b)));
        return ToTypedArray(operation.ElementType, items);
    }
    private object Distinct(List<object?> items, BoundCollection operation, RuntimeState state)
    {
        var distinct = new List<object?>(); var keys = new List<object?>();
        foreach (object? item in items)
        {
            object? key = operation.Argument is null ? item : EvaluateExpression(operation.Argument, state, item);
            if (keys.Any(existing => EqualsNormalized(existing, key))) continue;
            keys.Add(key); distinct.Add(item);
        }
        return ToTypedArray(operation.ElementType, distinct);
    }
    private object Group(List<object?> items, BoundCollection operation, RuntimeState state)
    {
        if (operation.Argument is null) throw new InvalidOperationException("GROUP requires a typed BY selector.");
        var groups = new List<(object? Key, List<object?> Items)>();
        foreach (object? item in items)
        {
            object? key = EvaluateExpression(operation.Argument, state, item);
            int index = groups.FindIndex(group => EqualsNormalized(group.Key, key));
            if (index < 0) groups.Add((key, new List<object?> { item })); else groups[index].Items.Add(item);
        }

        Type groupType = typeof(CollectionGroup<,>).MakeGenericType(operation.Argument.Type, operation.ElementType);
        Array result = Array.CreateInstance(groupType, groups.Count);
        for (int index = 0; index < groups.Count; index++)
        {
            Array typedItems = ToTypedArray(operation.ElementType, groups[index].Items);
            object group = Activator.CreateInstance(groupType, groups[index].Key, typedItems)
                ?? throw new InvalidOperationException($"Could not construct typed group '{groupType.Name}'.");
            result.SetValue(group, index);
        }
        return result;
    }
    private static RuntimeState SnapshotState(RuntimeState state)
    {
        var snapshot = new RuntimeState { PipelineValue = state.PipelineValue };
        foreach ((string name, object? value) in state.Variables) snapshot.SetVariable(name, value);
        return snapshot;
    }
    private static async ValueTask<List<object?>> MaterializeSequenceAsync(object? source, string operation, CancellationToken ct)
    {
        if (source is IEnumerable enumerable) return enumerable.Cast<object?>().ToList();
        if (source is not null && AsyncSequenceAdapter.CanEnumerate(source)) return await AsyncSequenceAdapter.ToListAsync(source, ct).ConfigureAwait(false);
        throw new InvalidOperationException($"{operation} source is not enumerable.");
    }
    private object? EvaluateExpression(BoundExpression expression, RuntimeState state, object? item) => expression switch { BoundValueExpression value => Materialize(value.Value, state), BoundItemPropertyExpression property => item is null ? null : property.Accessor(item), BoundUnaryExpression unary => unary.Operator == "NOT" ? !ToBoolean(EvaluateExpression(unary.Operand, state, item)) : EvaluateExpression(unary.Operand, state, item), BoundPredicateExpression predicate => EvaluatePredicate(predicate, state, item), BoundBinaryExpression binary => EvaluateBinaryExpression(binary, state, item), BoundBetweenExpression between => EvaluateBetweenExpression(between, state, item), _ => null };
    private object EvaluateBinaryExpression(BoundBinaryExpression binary, RuntimeState state, object? item) { if (binary.Operator == "AND") { object? left = EvaluateExpression(binary.Left, state, item); return ToBoolean(left) && ToBoolean(EvaluateExpression(binary.Right, state, item)); } if (binary.Operator == "OR") { object? left = EvaluateExpression(binary.Left, state, item); return ToBoolean(left) || ToBoolean(EvaluateExpression(binary.Right, state, item)); } return EvaluateBinary(binary.Operator, EvaluateExpression(binary.Left, state, item), EvaluateExpression(binary.Right, state, item)); }
    private object EvaluateBetweenExpression(BoundBetweenExpression between, RuntimeState state, object? item) { object? value = EvaluateExpression(between.Operand, state, item); object? lower = EvaluateExpression(between.Lower, state, item); object? upper = EvaluateExpression(between.Upper, state, item); return Compare(value, lower) >= 0 && Compare(value, upper) <= 0; }
    private bool EvaluatePredicate(BoundPredicateExpression predicate, RuntimeState state, object? item) { object? value = EvaluateExpression(predicate.Operand, state, item); foreach (string capability in predicate.Descriptor.CapabilitiesFor(predicate.Operand.Type)) if (!IsCapabilityAllowed(capability, value)) throw new UnauthorizedAccessException($"Capability '{capability}' is required by predicate '{predicate.Predicate}'."); return _predicates.Evaluate(predicate.Predicate, value, new PredicateContext(_services)); }
    private bool IsCapabilityAllowed(string capability, object? resource) { if (_capabilities is IScopedCapabilityPolicy scoped) return scoped.IsAllowed(capability, null) || scoped.IsAllowed(capability, resource); return _capabilities.IsAllowed(capability); }
    private static object EvaluateBinary(string op, object? left, object? right) { if (op is "=" or "==" or "IS") return EqualsNormalized(left, right); if (op is "!=" or "IS NOT") return !EqualsNormalized(left, right); if (op == "CONTAINS") { if (left is string text) return text.Contains(right?.ToString() ?? string.Empty, StringComparison.Ordinal); if (left is IDictionary dictionary) return right is not null && dictionary.Contains(right); if (left is IEnumerable enumerable) return enumerable.Cast<object?>().Any(x => EqualsNormalized(x, right)); return false; } if (op == "STARTS WITH") return (left?.ToString() ?? string.Empty).StartsWith(right?.ToString() ?? string.Empty, StringComparison.Ordinal); if (op == "ENDS WITH") return (left?.ToString() ?? string.Empty).EndsWith(right?.ToString() ?? string.Empty, StringComparison.Ordinal); if (op == "MATCHES") return Regex.IsMatch(left?.ToString() ?? string.Empty, right?.ToString() ?? string.Empty, RegexOptions.CultureInvariant); if (op == "IN") { if (right is string text) return text.Contains(left?.ToString() ?? string.Empty, StringComparison.Ordinal); if (right is IEnumerable enumerable) return enumerable.Cast<object?>().Any(x => EqualsNormalized(x, left)); return false; } int comparison = Compare(left, right); return op switch { ">" => comparison > 0, "<" => comparison < 0, ">=" => comparison >= 0, "<=" => comparison <= 0, "BEFORE" => comparison < 0, "AFTER" => comparison > 0, _ => false }; }
    private static bool EqualsNormalized(object? left, object? right) { if (left is null || right is null) return left is null && right is null; if (IsNumber(left) && IsNumber(right)) return Convert.ToDecimal(left, CultureInfo.InvariantCulture) == Convert.ToDecimal(right, CultureInfo.InvariantCulture); return Equals(left, right) || string.Equals(left.ToString(), right.ToString(), StringComparison.OrdinalIgnoreCase); }
    private static int Compare(object? left, object? right) { if (left is null && right is null) return 0; if (left is null) return -1; if (right is null) return 1; if (IsNumber(left) && IsNumber(right)) return Convert.ToDecimal(left, CultureInfo.InvariantCulture).CompareTo(Convert.ToDecimal(right, CultureInfo.InvariantCulture)); if (left is IComparable comparable && left.GetType().IsInstanceOfType(right)) return comparable.CompareTo(right); return string.Compare(left.ToString(), right.ToString(), StringComparison.OrdinalIgnoreCase); }
    private static bool ToBoolean(object? value) => Convert.ToBoolean(value, CultureInfo.InvariantCulture);
    private static bool IsNumber(object value) => Type.GetTypeCode(value.GetType()) is TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal;
    private static Array ToTypedArray(Type elementType, IReadOnlyList<object?> values) { Array array = Array.CreateInstance(elementType, values.Count); for (int i = 0; i < values.Count; i++) array.SetValue(values[i], i); return array; }
    private static void StoreOutputs(BoundSentence sentence, object? result, RuntimeState state) { BoundVariableValue[] outputs = sentence.Roles.Where(x => x.Slot.Direction is RoleDirection.Output or RoleDirection.InputOutput).SelectMany(x => x.Values).OfType<BoundVariableValue>().Where(x => x.IsOutput).ToArray(); if (outputs.Length == 0) return; if (outputs.Length == 1) { state.SetVariable(outputs[0].Name, result); return; } for (int i = 0; i < outputs.Length; i++) state.SetVariable(outputs[i].Name, Project(result, outputs[i].Name, i)); }
    private static object? Project(object? result, string name, int index) { if (result is null) return null; if (result is IDictionary dictionary && dictionary.Contains(name)) return dictionary[name]; var property = result.GetType().GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase); if (property is not null) return property.GetValue(result); if (result is System.Runtime.CompilerServices.ITuple tuple && index < tuple.Length) return tuple[index]; if (result is IList list && index < list.Count) return list[index]; throw new InvalidOperationException($"Cannot project output '{name}' from {result.GetType().Name}."); }
    private static object? Default(Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;
    private static IEnumerable<string> CollectCapabilities(BoundScript script) { foreach (BoundStatement statement in script.Statements) foreach (string capability in CollectCapabilities(statement)) yield return capability; }
    private static IEnumerable<string> CollectCapabilities(BoundStatement statement) { switch (statement) { case BoundPipeline pipeline: foreach (BoundStage stage in pipeline.Stages) { if (stage is BoundSentence sentence) foreach (string capability in sentence.Implementation.Capabilities) yield return capability; foreach (string capability in CollectExpressionCapabilities(stage)) yield return capability; } break; case BoundIf conditional: foreach (string capability in CollectExpressionCapabilities(conditional.Condition)) yield return capability; foreach (BoundStatement child in conditional.Then.Statements) foreach (string capability in CollectCapabilities(child)) yield return capability; if (conditional.Else is not null) foreach (BoundStatement child in conditional.Else.Statements) foreach (string capability in CollectCapabilities(child)) yield return capability; break; case BoundForEach loop: foreach (BoundStatement child in loop.Body.Statements) foreach (string capability in CollectCapabilities(child)) yield return capability; break; } }
    private static IEnumerable<string> CollectExpressionCapabilities(BoundStage stage) { if (stage is BoundFilter filter) return CollectExpressionCapabilities(filter.Predicate); if (stage is BoundCheck check) return CollectExpressionCapabilities(check.Condition); return Array.Empty<string>(); }
    private static IEnumerable<string> CollectExpressionCapabilities(BoundExpression expression) { if (expression is BoundPredicateExpression predicate) foreach (string capability in predicate.Descriptor.CapabilitiesFor(predicate.Operand.Type)) yield return capability; switch (expression) { case BoundUnaryExpression unary: foreach (string capability in CollectExpressionCapabilities(unary.Operand)) yield return capability; break; case BoundBinaryExpression binary: foreach (string capability in CollectExpressionCapabilities(binary.Left)) yield return capability; foreach (string capability in CollectExpressionCapabilities(binary.Right)) yield return capability; break; case BoundBetweenExpression between: foreach (string capability in CollectExpressionCapabilities(between.Operand)) yield return capability; foreach (string capability in CollectExpressionCapabilities(between.Lower)) yield return capability; foreach (string capability in CollectExpressionCapabilities(between.Upper)) yield return capability; break; case BoundPredicateExpression predicate: foreach (string capability in CollectExpressionCapabilities(predicate.Operand)) yield return capability; break; } }
}
