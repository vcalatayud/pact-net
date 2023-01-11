using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PactNet;
using PactNet.Infrastructure.Outputters;
using PactNet.Verifier;
using PactNet.Verifier.Messaging;
using Xunit;
using Xunit.Abstractions;

namespace Provider.Tests
{
    public class StockEventGeneratorTests : IDisposable
    {
        private readonly PactVerifier verifier;

        public StockEventGeneratorTests(ITestOutputHelper output)
        {
            this.verifier = new PactVerifier(new PactVerifierConfig
            {
                Outputters = new List<IOutput>
                {
                    new XUnitOutput(output)
                },
                LogLevel = PactLogLevel.Debug
            });
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            this.verifier.Dispose();
        }

        [Fact]
        public void EnsureEventApiHonoursPactWithConsumer()
        {
            string pactPath = Path.Combine("..",
                                           "..",
                                           "..",
                                           "..",
                                           "Consumer.Tests",
                                           "pacts",
                                           "Stock Event Consumer-Stock Event Producer.json");

            var defaultSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                DefaultValueHandling = DefaultValueHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.Indented
            };

            /**
            * Provider will have to say:
            * Which is the consumer it wants to verify
            * What to do with the provider states
            * And what to do with the "When" the when is supposed to trigger the message so that it gets captured and can be compared
            **/

            this.verifier
                .MessagingProvider("Stock Event Producer", defaultSettings) 
                .WithProviderMessages(scenarios =>
                 {
                    /**
                    * We'll need to allow to define these "scenarios" in a clean way
                    * Maybe having a "Trigger" class or "Event", "Action" that can be created and added in the configuration
                    * With a method receiving the IMessageScenarioBuilder
                    * And methods to match the when? 
                    * Seems like actions should match the "When" exactly.
                    * We could have Actions like "an invoice is created"
                    * the action will fill the DoAction() method
                    * that eventually will trigger the latest part of the CreateInvoice() process
                    * (this way we ensure that the provider generates the message also in production)
                    * but we first need to mock the real sender
                    * we could provide a mock of the desired interface in the method?
                    * something like: DoAction(IMessageSender sender)
                    * but then we are forcing the production code to have a class implementing the IMessageSender
                    * the provider will have to implement the way to capture the message and avoid sending it
                    * for each Action
                    * so the DoAction() expects a message in return, or a collection of messages
                    * should we call it GenerateMessage?
                    * or DoAction(MessageSender sender)
                    * waiting for a call to sender.Send(message) inside the method
                    * or the DoAction() method returning a Message object
                    * the Message object has MetaData and Content
                    * yes, I think this last option is more intuitive
                    * this way you are expected to fill the method with an action that returns a message, so you'll need to generate it and capture it inside
                    **/
                     scenarios.Add("a single event",
                                   () => new StockEvent
                                   {
                                       Name = "AAPL",
                                       Price = 1.23m,
                                       Timestamp = new DateTimeOffset(2022, 2, 14, 13, 14, 15, TimeSpan.Zero)
                                   })
                              .Add("some stock ticker events", SetupStockEvents);
                 })
                .WithFileSource(new FileInfo(pactPath))
                .Verify();
        }

        private static async Task SetupStockEvents(IMessageScenarioBuilder builder)
        {
            //-----------------------------------------------------
            // A complex example of scenario setting.
            //
            // - here we chose to generate the object from the business component
            //   by mocking the last call before sending information to the queue
            //-----------------------------------------------------
            await builder.WithMetadata(new
                          {
                              ContentType = "application/json",
                              Key = "valueKey"
                          })
                         .WithContentAsync(async () =>
                          {
                              //The component that will send the message to the queue (Kafka, Rabbit, etc)
                              var mockSender = new Mock<IStockEventSender>();

                              ICollection<StockEvent> eventsPushed = null;

                              //The last method called before sending the message to the queue needs to be mocked
                              //... and the message (here eventPushed) needs to be intercepted in the test for verification
                              mockSender.Setup(x => x.SendAsync(It.IsAny<ICollection<StockEvent>>()))
                                        .Callback((ICollection<StockEvent> events) => eventsPushed = events)
                                        .Returns(ValueTask.CompletedTask);

                              //We call the real producer to generate some real messages, which are captured above
                              var generator = new StockEventGenerator(mockSender.Object);
                              await generator.GenerateEventsAsync();

                              eventsPushed.Should().NotBeNull().And.NotBeEmpty();

                              return eventsPushed;
                          });

            /*-----------------------------------------------------
             A simple example of scenario setting.
            
             - here we chose to generate the object manually
            
               WARNING: be careful with this approach, you never
               guarantee your actual code is in sync with the
               manually generated object below.
            -----------------------------------------------------

            builder.WithMetadata(new { key = "valueKey" })
                   .WithContent(new List<Event>
                   {
                       new StockEvent
                       {
                           Name = "AAPL",
                           Price = 1.23m,
                           Timestamp = new DateTimeOffset(2022, 2, 14, 13, 14, 15, TimeSpan.Zero)
                       }
                   });
            */
        }
    }
}
