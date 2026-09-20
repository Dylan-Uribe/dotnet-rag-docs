using System.Text.Json;

namespace Rag.Evals;

public static class SweepReport
{
    private static readonly int[] Cutoffs = [1, 3, 5, 10];

    public static void Print(IReadOnlyList<SweepResult> results)
    {
        Console.WriteLine();
        Console.WriteLine(
            $"{"chunk",6} {"overlap",8} {"chunks",7} {"ctx tok",8} " +
            $"{"r@1",6} {"r@3",6} {"r@5",6} {"r@10",6} {"MRR",6}  misses");
        Console.WriteLine(new string('-', 96));

        foreach (SweepResult result in results)
        {
            string misses = string.Join(",", result.MissedQuestionIds);

            Console.WriteLine(
                $"{result.ChunkSizeTokens,6} {result.ChunkOverlapTokens,8} {result.ChunkCount,7} " +
                $"{result.AverageContextTokens,8:0} " +
                $"{RetrievalMetrics.RecallAt(result.Outcomes, 1),6:0.00} " +
                $"{RetrievalMetrics.RecallAt(result.Outcomes, 3),6:0.00} " +
                $"{RetrievalMetrics.RecallAt(result.Outcomes, 5),6:0.00} " +
                $"{RetrievalMetrics.RecallAt(result.Outcomes, 10),6:0.00} " +
                $"{RetrievalMetrics.MeanReciprocalRank(result.Outcomes),6:0.00}  {misses}");
        }

        Console.WriteLine();
        Console.WriteLine(
            $"One question is worth {1.0 / results[0].Outcomes.Count:0.00} of recall, " +
            "so differences below two questions are noise.");
    }

    public static async Task WriteJsonAsync(IReadOnlyList<SweepResult> results, string path)
    {
        var payload = new
        {
            runAtUtc = DateTime.UtcNow,
            questionCount = results[0].Outcomes.Count,
            configurations = results.Select(result => new
            {
                chunkSizeTokens = result.ChunkSizeTokens,
                chunkOverlapTokens = result.ChunkOverlapTokens,
                chunkCount = result.ChunkCount,
                averageContextTokens = Math.Round(result.AverageContextTokens, 1),
                metrics = Cutoffs.ToDictionary(
                        k => $"recall@{k}",
                        k => Math.Round(RetrievalMetrics.RecallAt(result.Outcomes, k), 4))
                    .Append(new KeyValuePair<string, double>(
                        "mrr",
                        Math.Round(RetrievalMetrics.MeanReciprocalRank(result.Outcomes), 4)))
                    .ToDictionary(pair => pair.Key, pair => pair.Value),
                missedQuestionIds = result.MissedQuestionIds,
                ranks = result.Outcomes.ToDictionary(
                    outcome => outcome.Question.Id,
                    outcome => outcome.FirstHitRank)
            })
        };

        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine();
        Console.WriteLine($"Results written to {path}");
    }
}
