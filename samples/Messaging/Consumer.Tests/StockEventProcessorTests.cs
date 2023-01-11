using System.Collections.Generic;
using FluentAssertions;
using FluentAssertions.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PactNet;
using PactNet.Matchers;
using Xunit;
using Xunit.Abstractions;

namespace Consumer.Tests
{
    public class StockEventProcessorTests
    {
        private readonly IMessagePactBuilderV3 messagePact;

        public StockEventProcessorTests(ITestOutputHelper output)
        {
            IPactV3 v3 = Pact.V3("Stock Event Consumer", "Stock Event Producer", new PactConfig
            {
                PactDir = "../../../pacts/",
                DefaultJsonSettings = new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                },
                Outputters = new[]
                {
                    new XUnitOutput(output)
                }
            });

            this.messagePact = v3.WithMessageInteractions();
        }

        [Fact]
        public void ReceiveSomeStockEvents()
        {
            this.messagePact
                .ExpectsToReceive("some stock ticker events") //This is the When, the trigger for the message that should be generated
                .Given("A list of events is pushed to the queue")
                .WithMetadata("key", "valueKey")
                /**
                * Here we set the expectations for the received message
                **/
                .WithJsonContent(Match.MinType(new
                {
                    Name = Match.Type("AAPL"),
                    Price = Match.Decimal(1.23m),
                    Timestamp = Match.Type(14.February(2022).At(13, 14, 15, 678))
                }, 1))
                .Verify<ICollection<StockEvent>>(events =>
                {
                    /**
                    * And this is the RunTest part
                    * Here we won't check the message content manually, we already defined our expectations above
                    * but we will process the message with production classes to be sure that we are able to handle it
                    **/
                    events.Should().BeEquivalentTo(new[]
                    {
                        new StockEvent
                        {
                            Name = "AAPL",
                            Price = 1.23m,
                            Timestamp = 14.February(2022).At(13, 14, 15, 678)
                        }
                    });
                });
        }
    }
}
