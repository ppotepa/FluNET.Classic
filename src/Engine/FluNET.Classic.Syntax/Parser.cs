using FluNET.Classic.Core;
using System.Globalization;

namespace FluNET.Classic.Syntax;

public sealed record SyntaxDiagnostic(string Code, string Message, TextSpan Span);
public sealed record ParseResult(ScriptNode Script, IReadOnlyList<SyntaxDiagnostic> Diagnostics)
{
    public bool Success => Diagnostics.Count == 0;
}

public sealed class ClassicParser
{
    private readonly LanguageSnapshot _language;
    private readonly ClassicLexer _lexer;
    private readonly List<SyntaxDiagnostic> _diagnostics = [];
    private IReadOnlyList<SyntaxToken> _tokens = Array.Empty<SyntaxToken>();
    private int _position;

    public ClassicParser(LanguageSnapshot language, ClassicLexer? lexer = null)
    {
        _language = language;
        _lexer = lexer ?? new ClassicLexer();
    }

    public ParseResult Parse(string? source)
    {
        source ??= string.Empty;
        _diagnostics.Clear();
        LexResult lex = _lexer.LexDetailed(source);
        _tokens = lex.Tokens;
        _position = 0;
        _diagnostics.AddRange(lex.Diagnostics.Select(x => new SyntaxDiagnostic(x.Code, x.Message, x.Span)));
        List<StatementNode> statements = ParseStatements();
        TextSpan span = statements.Count == 0 ? new(0, source.Length) : TextSpan.FromBounds(statements[0].Span.Start, statements[^1].Span.End);
        return new(new ScriptNode(statements, span), _diagnostics.ToArray());
    }

    private List<StatementNode> ParseStatements()
    {
        var result = new List<StatementNode>();
        SkipNewLines();
        while (Current.Kind != TokenKind.End)
        {
            if (IsWord("ELSE") || IsWord("END") || IsWord("FINALLY") || IsWord("ON") && IsWord("FAILURE", 1))
                break;
            StatementNode? statement = ParseStatement();
            if (statement is not null)
            {
                result.Add(statement);
                if (statement is not CommentStatementNode)
                    RequirePeriod();
            }
            else
            {
                SkipToBoundary();
                if (Current.Kind == TokenKind.RightBrace)
                    Advance();
            }
            SkipNewLines();
        }
        return result;
    }

