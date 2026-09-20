using Microsoft.ML.Tokenizers;
using Rag.API.Retrieval;

namespace Rag.Evals;

/// <summary>
/// Runs the golden set through the real retriever. Nothing is mocked here: the
/// question is embedded by the configured model and searched with pgvector, exactly
/// as <c>POST /query</c> would do it.
/// </summary>
public sealed class EvalRunner(IRetriever retriever, Tokenizer tokenizer)
{
    public async Task<IReadOnlyList<QuestionOutcome>> RunAsync(IReadOnlyList<EvalQuestion> questions)
    {
        var outcomes = new List<QuestionOutcome>(questions.Count);

        foreach (EvalQuestion question in questions)
        {
            IReadOnlyList<RetrievedChunk> chunks = await retriever.RetrieveAsync(question.Question);

            int[] pages = chunks.Select(chunk => chunk.PageNumber).ToArray();
            double[] distances = chunks.Select(chunk => chunk.Distance).ToArray();

            // What this question would actually cost the chat model, measured rather
            // than estimated: it is the other half of every chunk-size decision.
            int contextTokens = chunks.Sum(chunk => tokenizer.CountTokens(chunk.Text));

            outcomes.Add(new QuestionOutcome(
                question,
                pages,
                distances,
                FirstHitRank(question, pages),
                contextTokens));

            Console.Write('.');
        }

        Console.WriteLine();

        return outcomes;
    }

    private static int FirstHitRank(EvalQuestion question, int[] retrievedPages)
    {
        for (int i = 0; i < retrievedPages.Length; i++)
        {
            if (question.ExpectedPages.Contains(retrievedPages[i]))
            {
                return i + 1;
            }
        }

        return 0;
    }
}
