using System;

namespace PactNet.Verifier.Messaging
{
    /// <summary>
    /// Defines the message scenario builder interface
    /// </summary>
    public interface IMessageScenarioContentBuilder
    {
        /// <summary>
        /// Set the metadata of the message content
        /// </summary>
        /// <param name="metadata">the metadata</param>
        /// <returns>Fluent builder</returns>
        IMessageScenarioContentBuilder WithMetadata(dynamic metadata);

        /// <summary>
        /// Set the action of the scenario
        /// </summary>
        /// <param name="action">the function invoked</param>
        Scenario WithContent(Func<dynamic> action);

        /// <summary>
        /// Set the object returned by the scenario
        /// </summary>
        /// <param name="messageContent">the message content</param>
        Scenario WithContent(dynamic messageContent); // TODO: I don't think this works because it doesn't know which overload to call due to the dynamic
    }
}