    private StatementNode? ParseStatement()
    {
        if (Current.Kind == TokenKind.Comment)
            return ParseComment();
        if (IsWord("IF"))
            return ParseIf();
        if (IsWord("FOR") && IsWord("EACH", 1))
            return ParseForEach();
        if (IsWord("TRY"))
            return ParseTry();
        if (IsWord("DEFINE"))
            return IsWord("RECORD", 1) ? ParseRecordDefinition() : ParseDefinition();
        if (IsWord("RETURN"))
            return ParseReturn();
        return ParsePipeline();
    }
    private CommentStatementNode ParseComment()
    {
        SyntaxToken token = Advance();
        return new(token.Value?.ToString() ?? string.Empty, token.Span);
    }
    private IfNode ParseIf()
    {
        int start = Advance().Span.Start;
        ExpressionNode condition = ParseExpressionUntil("THEN");
        ConsumeThen("FLU-SYN-101", "IF requires ', THEN'.");
        BlockNode then = ParseNamedBlock("IF", allowElse: true);
        BlockNode? otherwise = null;
        if (IsWord("ELSE"))
        {
            Advance();
            SkipNewLines();
            otherwise = ParseNamedBlock("IF", allowElse: false);
        }
        ExpectEnd("IF", "FLU-SYN-102", "IF requires END IF.");
        return new(condition, then, otherwise, TextSpan.FromBounds(start, Previous.Span.End));
    }
    private ForEachNode ParseForEach()
    {
        int start = Advance().Span.Start;
        ExpectWord("EACH", "FLU-SYN-110", "FOR must be followed by EACH.");
        SyntaxToken variable = Current;
        if (variable.Kind != TokenKind.Variable)
            _diagnostics.Add(new("FLU-SYN-111", "FOR EACH requires an iterator variable.", variable.Span));
        else
            Advance();
        ExpectWord("IN", "FLU-SYN-112", "FOR EACH requires IN.");
        ExpressionNode source = ParseExpressionUntil("DO");
        int? parallelism = null;
        if (Current.Kind == TokenKind.Comma)
        {
            int saved = _position;
            Advance();
            SkipNewLines();
            if (IsWord("PARALLEL"))
            {
                Advance();
                if (Current.Kind == TokenKind.Number)
                {
                    parallelism = Convert.ToInt32(Current.Value, CultureInfo.InvariantCulture);
                    Advance();
                }
                else
                    _diagnostics.Add(new("FLU-SYN-118", "PARALLEL requires a positive worker count.", Current.Span));
                if (Current.Kind != TokenKind.Comma)
                    _diagnostics.Add(new("FLU-SYN-119", "PARALLEL requires ', DO'.", Current.Span));
                else
                    Advance();
                SkipNewLines();
                ExpectWord("DO", "FLU-SYN-113", "FOR EACH requires ', DO'.");
                SkipNewLines();
            }
            else
                _position = saved;
        }
        if (parallelism is <= 0)
            _diagnostics.Add(new("FLU-SYN-118", "PARALLEL requires a positive worker count.", Previous.Span));
        if (parallelism is null)
            ConsumeDo("FLU-SYN-113", "FOR EACH requires ', DO'.");
        BlockNode body = ParseNamedBlock("FOR", allowElse: false);
        ExpectEnd("FOR", "FLU-SYN-114", "FOR EACH requires END FOR.");
        return new(variable.Value?.ToString() ?? string.Empty, source, parallelism, body, TextSpan.FromBounds(start, Previous.Span.End));
    }
    private TryNode ParseTry()
    {
        int start = Advance().Span.Start;
        ConsumeDo("FLU-SYN-116", "TRY requires ', DO'.");
        BlockNode body = ParseNamedBlock("TRY", allowElse: false);
        BlockNode? failure = null;
        if (IsWord("ON") && IsWord("FAILURE", 1))
        {
            Advance();
            Advance();
            SkipNewLines();
            failure = ParseNamedBlock("TRY", allowElse: false);
        }
        BlockNode? finallyBlock = null;
        if (IsWord("FINALLY"))
        {
            Advance();
            SkipNewLines();
            finallyBlock = ParseNamedBlock("TRY", allowElse: false);
        }
        ExpectEnd("TRY", "FLU-SYN-117", "TRY requires END TRY.");
        return new(body, failure, finallyBlock, TextSpan.FromBounds(start, Previous.Span.End));
    }
    private DefinitionNode ParseDefinition()
    {
        int start = Advance().Span.Start;
        bool isFunction = IsWord("FUNCTION");
        if (!isFunction && !IsWord("TASK"))
            _diagnostics.Add(new("FLU-SYN-140", "DEFINE requires FUNCTION or TASK.", Current.Span));
        else
            Advance();
        SyntaxToken name = Current;
        if (name.Kind != TokenKind.Word)
            _diagnostics.Add(new("FLU-SYN-141", "A definition requires a verb name.", name.Span));
        else
            Advance();
        string? qualifier = null;
        if (Current.Kind == TokenKind.Word && !IsWord("WHAT") && !IsWord("RETURNING") && !IsWord("DO"))
        {
            qualifier = Current.Text;
            Advance();
        }
        var parameters = new List<DefinitionParameterNode>();
        while (!IsWord("RETURNING") && !IsWord("DO") && Current.Kind != TokenKind.End)
        {
            if (Current.Kind == TokenKind.Comma)
            {
                Advance();
                SkipNewLines();
                continue;
            }
            SyntaxToken role = Current;
            if (role.Kind != TokenKind.Word)
            {
                _diagnostics.Add(new("FLU-SYN-142", "Expected a parameter role.", role.Span));
                Advance();
                continue;
            }
            if (!LanguageRoleNames.IsContextual(role.Text))
                _diagnostics.Add(new("FLU-SYN-155", $"Definition role '{role.Text}' is not part of the canonical contextual role vocabulary.", role.Span));
            Advance();
            SyntaxToken variable = Current;
            if (variable.Kind != TokenKind.Variable)
            {
                _diagnostics.Add(new("FLU-SYN-143", $"Parameter role {role.Text} requires a [variable].", variable.Span));
                break;
            }
            Advance();
            ExpectWord("AS", "FLU-SYN-144", "Definition parameters require AS TYPE.");
            SyntaxToken type = Current;
            if (type.Kind != TokenKind.Word)
                _diagnostics.Add(new("FLU-SYN-145", "Definition parameters require a type name.", type.Span));
            else
                Advance();
            parameters.Add(new(role.Text.ToUpperInvariant(), variable.Value?.ToString() ?? string.Empty, type.Text, TextSpan.FromBounds(role.Span.Start, Previous.Span.End)));
        }
        ExpectWord("RETURNING", "FLU-SYN-146", "Definition requires RETURNING TYPE.");
        SyntaxToken returnType = Current;
        if (returnType.Kind != TokenKind.Word)
            _diagnostics.Add(new("FLU-SYN-147", "Definition requires a return type.", returnType.Span));
        else
            Advance();
        ConsumeDo("FLU-SYN-148", "Definition requires ', DO'.");
        BlockNode body = ParseNamedBlock(isFunction ? "FUNCTION" : "TASK", allowElse: false);
        ExpectEnd(isFunction ? "FUNCTION" : "TASK", "FLU-SYN-149", "Definition has an invalid named ending.");
        return new(isFunction ? DefinitionKind.Function : DefinitionKind.Task, name.Value?.ToString() ?? string.Empty, qualifier, parameters, returnType.Text, body, TextSpan.FromBounds(start, Previous.Span.End));
    }
    private RecordDefinitionNode ParseRecordDefinition()
    {
        int start = Advance().Span.Start;
        ExpectWord("RECORD", "FLU-SYN-150", "DEFINE requires RECORD.");
        SyntaxToken name = Current;
        if (name.Kind != TokenKind.Word)
            _diagnostics.Add(new("FLU-SYN-151", "A record requires a name.", name.Span));
        else
            Advance();
        var fields = new List<RecordFieldNode>();
        while (Current.Kind is not TokenKind.End and not TokenKind.Period)
        {
            if (Current.Kind == TokenKind.Comma)
            {
                Advance();
                SkipNewLines();
                continue;
            }
            SyntaxToken field = Current;
            if (field.Kind != TokenKind.Word)
            {
                _diagnostics.Add(new("FLU-SYN-152", "Expected a record field name.", field.Span));
                Advance();
                continue;
            }
            Advance();
            ExpectWord("AS", "FLU-SYN-153", "Record fields require AS TYPE.");
            SyntaxToken type = Current;
            if (type.Kind != TokenKind.Word)
                _diagnostics.Add(new("FLU-SYN-154", "Record fields require a type name.", type.Span));
            else
                Advance();
            fields.Add(new(field.Text, type.Text, TextSpan.FromBounds(field.Span.Start, Previous.Span.End)));
        }
        return new(name.Value?.ToString() ?? string.Empty, fields, TextSpan.FromBounds(start, Previous.Span.End));
    }
    private ReturnNode ParseReturn()
    {
        int start = Advance().Span.Start;
        ExpressionNode? value = IsStageBoundary(Current) ? null : ParseExpressionUntil();
        return new(value, TextSpan.FromBounds(start, value?.Span.End ?? Previous.Span.End));
    }
    private BlockNode ParseNamedBlock(string owner, bool allowElse)
    {
        int start = Current.Span.Start;
        List<StatementNode> statements = ParseStatements();
        if (!allowElse && IsWord("ELSE"))
            _diagnostics.Add(new("FLU-SYN-115", $"{owner} block does not allow ELSE.", Current.Span));
        int end = statements.LastOrDefault()?.Span.End ?? start;
        return new(statements, TextSpan.FromBounds(start, end));
    }
    private PipelineNode? ParsePipeline(bool stopAtElse = false)
    {
        int start = Current.Span.Start;
        var stages = new List<PipelineStageNode>();
        PipelineStageNode? first = ParseStage();
        if (first is null)
            return null;
        stages.Add(first);
        while (TryConsumePipelineContinuation())
        {
            if (stopAtElse && IsWord("ELSE"))
                break;
            PipelineStageNode? stage = ParseStage();
            if (stage is null)
                break;
            stages.Add(stage);
        }
        return new(stages, TextSpan.FromBounds(start, stages[^1].Span.End));
    }
    private bool TryConsumePipelineContinuation()
    {
        if (Current.Kind != TokenKind.Comma)
            return false;
        int saved = _position;
        Advance();
        SkipNewLines();
        if (IsWord("THEN"))
        {
            Advance();
            SkipNewLines();
            return true;
        }
        _position = saved;
        return false;
    }
    private PipelineStageNode? ParseStage()
    {
        if (IsWord("FILTER"))
            return ParseFilter();
        if (IsWord("CHECK") && IsWord("IF", 1))
            return ParseCheck();
        if (Current.Kind == TokenKind.Word && _language.TryGetIntrinsic(Current.Text, out IntrinsicDescriptor intrinsic))
            return ParseIntrinsic(intrinsic);
        return ParseSentence();
    }
    private FilterStageNode ParseFilter()
    {
        int start = Advance().Span.Start;
        ExpressionNode? source = null;
        if (!IsWord("WHERE"))
            source = ParseAtomic();
        ExpectWord("WHERE", "FLU-SYN-120", "FILTER requires WHERE.");
        ExpressionNode predicate = ParseExpressionUntil("INTO", "THEN", "ELSE");
        string? alias = ParseOptionalResultAlias();
        return new(source, predicate, alias, TextSpan.FromBounds(start, alias is not null ? Previous.Span.End : predicate.Span.End));
    }
    private CheckStageNode ParseCheck()
    {
        int start = Advance().Span.Start;
        ExpectWord("IF", "FLU-SYN-125", "CHECK requires IF.");
        ExpressionNode condition = ParseExpressionUntil("INTO", "THEN", "ELSE");
        string? alias = ParseOptionalResultAlias();
        return new(condition, alias, TextSpan.FromBounds(start, alias is not null ? Previous.Span.End : condition.Span.End));
    }
    private CollectionStageNode ParseIntrinsic(IntrinsicDescriptor intrinsic)
    {
        SyntaxToken operation = Advance();
        int start = operation.Span.Start;
        string name = intrinsic.Name.ToUpperInvariant();
        ExpressionNode? source = null;
        ExpressionNode? argument = null;
        ExpressionNode? strategy = null;
        switch (intrinsic.Syntax)
        {
            case IntrinsicSyntaxKind.CollectionBy:
                if (!IsWord("BY"))
                    source = ParseAtomic();
                ExpectWord("BY", "FLU-SYN-160", $"{name} requires BY.");
                argument = ParseAtomic();
                break;
            case IntrinsicSyntaxKind.CollectionAmountFrom:
                argument = ParseAtomic();
                if (IsWord("FROM"))
                {
                    Advance();
                    source = ParseAtomic();
                }
                break;
            case IntrinsicSyntaxKind.CollectionDistinct:
                if (!IsWord("BY") && !IsResultBinding() && !IsStageBoundary(Current))
                    source = ParseAtomic();
                if (IsWord("BY"))
                {
                    Advance();
                    argument = ParseAtomic();
                }
                break;
            case IntrinsicSyntaxKind.CollectionSourceOptional:
                if (!IsResultBinding() && !IsStageBoundary(Current))
                    source = ParseAtomic();
                break;
            default:
                _diagnostics.Add(new("FLU-SYN-161", $"Intrinsic '{name}' uses unsupported syntax '{intrinsic.Syntax}'.", operation.Span));
                break;
        }

        if (intrinsic.StrategyType is not null && IsWord(intrinsic.StrategyRole))
        {
            Advance();
            strategy = ParseAtomic();
        }
        else if (IsWord("USING") && intrinsic.StrategyType is null)
        {
            _diagnostics.Add(new("FLU-SYN-162", $"Intrinsic '{name}' does not accept USING strategy.", Current.Span));
            Advance();
            if (!IsResultBinding() && !IsStageBoundary(Current))
                Advance();
        }

        string? alias = ParseOptionalResultAlias();
        int end = alias is not null ? Previous.Span.End : strategy?.Span.End ?? argument?.Span.End ?? source?.Span.End ?? operation.Span.End;
        return new(name, source, argument, strategy, alias, TextSpan.FromBounds(start, end));
    }
    private bool IsResultBinding() => IsWord("INTO");

