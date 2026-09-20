using Microsoft.Extensions.AI;

namespace Rag.Tests.TestSupport;

/// <summary>
/// Stands in for the chat model. Captures the messages it is sent, which is the
/// only part of a model call that is deterministic enough to assert on.
/// </summary>
internal sealed class FakeChatClient(string answer = "A grounded answer.") : IChatClient
{
    public List<ChatMessage[]> ReceivedMessages { get; } = [];

    public int CallCount => ReceivedMessages.Count;

    public ChatMessage[] LastMessages => ReceivedMessages[^1];

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ReceivedMessages.Add(messages.ToArray());

        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, answer)));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The answer generator does not stream.");

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
