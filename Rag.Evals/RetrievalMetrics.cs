namespace Rag.Evals;

public static class RetrievalMetrics
{
    /// <summary>
    /// Share of questions whose answer page appears in the first k results.
    /// This is the ceiling on the generator: what is not retrieved cannot be answered.
    /// </summary>
    public static double RecallAt(IReadOnlyList<QuestionOutcome> outcomes, int k) =>
        outcomes.Count == 0 ? 0 : outcomes.Count(outcome => outcome.IsHitWithin(k)) / (double)outcomes.Count;

    /// <summary>
    /// Mean reciprocal rank: rewards putting the answer first, not merely somewhere.
    /// A miss contributes zero.
    /// </summary>
    public static double MeanReciprocalRank(IReadOnlyList<QuestionOutcome> outcomes) =>
        outcomes.Count == 0
            ? 0
            : outcomes.Sum(outcome => outcome.FirstHitRank == 0 ? 0 : 1.0 / outcome.FirstHitRank) / outcomes.Count;
}