    private SentenceNode? ParseSentence()
    {
        if (Current.Kind != TokenKind.Word)
        {
            _diagnostics.Add(new("FLU-SYN-001", $"Expected a verb but found '{Current.Text}'.", Current.Span));
            return null;
        }
        SyntaxToken verbToken = Advance();
        _language.TryGetVerb(verbToken.Text, out VerbDescriptor? verb);
        string? qualifier = null;
        if (Current.Kind == TokenKind.Word && _language.TryGetQualifier(Current.Text, out QualifierDescriptor qualifierDescriptor))
        {
            qualifier = qualifierDescriptor.Name;
            Advance();
        }
        else if (verb is null && Current.Kind == TokenKind.Word && !GenericScriptRoleSet().Contains(Current.Text))
        {
            qualifier = Current.Text;
            Advance();
        }
        HashSet<string> surfaceRoles = verb is null ? GenericScriptRoleSet() : BuildSurfaceRoleSet(verb);
        string? currentRole = verb is null ? LanguageRoleNames.What : DetermineImplicitRole(verb);
        SyntaxToken? roleToken = null;
        var clauses = new List<ClauseNode>();
        var values = new List<ExpressionNode>();
        string? alias = null;
        while (!IsStageBoundary(Current))
        {
            if (Current.Kind == TokenKind.Comma)
            {
                Advance();
                SkipNewLines();
                continue;
            }
            if (IsWord("INTO"))
            {
                Flush();
                alias = ParseOptionalResultAlias();
                break;
            }

            if (Current.Kind == TokenKind.Word && surfaceRoles.Contains(Current.Text))
            {
                Flush();
                roleToken = Advance();
                currentRole = roleToken.Text.ToUpperInvariant();
                SkipNewLines();
                continue;
            }
            if (currentRole is null)
            {
                _diagnostics.Add(new("FLU-SYN-003", $"Unexpected value '{Current.Text}' after {verbToken.Text}.", Current.Span));
                Advance();
                continue;
            }
            int before = _position;
            values.Add(ParseSentenceValue(surfaceRoles));
            if (_position == before)
                Advance();
        }
        Flush();
        int end = alias is not null ? Previous.Span.End : clauses.LastOrDefault()?.Span.End ?? verbToken.Span.End;
        return new(verbToken.Text, qualifier, clauses, alias, TextSpan.FromBounds(verbToken.Span.Start, end));
        void Flush()
        {
            if (currentRole is null || values.Count == 0)
            {
                values.Clear();
                roleToken = null;
                return;
            }
            int clauseStart = roleToken?.Span.Start ?? values[0].Span.Start;
            clauses.Add(new(currentRole, values.ToArray(), TextSpan.FromBounds(clauseStart, values[^1].Span.End)));
            values.Clear();
            roleToken = null;
        }
    }

