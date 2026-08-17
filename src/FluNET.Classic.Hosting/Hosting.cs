using FluNET.Classic.Binding;
using FluNET.Classic.Core;
using FluNET.Classic.Runtime;
using FluNET.Classic.Standard;
using FluNET.Classic.Standard.Http;
using FluNET.Classic.Standard.Text;
using FluNET.Classic.Syntax;
using FluNET.Classic.Tooling;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Classic.Hosting;

public sealed class FluNetOptions
{
    public IList<ILanguageModule> Modules { get; } = StandardModules.Create().ToList();
    public ISet<string>? AllowedCapabilities { get; set; }
    public Action<ValueResolverRegistry>? ConfigureResolvers { get; set; }
    public Action<ValueConversionRegistry>? ConfigureConverters { get; set; }
    public Action<PredicateRegistry>? ConfigurePredicates { get; set; }
    public Action<ExecutionPolicy>? ConfigureExecution { get; set; }
}

public static class FluNetHostingExtensions
{
    public static IServiceCollection AddFluNetClassic(this IServiceCollection services, FluNetOptions? options = null)
    {
        options ??= new FluNetOptions(); ILanguageModule[] modules = options.Modules.ToArray(); IReadOnlyList<LanguageDiagnostic> moduleDiagnostics = ModuleGraphValidator.Validate(modules); if (moduleDiagnostics.Any(x => x.Severity == LanguageDiagnosticSeverity.Error)) throw new LanguageCompilationException(moduleDiagnostics);
        services.AddSingleton(options); services.AddSingleton(modules); services.AddSingleton<LanguageCompiler>(); services.AddSingleton(sp => sp.GetRequiredService<LanguageCompiler>().Build(modules: modules)); services.AddSingleton(sp => sp.GetRequiredService<LanguageBuildResult>().ThrowIfFailed()); services.AddSingleton<LanguageIntrospectionService>(); services.AddSingleton<ClassicLanguageService>();
        services.AddSingleton(sp => { var registry = new ValueResolverRegistry(); options.ConfigureResolvers?.Invoke(registry); return registry; }); services.AddSingleton(sp => { var registry = new ValueConversionRegistry(); options.ConfigureConverters?.Invoke(registry); return registry; }); services.AddSingleton(sp => { var registry = new PredicateRegistry(); options.ConfigurePredicates?.Invoke(registry); return registry; });
        services.AddSingleton(sp => { var policy = new ExecutionPolicy(); options.ConfigureExecution?.Invoke(policy); return policy; });
        services.AddSingleton<ICapabilityPolicy>(_ => options.AllowedCapabilities is null ? new AllowAllCapabilityPolicy() : new CapabilitySetPolicy(options.AllowedCapabilities)); services.AddSingleton(new HttpClient()); services.AddSingleton<IOutputWriter, ConsoleOutputWriter>(); services.AddSingleton<IEmailSender, MissingEmailSender>(); services.AddSingleton<ClassicLexer>(); services.AddSingleton<ClassicFormatter>(); services.AddSingleton<ExecutionPlanner>();
        services.AddTransient(sp => new ClassicParser(sp.GetRequiredService<LanguageSnapshot>(), sp.GetRequiredService<ClassicLexer>())); services.AddTransient(sp => new SemanticBinder(sp.GetRequiredService<LanguageSnapshot>(), sp.GetRequiredService<ValueResolverRegistry>(), sp.GetRequiredService<ValueConversionRegistry>(), sp.GetRequiredService<PredicateRegistry>(), sp)); services.AddTransient(sp => new BoundExecutor(sp.GetRequiredService<ValueConversionRegistry>(), sp.GetRequiredService<PredicateRegistry>(), sp.GetRequiredService<ICapabilityPolicy>(), sp, sp.GetRequiredService<ExecutionPolicy>())); services.AddTransient<ClassicEngine>(); services.AddTransient<ClassicDocumentService>(); return services;
    }
}

public static class FluNetHost
{
    public static ServiceProvider Create(FluNetOptions? options = null, Action<IServiceCollection>? configure = null) { var services = new ServiceCollection(); services.AddFluNetClassic(options); configure?.Invoke(services); return services.BuildServiceProvider(); }
}
public sealed class ConsoleOutputWriter : IOutputWriter { public ValueTask WriteLineAsync(string text, CancellationToken cancellationToken = default) { Console.WriteLine(text); return ValueTask.CompletedTask; } }
public sealed class MissingEmailSender : IEmailSender { public ValueTask SendAsync(string to, string message, CancellationToken cancellationToken = default) => throw new InvalidOperationException("No IEmailSender is registered. Configure one in the host before using SEND."); }
