namespace FluNET.Classic.Core;

public sealed record OperatorEvaluationContext(IServiceProvider? Services);

public interface IOperatorEvaluator
{
    string OperatorStableId { get; }
    object? Evaluate(IReadOnlyList<object?> operands, OperatorEvaluationContext context);
}

public sealed class OperatorEvaluatorRegistry
{
    private readonly Dictionary<string, IOperatorEvaluator> _evaluators = new(StringComparer.OrdinalIgnoreCase);

    public void Register(IOperatorEvaluator evaluator)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        if (string.IsNullOrWhiteSpace(evaluator.OperatorStableId)) throw new ArgumentException("Operator evaluator stable ID cannot be empty.", nameof(evaluator));
        _evaluators[evaluator.OperatorStableId] = evaluator;
    }

    public void Register(string operatorStableId, Func<IReadOnlyList<object?>, OperatorEvaluationContext, object?> evaluator)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operatorStableId);
        ArgumentNullException.ThrowIfNull(evaluator);
        Register(new DelegateOperatorEvaluator(operatorStableId, evaluator));
    }

    public bool CanEvaluate(OperatorDescriptor descriptor) => descriptor.Evaluation != OperatorEvaluationKind.Custom || _evaluators.ContainsKey(descriptor.StableId);

    public bool TryEvaluate(OperatorDescriptor descriptor, IReadOnlyList<object?> operands, IServiceProvider? services, out object? result)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(operands);
        if (!_evaluators.TryGetValue(descriptor.StableId, out IOperatorEvaluator? evaluator))
        {
            result = null;
            return false;
        }
        result = evaluator.Evaluate(operands, new OperatorEvaluationContext(services));
        return true;
    }

    private sealed class DelegateOperatorEvaluator(
        string operatorStableId,
        Func<IReadOnlyList<object?>, OperatorEvaluationContext, object?> evaluator) : IOperatorEvaluator
    {
        public string OperatorStableId { get; } = operatorStableId;
        public object? Evaluate(IReadOnlyList<object?> operands, OperatorEvaluationContext context) => evaluator(operands, context);
    }
}
