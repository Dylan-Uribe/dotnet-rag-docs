using System.Text.Json;

namespace Rag.Evals;

public static class EvalReport
{
    private static readonly int[] Cutoffs = [1, 3, 5, 10];

    public static void Print(IReadOnlyList<QuestionOutcome> outcomes, int topK)
    {
        Console.WriteLine();
        Console.WriteLine($"Questions: {outcomes.Count}   TopK: {topK}");
        Console.WriteLine();

        Console.WriteLine($"{"id",-5} {"kind",-16} {"rank",5}  question");
        Console.WriteLine(new string('-', 96));

        foreach (QuestionOutcome outcome in outcomes)
        {
            string rank = outcome.FirstHitRank == 0 ? "MISS" : outcome.FirstHitRank.ToString();
            string question = Truncate(outcome.Question.Question, 62);

            Console.WriteLine($"{outcome.Question.Id,-5} {outcome.Question.Kind,-16} {rank,5}  {question}");
        }

        Console.WriteLine();
        PrintMetrics("OVERALL", outcomes);

        Console.WriteLine();
        Console.WriteLine("By question kind");
        Console.WriteLine(new string('-', 96));

        foreach (IGrouping<string, QuestionOutcome> group in outcomes
            .GroupBy(outcome => outcome.Question.Kind)
            .OrderBy(group => group.Key))
        {
            PrintMetrics($"{group.Key} ({group.Count()})", group.ToList());
        }

        Console.WriteLine();
        PrintMisses(outcomes);
    }

    private static void PrintMetrics(string label, IReadOnlyList<QuestionOutcome> outcomes)
    {
        string recalls = string.Join(
            "   ",
            Cutoffs.Select(k => $"recall@{k} {RetrievalMetrics.RecallAt(outcomes, k):0.00}"));

        Console.WriteLine($"{label,-22} {recalls}   MRR {RetrievalMetrics.MeanReciprocalRank(outcomes):0.00}");
    }

    private static void PrintMisses(IReadOnlyList<QuestionOutcome> outcomes)
    {
        List<QuestionOutcome> misses = outcomes.Where(outcome => outcome.FirstHitRank == 0).ToList();

        if (misses.Count == 0)
        {
            Console.WriteLine("No misses.");
            return;
        }

        Console.WriteLine($"Misses ({misses.Count})");
        Console.WriteLine(new string('-', 96));

        foreach (QuestionOutcome miss in misses)
        {
            Console.WriteLine($"{miss.Question.Id}  {miss.Question.Question}");
            Console.WriteLine($"      expected page(s): {string.Join(", ", miss.Question.ExpectedPages)}");
            Console.WriteLine($"      retrieved pages : {string.Join(", ", miss.RetrievedPages)}");
            Console.WriteLine($"      best distance   : {(miss.Distances.Count == 0 ? "-" : miss.Distances[0].ToString("0.000"))}");
        }
    }

    public static async Task WriteJsonAsync(IReadOnlyList<QuestionOutcome> outcomes, int topK, string path)
    {
        var payload = new
        {
            runAtUtc = DateTime.UtcNow,
            topK,
            questionCount = outcomes.Count,
            overall = Summarise(outcomes),
            byKind = outcomes
                .GroupBy(outcome => outcome.Question.Kind)
                .OrderBy(group => group.Key)
                .ToDictionary(group => group.Key, group => Summarise(group.ToList())),
            questions = outcomes.Select(outcome => new
            {
                id = outcome.Question.Id,
                kind = outcome.Question.Kind,
                question = outcome.Question.Question,
                expectedPages = outcome.Question.ExpectedPages,
                retrievedPages = outcome.RetrievedPages,
                firstHitRank = outcome.FirstHitRank,
                distances = outcome.Distances.Select(distance => Math.Round(distance, 4))
            })
        };

        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine();
        Console.WriteLine($"Results written to {path}");
    }

    private static Dictionary<string, double> Summarise(IReadOnlyList<QuestionOutcome> outcomes)
    {
        var summary = Cutoffs.ToDictionary(
            k => $"recall@{k}",
            k => Math.Round(RetrievalMetrics.RecallAt(outcomes, k), 4));

        summary["mrr"] = Math.Round(RetrievalMetrics.MeanReciprocalRank(outcomes), 4);

        return summary;
    }

    private static string Truncate(string text, int length) =>
        text.Length <= length ? text : string.Concat(text.AsSpan(0, length - 1), "…");
}
