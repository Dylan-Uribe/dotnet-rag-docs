using Rag.API.Contracts;
using Rag.API.Query;

namespace Rag.Evals;

/// <summary>
/// Asks questions the corpus cannot answer and checks that the system says so instead
/// of inventing one. Unlike the retrieval eval this goes all the way through the chat
/// model, so it is the expensive one — and the only one whose input is a model's words.
/// </summary>
public sealed class AbstentionRunner(IQueryService queryService)
{
    /// <summary>
    /// The wording AnswerGenerator orders the model to repeat verbatim. It lives there
    /// as a private constant, so the eval matches on its stable core.
    /// </summary>
    private const string TemplateMarker = "could not find an answer";

    /// <summary>
    /// A model that refuses in its own words is still refusing. Matching only the
    /// template counts those as hallucinations, which is how this eval read 18 correct
    /// refusals as failures the first time it was pointed at a weakened prompt.
    /// This list is a heuristic and will keep needing new entries; the principled fix
    /// is a judge model, which is a different eval.
    /// </summary>
    private static readonly string[] SoftRefusalMarkers =
    [
        "context does not",
        "does not provide",
        "does not specify",
        "does not mention",
        "no information is provided",
        "not mentioned in the context",
        "cannot provide",
        "can't provide",
        "cannot assist",
        "can't assist",
        "cannot answer",
        "can't answer"
    ];

    public async Task<IReadOnlyList<AbstentionOutcome>> RunAsync(IReadOnlyList<AbstentionQuestion> questions)
    {
        var outcomes = new List<AbstentionOutcome>(questions.Count);

        foreach (AbstentionQuestion question in questions)
        {
            AnswerResponse response = await queryService.AskAsync(question.Question);

            var outcome = new AbstentionOutcome(
                question,
                response.Answer.Trim(),
                response.Citations.Count,
                Classify(response.Answer));

            outcomes.Add(outcome);

            Console.Write(outcome.IsCorrect ? '.' : 'x');
        }

        Console.WriteLine();

        return outcomes;
    }

    private static ResponseKind Classify(string answer)
    {
        if (answer.Contains(TemplateMarker, StringComparison.OrdinalIgnoreCase))
        {
            return ResponseKind.TemplateRefusal;
        }

        return SoftRefusalMarkers.Any(marker => answer.Contains(marker, StringComparison.OrdinalIgnoreCase))
            ? ResponseKind.SoftRefusal
            : ResponseKind.Answered;
    }
}
