using System.Text.Json;

namespace Rag.Evals;

public static class AbstentionReport
{
    public static void Print(IReadOnlyList<AbstentionOutcome> outcomes)
    {
        List<AbstentionOutcome> unanswerable = outcomes.Where(o => !o.Question.Answerable).ToList();
        List<AbstentionOutcome> answerable = outcomes.Where(o => o.Question.Answerable).ToList();

        Console.WriteLine();
        Console.WriteLine($"{"id",-5} {"kind",-14} {"expected",-9} {"got",-9}  verdict");
        Console.WriteLine(new string('-', 96));

        foreach (AbstentionOutcome outcome in outcomes)
        {
            string expected = outcome.Question.Answerable ? "answer" : "abstain";
            string got = outcome.Response switch
            {
                ResponseKind.TemplateRefusal => "abstain",
                ResponseKind.SoftRefusal => "abstain*",
                _ => "answer"
            };
            string verdict = outcome switch
            {
                { IsHallucination: true } => "HALLUCINATION",
                { IsOverRefusal: true } => "OVER-REFUSAL",
                _ => "ok"
            };

            Console.WriteLine(
                $"{outcome.Question.Id,-5} {outcome.Question.Kind,-14} {expected,-9} {got,-9}  {verdict}");
        }

        Console.WriteLine();
        Console.WriteLine("Rates");
        Console.WriteLine(new string('-', 96));
        Console.WriteLine(
            $"{"abstention (unanswerable)",-30} {Rate(unanswerable, o => o.Abstained):0.00}   " +
            $"{unanswerable.Count(o => o.Abstained)}/{unanswerable.Count}");
        Console.WriteLine(
            $"{"answer rate (answerable)",-30} {Rate(answerable, o => !o.Abstained):0.00}   " +
            $"{answerable.Count(o => !o.Abstained)}/{answerable.Count}");
        Console.WriteLine(
            $"{"overall correct",-30} {Rate(outcomes, o => o.IsCorrect):0.00}   " +
            $"{outcomes.Count(o => o.IsCorrect)}/{outcomes.Count}");

        List<AbstentionOutcome> refusals = outcomes.Where(o => o.Abstained).ToList();

        Console.WriteLine(
            $"{"template compliance",-30} {Rate(refusals, o => o.Response == ResponseKind.TemplateRefusal):0.00}   " +
            $"{refusals.Count(o => o.Response == ResponseKind.TemplateRefusal)}/{refusals.Count}" +
            "   (refusals using the exact wording; abstain* marks the rest)");

        Console.WriteLine();
        Console.WriteLine("Abstention by kind (unanswerable only)");
        Console.WriteLine(new string('-', 96));

        foreach (IGrouping<string, AbstentionOutcome> group in unanswerable
            .GroupBy(o => o.Question.Kind)
            .OrderBy(group => group.Key))
        {
            List<AbstentionOutcome> items = group.ToList();
            Console.WriteLine(
                $"{group.Key,-30} {Rate(items, o => o.Abstained):0.00}   " +
                $"{items.Count(o => o.Abstained)}/{items.Count}");
        }

        PrintFailures("Hallucinations", outcomes.Where(o => o.IsHallucination).ToList());
        PrintFailures("Over-refusals", outcomes.Where(o => o.IsOverRefusal).ToList());
    }

    private static void PrintFailures(string title, IReadOnlyList<AbstentionOutcome> failures)
    {
        Console.WriteLine();

        if (failures.Count == 0)
        {
            Console.WriteLine($"{title}: none.");
            return;
        }

        Console.WriteLine($"{title} ({failures.Count})");
        Console.WriteLine(new string('-', 96));

        foreach (AbstentionOutcome failure in failures)
        {
            Console.WriteLine($"{failure.Question.Id}  {failure.Question.Question}");
            Console.WriteLine($"      answer: {failure.Answer}");
            Console.WriteLine($"      why it is not in the corpus: {failure.Question.Note}");
            Console.WriteLine();
        }
    }

    private static double Rate(IReadOnlyList<AbstentionOutcome> outcomes, Func<AbstentionOutcome, bool> predicate) =>
        outcomes.Count == 0 ? 0 : outcomes.Count(predicate) / (double)outcomes.Count;

    public static async Task WriteJsonAsync(IReadOnlyList<AbstentionOutcome> outcomes, string path)
    {
        List<AbstentionOutcome> unanswerable = outcomes.Where(o => !o.Question.Answerable).ToList();
        List<AbstentionOutcome> answerable = outcomes.Where(o => o.Question.Answerable).ToList();

        var payload = new
        {
            runAtUtc = DateTime.UtcNow,
            questionCount = outcomes.Count,
            abstentionRateOnUnanswerable = Math.Round(Rate(unanswerable, o => o.Abstained), 4),
            answerRateOnAnswerable = Math.Round(Rate(answerable, o => !o.Abstained), 4),
            overallCorrect = Math.Round(Rate(outcomes, o => o.IsCorrect), 4),
            templateCompliance = Math.Round(
                Rate(outcomes.Where(o => o.Abstained).ToList(), o => o.Response == ResponseKind.TemplateRefusal), 4),
            byKind = unanswerable
                .GroupBy(o => o.Question.Kind)
                .OrderBy(group => group.Key)
                .ToDictionary(
                    group => group.Key,
                    group => Math.Round(Rate(group.ToList(), o => o.Abstained), 4)),
            questions = outcomes.Select(outcome => new
            {
                id = outcome.Question.Id,
                kind = outcome.Question.Kind,
                question = outcome.Question.Question,
                answerable = outcome.Question.Answerable,
                abstained = outcome.Abstained,
                response = outcome.Response.ToString(),
                hallucination = outcome.IsHallucination,
                overRefusal = outcome.IsOverRefusal,
                citationCount = outcome.CitationCount,
                answer = outcome.Answer
            })
        };

        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine();
        Console.WriteLine($"Results written to {path}");
    }
}
