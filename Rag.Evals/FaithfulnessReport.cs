using System.Text.Json;

namespace Rag.Evals;

public static class FaithfulnessReport
{
    public static void PrintCalibration(IReadOnlyList<CalibrationOutcome> outcomes)
    {
        int faithfulAgreements = outcomes.Count(outcome => outcome.FaithfulAgrees);

        List<CalibrationOutcome> scoredCorrect = outcomes.Where(outcome => outcome.CorrectAgrees is not null).ToList();
        int correctAgreements = scoredCorrect.Count(outcome => outcome.CorrectAgrees == true);

        Console.WriteLine();
        Console.WriteLine("Judge calibration against hand-labelled cases");
        Console.WriteLine(new string('-', 96));
        Console.WriteLine($"faithful agreement   {faithfulAgreements}/{outcomes.Count}");
        Console.WriteLine($"correct agreement    {correctAgreements}/{scoredCorrect.Count}   (arguable cases excluded)");

        List<CalibrationOutcome> disagreements = outcomes
            .Where(outcome => !outcome.FaithfulAgrees || outcome.CorrectAgrees == false)
            .ToList();

        if (disagreements.Count == 0)
        {
            Console.WriteLine();
            Console.WriteLine("The judge agreed on every case.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"Disagreements ({disagreements.Count})");
        Console.WriteLine(new string('-', 96));

        foreach (CalibrationOutcome outcome in disagreements)
        {
            Console.WriteLine($"{outcome.Case.Id}  {outcome.Case.Question}");
            Console.WriteLine($"      answer      : {outcome.Case.Answer}");
            Console.WriteLine(
                $"      expected    : faithful={outcome.Case.ExpectedFaithful} correct={outcome.Case.ExpectedCorrect?.ToString() ?? "n/a"}");
            Console.WriteLine(
                $"      judge said  : faithful={outcome.Verdict.Faithful} correct={outcome.Verdict.Correct}");
            Console.WriteLine($"      its reason  : {outcome.Verdict.Reason}");
            Console.WriteLine($"      why labelled: {outcome.Case.Note}");
            Console.WriteLine();
        }
    }

    public static void Print(IReadOnlyList<FaithfulnessOutcome> outcomes)
    {
        Console.WriteLine();
        Console.WriteLine($"{"id",-5} {"faithful",9} {"correct",8}  reason");
        Console.WriteLine(new string('-', 96));

        foreach (FaithfulnessOutcome outcome in outcomes)
        {
            Console.WriteLine(
                $"{outcome.Question.Id,-5} {Mark(outcome.Verdict.Faithful),9} {Mark(outcome.Verdict.Correct),8}  " +
                Truncate(outcome.Verdict.Reason, 60));
        }

        int faithful = outcomes.Count(outcome => outcome.Verdict.Faithful);
        int correct = outcomes.Count(outcome => outcome.Verdict.Correct);
        int both = outcomes.Count(outcome => outcome.Verdict is { Faithful: true, Correct: true });

        Console.WriteLine();
        Console.WriteLine("Rates");
        Console.WriteLine(new string('-', 96));
        Console.WriteLine($"{"faithfulness",-22} {faithful / (double)outcomes.Count:0.00}   {faithful}/{outcomes.Count}");
        Console.WriteLine($"{"correctness",-22} {correct / (double)outcomes.Count:0.00}   {correct}/{outcomes.Count}");
        Console.WriteLine($"{"both",-22} {both / (double)outcomes.Count:0.00}   {both}/{outcomes.Count}");

        Console.WriteLine();
        Console.WriteLine("Cross-tab");
        Console.WriteLine(new string('-', 96));
        Console.WriteLine($"{"",-22} {"correct",9} {"wrong",9}");
        Console.WriteLine($"{"faithful",-22} {Count(outcomes, true, true),9} {Count(outcomes, true, false),9}");
        Console.WriteLine($"{"unfaithful",-22} {Count(outcomes, false, true),9} {Count(outcomes, false, false),9}");

        List<FaithfulnessOutcome> failures = outcomes
            .Where(outcome => !outcome.Verdict.Faithful || !outcome.Verdict.Correct)
            .ToList();

        if (failures.Count == 0)
        {
            Console.WriteLine();
            Console.WriteLine("No failures.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"Failures ({failures.Count})");
        Console.WriteLine(new string('-', 96));

        foreach (FaithfulnessOutcome failure in failures)
        {
            Console.WriteLine($"{failure.Question.Id}  {failure.Question.Question}");
            Console.WriteLine($"      answer   : {Truncate(failure.Answer, 160)}");
            Console.WriteLine($"      reference: {Truncate(failure.Question.Answer, 160)}");
            Console.WriteLine(
                $"      verdict  : faithful={failure.Verdict.Faithful} correct={failure.Verdict.Correct} — {failure.Verdict.Reason}");
            Console.WriteLine();
        }
    }

    private static int Count(IReadOnlyList<FaithfulnessOutcome> outcomes, bool faithful, bool correct) =>
        outcomes.Count(outcome => outcome.Verdict.Faithful == faithful && outcome.Verdict.Correct == correct);

    private static string Mark(bool value) => value ? "yes" : "NO";

    private static string Truncate(string text, int length) =>
        text.Length <= length ? text : string.Concat(text.AsSpan(0, length - 1), "…");

    public static async Task WriteJsonAsync(
        IReadOnlyList<CalibrationOutcome> calibration,
        IReadOnlyList<FaithfulnessOutcome> outcomes,
        string judgeModel,
        string path)
    {
        var payload = new
        {
            runAtUtc = DateTime.UtcNow,
            judgeModel,
            calibration = new
            {
                cases = calibration.Count,
                faithfulAgreement = calibration.Count(outcome => outcome.FaithfulAgrees),
                results = calibration.Select(outcome => new
                {
                    id = outcome.Case.Id,
                    expectedFaithful = outcome.Case.ExpectedFaithful,
                    judgedFaithful = outcome.Verdict.Faithful,
                    expectedCorrect = outcome.Case.ExpectedCorrect,
                    judgedCorrect = outcome.Verdict.Correct,
                    agrees = outcome.FaithfulAgrees,
                    reason = outcome.Verdict.Reason
                })
            },
            questionCount = outcomes.Count,
            faithfulness = Math.Round(outcomes.Count(o => o.Verdict.Faithful) / (double)outcomes.Count, 4),
            correctness = Math.Round(outcomes.Count(o => o.Verdict.Correct) / (double)outcomes.Count, 4),
            questions = outcomes.Select(outcome => new
            {
                id = outcome.Question.Id,
                kind = outcome.Question.Kind,
                question = outcome.Question.Question,
                answer = outcome.Answer,
                reference = outcome.Question.Answer,
                faithful = outcome.Verdict.Faithful,
                correct = outcome.Verdict.Correct,
                reason = outcome.Verdict.Reason
            })
        };

        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine();
        Console.WriteLine($"Results written to {path}");
    }
}
