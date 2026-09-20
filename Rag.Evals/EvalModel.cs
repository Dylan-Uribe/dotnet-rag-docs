namespace Rag.Evals;

/// <summary>
/// One labelled question. <see cref="ExpectedPages"/> is the ground truth: the pages
/// that actually answer it, written by hand against the corpus.
/// </summary>
public sealed record EvalQuestion(
    string Id,
    string Question,
    int[] ExpectedPages,
    string Kind,
    string Answer);

/// <summary>
/// What retrieval returned for one question. <see cref="FirstHitRank"/> is 1-based;
/// zero means no expected page appeared anywhere in the retrieved set.
/// </summary>
public sealed record QuestionOutcome(
    EvalQuestion Question,
    IReadOnlyList<int> RetrievedPages,
    IReadOnlyList<double> Distances,
    int FirstHitRank)
{
    public bool IsHitWithin(int k) => FirstHitRank > 0 && FirstHitRank <= k;
}
