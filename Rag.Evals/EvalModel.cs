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
    int FirstHitRank,
    int ContextTokens)
{
    public bool IsHitWithin(int k) => FirstHitRank > 0 && FirstHitRank <= k;
}

/// <summary>One chunking configuration and what the golden set scored under it.</summary>
public sealed record SweepResult(
    int ChunkSizeTokens,
    int ChunkOverlapTokens,
    int ChunkCount,
    IReadOnlyList<QuestionOutcome> Outcomes)
{
    public double AverageContextTokens => Outcomes.Average(outcome => outcome.ContextTokens);

    public IEnumerable<string> MissedQuestionIds =>
        Outcomes.Where(outcome => outcome.FirstHitRank == 0).Select(outcome => outcome.Question.Id);
}

/// <summary>
/// One question for the abstention eval. <see cref="Answerable"/> is the ground truth:
/// false means the corpus does not contain the answer and the system must say so.
/// </summary>
public sealed record AbstentionQuestion(
    string Id,
    string Question,
    bool Answerable,
    string Kind,
    string Note);

/// <summary>
/// How the system replied. The distinction matters: the prompt orders a verbatim
/// refusal, so answering "the context does not say" is correct behaviour but broken
/// prompt compliance, and the two are worth measuring apart.
/// </summary>
public enum ResponseKind
{
    /// <summary>The exact wording the prompt demands.</summary>
    TemplateRefusal,

    /// <summary>A refusal in the model's own words.</summary>
    SoftRefusal,

    /// <summary>An actual answer.</summary>
    Answered
}

public sealed record AbstentionOutcome(
    AbstentionQuestion Question,
    string Answer,
    int CitationCount,
    ResponseKind Response)
{
    public bool Abstained => Response != ResponseKind.Answered;

    /// <summary>Answering a question the corpus cannot support.</summary>
    public bool IsHallucination => !Question.Answerable && !Abstained;

    /// <summary>Refusing a question the corpus does answer.</summary>
    public bool IsOverRefusal => Question.Answerable && Abstained;

    public bool IsCorrect => !IsHallucination && !IsOverRefusal;
}

/// <summary>
/// A hand-labelled grading case. The context is written inline rather than retrieved,
/// so the judge is tested on a fixed input whose correct verdict is known.
/// </summary>
public sealed record CalibrationCase(
    string Id,
    string Context,
    string Question,
    string Answer,
    string? Reference,
    bool ExpectedFaithful,
    bool? ExpectedCorrect,
    string Note);

public sealed record CalibrationOutcome(CalibrationCase Case, JudgeVerdict Verdict)
{
    public bool FaithfulAgrees => Verdict.Faithful == Case.ExpectedFaithful;

    /// <summary>Null where the expected verdict is genuinely arguable, and excluded from the score.</summary>
    public bool? CorrectAgrees =>
        Case.ExpectedCorrect is null ? null : Verdict.Correct == Case.ExpectedCorrect;
}

public sealed record FaithfulnessOutcome(EvalQuestion Question, string Answer, JudgeVerdict Verdict);

public enum InjectionPosition
{
    /// <summary>Ahead of the legitimate passages.</summary>
    First,

    /// <summary>After them, closest to the question.</summary>
    Last
}

/// <summary>
/// One hostile passage plus the legitimate question it is meant to hijack.
/// <see cref="ExpectedFact"/> is what a healthy answer still contains, which separates
/// resisting the attack from being disrupted by it.
/// </summary>
public sealed record InjectionAttack(
    string Id,
    string Kind,
    string Question,
    string Payload,
    string? SuccessMarker,
    bool SuccessOnAbstention,
    string ExpectedFact,
    string Note);

public sealed record InjectionOutcome(
    InjectionAttack Attack,
    InjectionPosition Position,
    string Answer,
    bool Obeyed,
    bool TaskPreserved)
{
    public bool Resisted => !Obeyed;

    /// <summary>Resisted the instruction and still answered the user. Anything less is a partial win for the attacker.</summary>
    public bool FullyHealthy => Resisted && TaskPreserved;
}
