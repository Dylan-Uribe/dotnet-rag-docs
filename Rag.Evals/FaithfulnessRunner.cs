using Rag.API.Contracts;
using Rag.API.Generation;
using Rag.API.Retrieval;

namespace Rag.Evals;

/// <summary>
/// Grades the answers the system actually produces. Retrieval and generation are the
/// real ones; only the grading is delegated to a second model, and that model is
/// calibrated first because an uncalibrated judge produces confident numbers about
/// nothing.
/// </summary>
public sealed class FaithfulnessRunner(IRetriever retriever, IAnswerGenerator generator, Judge judge)
{
    /// <summary>
    /// Checks the judge against hand-labelled cases before it is used. Agreement here
    /// is the licence to believe the run that follows.
    /// </summary>
    public async Task<IReadOnlyList<CalibrationOutcome>> CalibrateAsync(
        IReadOnlyList<CalibrationCase> cases)
    {
        var outcomes = new List<CalibrationOutcome>(cases.Count);

        foreach (CalibrationCase testCase in cases)
        {
            JudgeVerdict verdict = await judge.JudgeAsync(
                testCase.Question,
                testCase.Context,
                testCase.Answer,
                testCase.Reference);

            var outcome = new CalibrationOutcome(testCase, verdict);
            outcomes.Add(outcome);

            Console.Write(outcome.FaithfulAgrees ? '.' : 'x');
        }

        Console.WriteLine();

        return outcomes;
    }

    public async Task<IReadOnlyList<FaithfulnessOutcome>> RunAsync(IReadOnlyList<EvalQuestion> questions)
    {
        var outcomes = new List<FaithfulnessOutcome>(questions.Count);

        foreach (EvalQuestion question in questions)
        {
            IReadOnlyList<RetrievedChunk> chunks = await retriever.RetrieveAsync(question.Question);

            AnswerResponse response = await generator.GenerateAsync(question.Question, chunks);

            // The judge must see exactly what the generator saw, not the whole document.
            string context = string.Join(
                "\n\n---\n\n",
                chunks.Select(chunk => $"[{chunk.FileName}, page {chunk.PageNumber}]\n{chunk.Text}"));

            JudgeVerdict verdict = await judge.JudgeAsync(
                question.Question,
                context,
                response.Answer,
                question.Answer);

            var outcome = new FaithfulnessOutcome(question, response.Answer, verdict);
            outcomes.Add(outcome);

            Console.Write(verdict is { Faithful: true, Correct: true } ? '.' : 'x');
        }

        Console.WriteLine();

        return outcomes;
    }
}
