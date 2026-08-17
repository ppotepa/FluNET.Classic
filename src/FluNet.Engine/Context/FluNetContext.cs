using FluNET.Binding;
using FluNET.Language;
using FluNET.Runtime;
using FluNET.Syntax.Parsing;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Context;

public sealed class FluNETContext : IDisposable
{
    private static FluNETContext? _defaultContext;
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScope _scope;

    public static FluNETContext Default => _defaultContext ??= Create();

    private FluNETContext(ServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _scope = _serviceProvider.CreateScope();
    }

    public static FluNETContext Create(Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        ConfigureDefaultServices(services);
        configureServices?.Invoke(services);
        return new FluNETContext(services.BuildServiceProvider());
    }

    public static void ConfigureDefaultServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<LanguageCompiler>();
        services.AddSingleton(sp => sp.GetRequiredService<LanguageCompiler>().Compile());

        services.AddSingleton<ValueResolverRegistry>();
        services.AddSingleton<ValueConversionRegistry>();

        services.AddTransient<ClassicLexer>();
        services.AddTransient<ClassicParser>(sp =>
            new ClassicParser(
                sp.GetRequiredService<LanguageSnapshot>(),
                sp.GetRequiredService<ClassicLexer>()));

        services.AddTransient<SemanticBinder>(sp =>
            new SemanticBinder(
                sp.GetRequiredService<LanguageSnapshot>(),
                sp.GetRequiredService<ValueResolverRegistry>(),
                sp.GetRequiredService<ValueConversionRegistry>(),
                sp));

        services.AddTransient<VerbActivator>(sp => new VerbActivator(sp));
        services.AddTransient<BoundExecutor>();
        services.AddTransient<ClassicEngine>();
    }

    public ClassicEngine GetEngine() => GetService<ClassicEngine>();

    public T GetService<T>() where T : notnull =>
        _scope.ServiceProvider.GetRequiredService<T>();

    public object GetService(Type serviceType) =>
        _scope.ServiceProvider.GetRequiredService(serviceType);

    public IServiceProvider ServiceProvider => _scope.ServiceProvider;

    public void Dispose()
    {
        _scope.Dispose();
        _serviceProvider.Dispose();
        if (ReferenceEquals(this, _defaultContext))
        {
            _defaultContext = null;
        }
    }

    public static void ResetDefault()
    {
        _defaultContext?.Dispose();
        _defaultContext = null;
    }
}