    private ExpressionNode ParseSentenceValue(HashSet<string> surfaceRoles)
    {
        string[] stops = surfaceRoles.Concat(new[] { "INTO", "THEN", "ELSE" }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return ParseExpressionUntil(stops);
    }

    private static HashSet<string> BuildSurfaceRoleSet(VerbDescriptor verb) => verb.Implementations.SelectMany(x => x.Patterns).SelectMany(x => x.Roles).Where(x => !x.Name.Equals(LanguageRoleNames.What, StringComparison.OrdinalIgnoreCase)).SelectMany(x => x.AllSurfaceNames).Where(x => !LanguageRoleNames.StructuralOnly.Contains(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
    private static HashSet<string> GenericScriptRoleSet() => new(LanguageRoleNames.Contextual, StringComparer.OrdinalIgnoreCase);
    private static string? DetermineImplicitRole(VerbDescriptor verb)
    {
        RoleSlotDescriptor[] roles = verb.Implementations.SelectMany(x => x.Patterns).SelectMany(x => x.Roles).ToArray();
        if (roles.Any(x => x.Name.Equals(LanguageRoleNames.What, StringComparison.OrdinalIgnoreCase) && x.Direction is RoleDirection.Input or RoleDirection.InputOutput))
            return LanguageRoleNames.What;
        string[] requiredInputs = roles.Where(x => x.Direction != RoleDirection.Output && x.Required && !x.Name.Equals(LanguageRoleNames.What, StringComparison.OrdinalIgnoreCase)).Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (requiredInputs.Length == 1)
            return requiredInputs[0];
        if (requiredInputs.Contains(LanguageRoleNames.From, StringComparer.OrdinalIgnoreCase))
            return LanguageRoleNames.From;
        if (requiredInputs.Contains(LanguageRoleNames.At, StringComparer.OrdinalIgnoreCase))
            return LanguageRoleNames.At;
        return null;
    }
    private string? ParseOptionalResultAlias()
    {
        if (!IsWord("INTO"))
            return null;
        Advance();
        SkipNewLines();
        if (Current.Kind != TokenKind.Variable)
        {
            _diagnostics.Add(new("FLU-SYN-130", "INTO requires a [variable].", Current.Span));
            return null;
        }
        string alias = Current.Value?.ToString() ?? string.Empty;
        Advance();
        return alias;
    }
    private ExpressionNode ParseExpressionUntil(params string[] stopWords) => ParseBinary(0, stopWords);
    private ExpressionNode ParseBinary(int parentPrecedence, string[] stopWords)
    {
        ExpressionNode left;
        int unary = UnaryPrecedence();
        if (unary > 0)
        {
            if (!TryPeekOperator(OperatorArity.Unary, out OperatorDescriptor unaryOperator, out int unaryTokens))
                return ParsePrimary(stopWords);
            int start = Current.Span.Start;
            AdvanceMany(unaryTokens);
            ExpressionNode operand = ParseBinary(unaryOperator.Precedence, stopWords);
            left = new UnaryExpression(unaryOperator.Name.ToUpperInvariant(), operand, TextSpan.FromBounds(start, operand.Span.End));
        }
        else
            left = ParsePrimary(stopWords);
        while (!AtExpressionBoundary(stopWords))
        {
            if (TryParsePredicate(ref left))
                continue;
            if (!TryPeekBinaryOperator(out OperatorDescriptor descriptor, out int tokenCount))
                break;
            int precedence = descriptor.Precedence;
            if (precedence <= parentPrecedence)
                break;
            AdvanceMany(tokenCount);
            string operatorText = descriptor.Name.ToUpperInvariant();
            if (descriptor.Arity == OperatorArity.Ternary)
            {
                ExpressionNode low = ParseBinary(precedence, stopWords);
                if (!IsWord("AND"))
                {
                    _diagnostics.Add(new("FLU-SYN-170", $"{operatorText} requires AND.", Current.Span));
                    break;
                }
                Advance();
                ExpressionNode high = ParseBinary(precedence, stopWords);
                left = new BetweenExpression(operatorText, left, low, high, TextSpan.FromBounds(left.Span.Start, high.Span.End));
                continue;
            }
            int rightParent = descriptor.Associativity == OperatorAssociativity.Right ? precedence - 1 : precedence;
            ExpressionNode right = ParseBinary(rightParent, stopWords);
            left = new BinaryExpression(left, operatorText, right, TextSpan.FromBounds(left.Span.Start, right.Span.End));
        }
        return left;
    }
    private bool TryParsePredicate(ref ExpressionNode left)
    {
        if (Current.Kind == TokenKind.Word && _language.TryGetPredicate(Current.Text, out PredicateDescriptor postfix) && postfix.Syntax == PredicateSyntaxKind.Postfix)
        {
            SyntaxToken predicate = Advance();
            left = new PredicateExpression(postfix.Name.ToUpperInvariant(), left, TextSpan.FromBounds(left.Span.Start, predicate.Span.End));
            return true;
        }
        if (!IsWord("IS"))
            return false;
        int offset = 1;
        bool negate = false;
        if (IsWord("NOT", offset))
        {
            negate = true;
            offset++;
        }
        if (Peek(offset).Kind != TokenKind.Word || !_language.TryGetPredicate(Peek(offset).Text, out PredicateDescriptor statePredicate) || statePredicate.Syntax != PredicateSyntaxKind.IsState)
            return false;
        Advance();
        if (negate)
            Advance();
        SyntaxToken predicateToken = Advance();
        ExpressionNode predicateExpression = new PredicateExpression(statePredicate.Name.ToUpperInvariant(), left, TextSpan.FromBounds(left.Span.Start, predicateToken.Span.End));
        left = negate ? new UnaryExpression("NOT", predicateExpression, predicateExpression.Span) : predicateExpression;
        return true;
    }
    private ExpressionNode ParsePrimary(string[] stopWords)
    {
        if (Current.Kind == TokenKind.LeftParen)
        {
            int start = Advance().Span.Start;
            ExpressionNode expression = ParseBinary(0, Array.Empty<string>());
            if (Current.Kind == TokenKind.RightParen)
                Advance();
            return expression with
            {
                Span = TextSpan.FromBounds(start, Previous.Span.End)
            };
        }
        return ParseAtomic();
    }
    private ExpressionNode ParseAtomic()
    {
        SyntaxToken token = Advance();
        return token.Kind switch
        {
            TokenKind.Variable => ParseVariable(token),
            TokenKind.Reference => new ReferenceExpression(token.Value?.ToString() ?? string.Empty, token.Span),
            TokenKind.String => ParseInterpolated(token),
            TokenKind.Number => new LiteralExpression(token.Value, token.Span),
            TokenKind.Word when token.Text.Equals("true", StringComparison.OrdinalIgnoreCase) => new LiteralExpression(true, token.Span),
            TokenKind.Word when token.Text.Equals("false", StringComparison.OrdinalIgnoreCase) => new LiteralExpression(false, token.Span),
            TokenKind.Word when token.Text.Equals("null", StringComparison.OrdinalIgnoreCase) => new LiteralExpression(null, token.Span),
            TokenKind.Word => ParseIdentifier(token),
            _ => new LiteralExpression(token.Text, token.Span)
        };
    }
    private static ExpressionNode ParseIdentifier(SyntaxToken token)
    {
        string[] parts = token.Text.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        ExpressionNode result = new IdentifierExpression(parts.FirstOrDefault() ?? token.Text, token.Span);
        foreach (string part in parts.Skip(1))
            result = new PropertyExpression(result, part, token.Span);
        return result;
    }
    private static ExpressionNode ParseVariable(SyntaxToken token)
    {
        string[] parts = (token.Value?.ToString() ?? string.Empty).Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        ExpressionNode result = new VariableExpression(parts.FirstOrDefault() ?? string.Empty, token.Span);
        foreach (string part in parts.Skip(1))
            result = new PropertyExpression(result, part, token.Span);
        return result;
    }
    private static ExpressionNode ParseInterpolated(SyntaxToken token)
    {
        string text = token.Value?.ToString() ?? string.Empty;
        var parts = new List<ExpressionNode>();
        int cursor = 0;
        while (cursor < text.Length)
        {
            int open = text.IndexOf('[', cursor);
            if (open < 0)
            {
                if (cursor < text.Length)
                    parts.Add(new LiteralExpression(text[cursor..], token.Span));
                break;
            }
            if (open > cursor)
                parts.Add(new LiteralExpression(text[cursor..open], token.Span));
            int close = text.IndexOf(']', open + 1);
            if (close < 0)
            {
                parts.Add(new LiteralExpression(text[open..], token.Span));
                break;
            }
            string name = text[(open + 1)..close];
            if (!IsInterpolationName(name))
            {
                parts.Add(new LiteralExpression(text[open..(close + 1)], token.Span));
                cursor = close + 1;
                continue;
            }
            parts.Add(ParseVariable(new(TokenKind.Variable, name, name, token.Span)));
            cursor = close + 1;
        }
        return parts.Count == 1 && parts[0] is LiteralExpression literal ? literal : new InterpolatedStringExpression(parts, token.Span);
    }
    private static bool IsInterpolationName(string name)
    {
        string[] segments = name.Split('.');
        return segments.Length > 0 && segments.All(segment => segment.Length > 0 && (char.IsLetter(segment[0]) || segment[0] == '_') && segment.Skip(1).All(ch => char.IsLetterOrDigit(ch) || ch == '_'));
    }
    private bool AtExpressionBoundary(string[] stopWords) => Current.Kind is TokenKind.End or TokenKind.Period or TokenKind.Comma or TokenKind.Comment or TokenKind.RightBrace or TokenKind.RightParen || IsStatementStartAfterNewLine() || stopWords.Any(x => IsWord(x));
    private int UnaryPrecedence() => TryPeekOperator(OperatorArity.Unary, out OperatorDescriptor descriptor, out _) ? descriptor.Precedence : 0;
    private bool TryPeekBinaryOperator(out OperatorDescriptor descriptor, out int tokenCount)
    {
        if (TryPeekOperator(OperatorArity.Ternary, out descriptor, out tokenCount))
            return true;
        return TryPeekOperator(OperatorArity.Binary, out descriptor, out tokenCount);
    }
    private bool TryPeekOperator(OperatorArity arity, out OperatorDescriptor descriptor, out int tokenCount)
    {
        foreach (OperatorDescriptor candidate in _language.Operators.Where(x => x.Arity == arity).OrderByDescending(MaxSurfaceWordCount).ThenByDescending(x => x.Precedence))
            foreach (string surface in candidate.AllSurfaceNames.OrderByDescending(SurfaceWordCount))
            {
                string[] words = surface.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (words.Length == 0)
                    continue;
                bool matches = true;
                for (int i = 0; i < words.Length; i++)
                {
                    SyntaxToken token = Peek(i);
                    if (token.Kind is not TokenKind.Word and not TokenKind.Operator || !token.Text.Equals(words[i], StringComparison.OrdinalIgnoreCase))
                    {
                        matches = false;
                        break;
                    }
                }
                if (!matches)
                    continue;
                descriptor = candidate;
                tokenCount = words.Length;
                return true;
            }
        descriptor = null!;
        tokenCount = 0;
        return false;
    }
    private static int MaxSurfaceWordCount(OperatorDescriptor descriptor) => descriptor.AllSurfaceNames.Max(SurfaceWordCount);
    private static int SurfaceWordCount(string surface) => surface.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
    private void AdvanceMany(int count)
    {
        for (int i = 0; i < count; i++)
            Advance();
    }
    private bool IsStageBoundary(SyntaxToken token) => token.Kind is TokenKind.End or TokenKind.Period or TokenKind.Comment or TokenKind.RightBrace || IsStatementStartAfterNewLine() || IsWord("THEN") || IsWord("ELSE") || IsWord("END") || IsWord("FINALLY") || IsWord("ON") && IsWord("FAILURE", 1) || (token.Kind == TokenKind.Comma && CommaStartsPipelineContinuation());
    private bool CommaStartsPipelineContinuation()
    {
        if (Current.Kind != TokenKind.Comma)
            return false;
        int offset = 1;
        while (Peek(offset).Kind == TokenKind.NewLine)
            offset++;
        return IsWordAt("THEN", offset);
    }
    private bool IsWord(string word, int offset = 0) => IsWordAt(word, offset);
    private bool IsWordAt(string word, int offset) => Peek(offset).Kind == TokenKind.Word && Peek(offset).Text.Equals(word, StringComparison.OrdinalIgnoreCase);
    private void ExpectWord(string word, string code, string message)
    {
        if (IsWord(word))
            Advance();
        else
            _diagnostics.Add(new(code, message, Current.Span));
    }
    private void ConsumeThen(string code, string message)
    {
        if (Current.Kind != TokenKind.Comma)
            _diagnostics.Add(new("FLU-SYN-103", "A comma is required before THEN.", Current.Span));
        else
            Advance();
        SkipNewLines();
        ExpectWord("THEN", code, message);
        SkipNewLines();
    }
    private void ConsumeDo(string code, string message)
    {
        if (Current.Kind != TokenKind.Comma)
            _diagnostics.Add(new("FLU-SYN-104", "A comma is required before DO.", Current.Span));
        else
            Advance();
        SkipNewLines();
        ExpectWord("DO", code, message);
        SkipNewLines();
    }
    private void ExpectEnd(string owner, string code, string message)
    {
        if (!IsWord("END"))
        {
            _diagnostics.Add(new(code, message, Current.Span));
            return;
        }
        Advance();
        ExpectWord(owner, code, message);
        SkipNewLines();
    }
    private void RequirePeriod()
    {
        SkipNewLines();
        if (Current.Kind == TokenKind.Period)
        {
            Advance();
            return;
        }
        if (Current.Kind == TokenKind.Semicolon)
        {
            _diagnostics.Add(new("FLU-SYN-004", "Semicolons are not valid statement markers; end the sentence with '.'.", Current.Span));
            Advance();
            return;
        }
        _diagnostics.Add(new("FLU-SYN-005", "Every statement must end with '.'.", Current.Span));
    }
    private void SkipNewLines()
    {
        while (Current.Kind == TokenKind.NewLine)
            Advance();
    }
    private void SkipToBoundary()
    {
        while (Current.Kind is not TokenKind.End and not TokenKind.Period and not TokenKind.Comment and not TokenKind.RightBrace)
            Advance();
    }
    private bool IsStatementStartAfterNewLine()
    {
        if (Current.Kind != TokenKind.NewLine)
            return false;
        int offset = 1;
        while (Peek(offset).Kind == TokenKind.NewLine)
            offset++;
        return Peek(offset).Kind == TokenKind.Word && (_language.TryGetVerb(Peek(offset).Text, out _) || IsWordAt("IF", offset) || IsWordAt("TRY", offset) || (IsWordAt("FOR", offset) && IsWordAt("EACH", offset + 1)));
    }
    private SyntaxToken Current => Peek(0); private SyntaxToken Previous => Peek(-1);
    private SyntaxToken Peek(int offset)
    {
        int index = Math.Clamp(_position + offset, 0, _tokens.Count - 1);
        return _tokens[index];
    }
    private SyntaxToken Advance()
    {
        SyntaxToken token = Current;
        if (_position < _tokens.Count - 1)
            _position++;
        return token;
    }
}
