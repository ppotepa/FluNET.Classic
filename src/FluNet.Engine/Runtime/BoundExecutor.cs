using System.Collections;
using System.Reflection;
using FluNET.Binding;
using FluNET.Language;

namespace FluNET.Runtime;

public sealed record RuntimeDiagnostic(string Code, string Message);

public sealed record RuntimeResult(
    RuntimeState State,
    IReadOnlyList<RuntimeDiagnostic> Diagnostics)
{
    public bool Success => Diagnostics.Count == 0;
}

public sealed class BoundExecutor
{
    private readonly VerbActivator _activator;

    public BoundExecutor(VerbActivator activator)
    {
        _activator = activator ?? throw new ArgumentNullException(nameof(activator));
    }

    public async ValueTask<RuntimeResult> ExecuteAsync(
        BoundScript script,
        RuntimeState? state = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(script);

        state ??= new RuntimeState();
        var diagnostics = new List<RuntimeDiagnostic>();

        if (script.Diagnostics.Count > 0)
        {
            diagnostics.AddRange(script.Diagnostics.Select(d => new RuntimeDiagnostic(d.Code, d.Message)));
            return new RuntimeResult(state, diagnostics);
        }

        foreach (BoundPipeline pipeline in script.Pipelines)
        {
            foreach (BoundSentence sentence in pipeline.Sentences)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    object verb = _activator.Create(sentence, state);
                    object? result = await InvokeAsync(verb, cancellationToken).ConfigureAwait(false);
                    state.PipelineValue = result;
                    StoreOutputs(sentence, result, state);
                }
                catch (Exception ex)
                {
                    diagnostics.Add(new RuntimeDiagnostic(
                        "FLU-RUN-001",
                        $"{sentence.Verb.Name} failed: {Unwrap(ex).Message}"));
                    return new RuntimeResult(state, diagnostics);
                }
            }
        }

        return new RuntimeResult(state, diagnostics);
    }

    private static async ValueTask<object?> InvokeAsync(object verb, CancellationToken cancellationToken)
    {
        MethodInfo? asyncMethod = verb.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "InvokeAsync" &&
                                 m.GetParameters() is { Length: <= 1 } parameters &&
                                 (parameters.Length == 0 || parameters[0].ParameterType == typeof(CancellationToken)));

        if (asyncMethod is not null)
        {
            object? invocation = asyncMethod.GetParameters().Length == 0
                ? asyncMethod.Invoke(verb, null)
                : asyncMethod.Invoke(verb, [cancellationToken]);
            return await AwaitValueAsync(invocation).ConfigureAwait(false);
        }

        MethodInfo? syncMethod = verb.GetType().GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance);
        if (syncMethod is null)
        {
            throw new InvalidOperationException($"{verb.GetType().Name} exposes neither Invoke nor InvokeAsync.");
        }

        return syncMethod.Invoke(verb, null);
    }

    private static async ValueTask<object?> AwaitValueAsync(object? value)
    {
        if (value is null)
        {
            return null;
        }

        Type type = value.GetType();
        if (value is Task task)
        {
            await task.ConfigureAwait(false);
            return type.IsGenericType ? type.GetProperty("Result")?.GetValue(value) : null;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueTask<>))
        {
            MethodInfo asTask = type.GetMethod("AsTask")!;
            Task taskValue = (Task)asTask.Invoke(value, null)!;
            await taskValue.ConfigureAwait(false);
            return taskValue.GetType().GetProperty("Result")?.GetValue(taskValue);
        }

        if (value is ValueTask nonGenericValueTask)
        {
            await nonGenericValueTask.ConfigureAwait(false);
            return null;
        }

        return value;
    }

    private static void StoreOutputs(BoundSentence sentence, object? result, RuntimeState state)
    {
        BoundVariableValue[] outputs = sentence.Roles
            .Where(r => r.Slot.Direction is RoleDirection.Output or RoleDirection.InputOutput)
            .SelectMany(r => r.Values)
            .OfType<BoundVariableValue>()
            .Where(v => v.IsOutput)
            .ToArray();

        if (outputs.Length == 0)
        {
            return;
        }

        if (outputs.Length == 1)
        {
            state.SetVariable(outputs[0].Name, result);
            return;
        }

        for (int i = 0; i < outputs.Length; i++)
        {
            state.SetVariable(outputs[i].Name, Project(result, outputs[i].Name, i));
        }
    }

    private static object? Project(object? result, string name, int index)
    {
        if (result is null)
        {
            return null;
        }

        if (result is IDictionary dictionary && dictionary.Contains(name))
        {
            return dictionary[name];
        }

        PropertyInfo? property = result.GetType().GetProperty(
            name,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (property is not null)
        {
            return property.GetValue(result);
        }

        if (result is ITuple tuple && index < tuple.Length)
        {
            return tuple[index];
        }

        if (result is IList list && index < list.Count)
        {
            return list[index];
        }

        throw new InvalidOperationException(
            $"Cannot project output '{name}' at position {index} from result type {result.GetType().Name}.");
    }

    private static Exception Unwrap(Exception exception) =>
        exception is TargetInvocationException { InnerException: not null } target
            ? target.InnerException
            : exception;
}
