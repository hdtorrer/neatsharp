using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NeatSharp.Evaluation;
using NeatSharp.Genetics;
using NeatSharp.Gpu.Configuration;
using NeatSharp.Gpu.Detection;
using NeatSharp.Gpu.Evaluation;

namespace NeatSharp.Gpu.Extensions;

/// <summary>
/// Extension methods for registering NeatSharp GPU services with the
/// <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers NeatSharp GPU evaluation services including device detection,
    /// GPU-accelerated network building, and batch evaluation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This registers the following services:
    /// <list type="bullet">
    ///   <item><see cref="GpuOptions"/> with validation (data annotations + custom validator).</item>
    ///   <item><see cref="IGpuDeviceDetector"/> as a singleton for GPU device detection.</item>
    ///   <item><see cref="INetworkBuilder"/> as a singleton <see cref="GpuNetworkBuilder"/>
    ///     that decorates the existing <see cref="FeedForwardNetworkBuilder"/>.</item>
    ///   <item><see cref="IBatchEvaluator"/> as a scoped <see cref="GpuBatchEvaluator"/>.</item>
    /// </list>
    /// </para>
    /// <para>
    /// Users must register their own <see cref="IGpuFitnessFunction"/> implementation
    /// separately (e.g., <c>services.AddSingleton&lt;IGpuFitnessFunction, MyFitness&gt;()</c>).
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">
    /// Optional action to configure <see cref="GpuOptions"/>. When null, defaults are used.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddNeatSharpGpu(
        this IServiceCollection services,
        Action<GpuOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register options with validation
        var optionsBuilder = services.AddOptions<GpuOptions>();
        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }
        optionsBuilder.ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IValidateOptions<GpuOptions>, GpuOptionsValidator>();

        // Register GPU device detector as singleton (cached after first detect)
        services.AddSingleton<IGpuDeviceDetector, GpuDeviceDetector>();

        // Register the concrete FeedForwardNetworkBuilder so it can be resolved
        // as the inner builder for the GpuNetworkBuilder decorator
        services.TryAddSingleton<FeedForwardNetworkBuilder>();

        // Decorator: replace INetworkBuilder with GpuNetworkBuilder wrapping the existing one
        services.Replace(ServiceDescriptor.Singleton<INetworkBuilder>(sp =>
            new GpuNetworkBuilder(sp.GetRequiredService<FeedForwardNetworkBuilder>())));

        // Register batch evaluator as scoped (holds GPU resources per scope)
        services.AddScoped<IBatchEvaluator, GpuBatchEvaluator>();

        return services;
    }
}
