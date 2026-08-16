using Ironbell.Api.Common.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Ironbell.Api.Tests.Common.Messaging;

public class DispatcherTests
{
    [Fact]
    public async Task Behaviours_wrap_the_handler_outermost_first()
    {
        var log = new List<string>();
        var dispatcher = BuildDispatcher(log, "outer", "inner");

        await dispatcher.SendAsync<TestRequest, string>(new TestRequest(), TestContext.Current.CancellationToken);

        log.ShouldBe(["outer:before", "inner:before", "handler", "inner:after", "outer:after"]);
    }

    [Fact]
    public async Task Handler_runs_when_no_behaviours_are_registered()
    {
        var log = new List<string>();
        var dispatcher = BuildDispatcher(log);

        var result = await dispatcher.SendAsync<TestRequest, string>(new TestRequest(), TestContext.Current.CancellationToken);

        result.ShouldBe("handled");
        log.ShouldBe(["handler"]);
    }

    [Fact]
    public async Task Missing_handler_fails_loudly_rather_than_silently()
    {
        var services = new ServiceCollection().AddLogging().AddMessaging().BuildServiceProvider();
        var dispatcher = services.GetRequiredService<IDispatcher>();

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await dispatcher.SendAsync<TestRequest, string>(new TestRequest(), TestContext.Current.CancellationToken));
    }

    private static IDispatcher BuildDispatcher(List<string> log, params string[] behaviourNames)
    {
        // AddMessaging registers LoggingBehaviour, which needs a logger to construct.
        var services = new ServiceCollection().AddLogging().AddMessaging();
        services.AddScoped<IHandler<TestRequest, string>>(_ => new TestHandler(log));

        foreach (var name in behaviourNames)
        {
            var captured = name;
            services.AddScoped<IPipelineBehaviour<TestRequest, string>>(
                _ => new RecordingBehaviour(log, captured));
        }

        return services.BuildServiceProvider().GetRequiredService<IDispatcher>();
    }

    private sealed record TestRequest : IRequest<string>;

    private sealed class TestHandler(List<string> log) : IHandler<TestRequest, string>
    {
        public ValueTask<string> HandleAsync(TestRequest request, CancellationToken cancellationToken)
        {
            log.Add("handler");
            return ValueTask.FromResult("handled");
        }
    }

    private sealed class RecordingBehaviour(List<string> log, string name)
        : IPipelineBehaviour<TestRequest, string>
    {
        public async ValueTask<string> HandleAsync(
            TestRequest request,
            PipelineStep<string> nextStep,
            CancellationToken cancellationToken)
        {
            log.Add($"{name}:before");
            var response = await nextStep();
            log.Add($"{name}:after");
            return response;
        }
    }
}
