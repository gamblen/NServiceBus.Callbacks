namespace NServiceBus.AcceptanceTests.Callbacks
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_int_response_canceled : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task ShouldNot_trigger_the_callback_when_canceled()
        {
            OperationCanceledException exception = null;

            var context = await Scenario.Define<Context>()
                .WithEndpoint<EndpointWithLocalCallback>(b => b.When(async (bus, c) =>
                {
                    var cs = new CancellationTokenSource();
                    c.TokenSource = cs;

                    var options = new SendOptions();

                    options.RegisterCancellationToken(cs.Token);

                    try
                    {
                        c.Response = await bus.Request<int>(new MyRequest(), options);
                        c.CallbackFired = true;
                    }
                    catch (OperationCanceledException e)
                    {
                        exception = e;
                    }
                }))
                .WithEndpoint<Replier>()
                .Done(c => exception != null || c.HandlerGotTheRequest)
                .Run();

            Assert.AreNotEqual(200, context.Response);
            Assert.False(context.CallbackFired);
            Assert.True(context.HandlerGotTheRequest);
            Assert.IsInstanceOf<OperationCanceledException>(exception);
        }

        public class Context : ScenarioContext
        {
            public CancellationTokenSource TokenSource { get; set; }
            public bool HandlerGotTheRequest { get; set; }
            public bool CallbackFired { get; set; }
            public int Response { get; set; }

        }

        public class Replier : EndpointConfigurationBuilder
        {
            public Replier()
            {
                EndpointSetup<DefaultServer>();
            }

            public class MyRequestHandler : IHandleMessages<MyRequest>
            {
                public Context Context { get; set; }

                public Task Handle(MyRequest message, IMessageHandlerContext context)
                {
                    Context.HandlerGotTheRequest = true;
                    Context.TokenSource.Cancel();

                    return context.Reply(200);
                }
            }
        }

        public class EndpointWithLocalCallback : EndpointConfigurationBuilder
        {
            public EndpointWithLocalCallback()
            {
                EndpointSetup<DefaultServer>()
                    .AddMapping<MyRequest>(typeof(Replier));
            }
        }

        public class MyRequest : IMessage { }

    }
}