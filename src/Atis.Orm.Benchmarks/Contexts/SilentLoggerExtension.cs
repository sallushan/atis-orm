using Atzonix.DependencyInjection;
using Atis.Orm;
using Atis.SqlExpressionEngine.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Atis.Orm.Benchmarks.Contexts
{
    /// <summary>
    /// Replaces Atis's default <see cref="ILogger"/>, which writes the whole translation trace to
    /// <c>Console.Out</c>, with one that writes nothing.
    ///
    /// Two reasons this matters here and not in an application: the trace fires on every
    /// compiled-query cache miss, so it lands inside the first measured invocation of an Atis
    /// benchmark; and BenchmarkDotNet's child process talks to its host over stdout, which is not a
    /// stream to sprinkle diagnostics into.
    ///
    /// Registration is first-wins, so this extension has to be added to the configuration
    /// <em>before</em> the provider extension whose <c>AddCoreServices()</c> supplies the default.
    /// </summary>
    public class SilentLoggerExtension : IServiceContextExtension
    {
        public void AddServices(IServiceCollection services)
        {
            var builder = new OrmServiceBuilder(services);
            builder.TryAdd<ILogger, SilentLogger>();
        }

        private sealed class SilentLogger : ILogger
        {
            public void Indent() { }
            public void Unindent() { }
            public void Log(string logText) { }
        }
    }
}
