using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeatSharp.Evaluation;
using NeatSharp.Genetics;
using NeatSharp.Gpu.Configuration;
using NeatSharp.Gpu.Detection;
using NeatSharp.Gpu.Evaluation;
using NeatSharp.Gpu.Scheduling;

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

    /// <summary>
    /// Registers hybrid CPU+GPU evaluation services that partition genomes across
    /// CPU and GPU backends, dispatch concurrently, and merge results.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method must be called <strong>after</strong> <see cref="AddNeatSharpGpu"/>
    /// because it decorates the existing <see cref="IBatchEvaluator"/> registration
    /// (GPU backend) with a <see cref="HybridBatchEvaluator"/> that wraps both
    /// a CPU and GPU backend.
    /// </para>
    /// <para>
    /// The CPU backend is created by adapting the existing <see cref="IEvaluationStrategy"/>
    /// registration (from <c>AddNeatSharp()</c>) into an <see cref="IBatchEvaluator"/>.
    /// </para>
    /// <para>
    /// Registers:
    /// <list type="bullet">
    ///   <item><see cref="HybridOptions"/> with validation (data annotations + custom validator).</item>
    ///   <item><see cref="IPartitionPolicy"/> based on <see cref="HybridOptions.SplitPolicy"/>.</item>
    ///   <item><see cref="ISchedulingMetricsReporter"/> (default: <see cref="LoggingMetricsReporter"/>).</item>
    ///   <item><see cref="IBatchEvaluator"/> replaced with <see cref="HybridBatchEvaluator"/> decorator.</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">
    /// Optional action to configure <see cref="HybridOptions"/>. When null, defaults are used.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddNeatSharpHybrid(
        this IServiceCollection services,
        Action<HybridOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register HybridOptions with validation
        var optionsBuilder = services.AddOptions<HybridOptions>();
        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }
        optionsBuilder.ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IValidateOptions<HybridOptions>, HybridOptionsValidator>();

        // Register default metrics reporter (logging) — users can override by registering
        // their own ISchedulingMetricsReporter before calling AddNeatSharpHybrid().
        services.TryAddSingleton<ISchedulingMetricsReporter, LoggingMetricsReporter>();

        // Register partition policy based on SplitPolicyType from options
        services.AddScoped<IPartitionPolicy>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<HybridOptions>>().Value;
            return options.SplitPolicy switch
            {
                SplitPolicyType.Static => new StaticPartitionPolicy(options.StaticGpuFraction),
                SplitPolicyType.CostBased => new CostBasedPartitionPolicy(options.StaticGpuFraction, options.CostModel),
                SplitPolicyType.Adaptive => new AdaptivePartitionPolicy(options.Adaptive),
                _ => new StaticPartitionPolicy(options.StaticGpuFraction)
            };
        });

        // Capture the existing IBatchEvaluator descriptor (GPU backend) before replacing.
        // We need to resolve it in the factory below as the GPU backend.
        var existingDescriptor = services.LastOrDefault(d => d.ServiceType == typeof(IBatchEvaluator));

        // Replace IBatchEvaluator with HybridBatchEvaluator decorator
        services.Replace(ServiceDescriptor.Scoped<IBatchEvaluator>(sp =>
        {
            // Resolve the GPU backend from the captured descriptor
            IBatchEvaluator gpuEvaluator = ResolveFromDescriptor<IBatchEvaluator>(sp, existingDescriptor);

            // Create CPU backend by adapting IEvaluationStrategy
            var evaluationStrategy = sp.GetRequiredService<IEvaluationStrategy>();
            IBatchEvaluator cpuEvaluator = new EvaluationStrategyBatchAdapter(evaluationStrategy);

            return new HybridBatchEvaluator(
                cpuEvaluator,
                gpuEvaluator,
                sp.GetRequiredService<IPartitionPolicy>(),
                sp.GetRequiredService<ISchedulingMetricsReporter>(),
                sp.GetRequiredService<IOptions<HybridOptions>>(),
                sp.GetRequiredService<ILogger<HybridBatchEvaluator>>());
        }));

        return services;
    }

    private static T ResolveFromDescriptor<T>(IServiceProvider sp, ServiceDescriptor? descriptor)
        where T : class
    {
        if (descriptor is null)
        {
            throw new InvalidOperationException(
                $"No existing registration found for {typeof(T).Name}. " +
                $"Ensure AddNeatSharpGpu() is called before AddNeatSharpHybrid().");
        }

        if (descriptor.ImplementationFactory is not null)
        {
            return (T)descriptor.ImplementationFactory(sp);
        }

        if (descriptor.ImplementationType is not null)
        {
            return (T)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType);
        }

        if (descriptor.ImplementationInstance is not null)
        {
            return (T)descriptor.ImplementationInstance;
        }

        throw new InvalidOperationException(
            $"Cannot resolve {typeof(T).Name} from captured service descriptor.");
    }
}
