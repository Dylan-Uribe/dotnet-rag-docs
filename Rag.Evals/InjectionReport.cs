using System.Text.Json;

namespace Rag.Evals;

public static class InjectionReport
{
    public static void Print(IReadOnlyList<InjectionOutcome> outcomes)
    {
        Console.WriteLine();
        Console.WriteLine($"{"id",-5} {"kind",-20} {"position",-9} {"obeyed",-7} {"answered",-9} verdict");
        Console.WriteLine(new string('-', 96));

        foreach (InjectionOutcome outcome in outcomes)
        {
            string verdict = outcome switch
            {
                { Obeyed: true } => "COMPROMISED",
                { TaskPreserved: false } => "disrupted",
                _ => "ok"
            };

            Console.WriteLine(
                $"{outcome.Attack.Id,-5} {outcome.Attack.Kind,-20} {outcome.Position,-9} " +
                $"{(outcome.Obeyed ? "yes" : "no"),-7} {(outcome.TaskPreserved ? "yes" : "no"),-9} {verdict}");
        }

        Console.WriteLine();
        Console.WriteLine("Rates");
        Console.WriteLine(new string('-', 96));
        Console.WriteLine(
            $"{"resistance",-24} {Rate(outcomes, o => o.Resisted):0.00}   " +
            $"{outcomes.Count(o => o.Resisted)}/{outcomes.Count}");
        Console.WriteLine(
            $"{"task preserved",-24} {Rate(outcomes, o => o.TaskPreserved):0.00}   " +
            $"{outcomes.Count(o => o.TaskPreserved)}/{outcomes.Count}");
        Console.WriteLine(
            $"{"resisted and answered",-24} {Rate(outcomes, o => o.FullyHealthy):0.00}   " +
            $"{outcomes.Count(o => o.FullyHealthy)}/{outcomes.Count}");

        Console.WriteLine();
        Console.WriteLine("By payload position");
        Console.WriteLine(new string('-', 96));

        foreach (IGrouping<InjectionPosition, InjectionOutcome> group in outcomes.GroupBy(o => o.Position))
        {
            List<InjectionOutcome> items = group.ToList();
            Console.WriteLine($"{group.Key,-24} resistance {Rate(items, o => o.Resisted):0.00}   {items.Count(o => o.Resisted)}/{items.Count}");
        }

        PrintDetail("Compromised", outcomes.Where(o => o.Obeyed).ToList());
        PrintDetail("Disrupted but not compromised", outcomes.Where(o => !o.Obeyed && !o.TaskPreserved).ToList());
    }

    private static void PrintDetail(string title, IReadOnlyList<InjectionOutcome> failures)
    {
        Console.WriteLine();

        if (failures.Count == 0)
        {
            Console.WriteLine($"{title}: none.");
            return;
        }

        Console.WriteLine($"{title} ({failures.Count})");
        Console.WriteLine(new string('-', 96));

        foreach (InjectionOutcome failure in failures)
        {
            Console.WriteLine($"{failure.Attack.Id} [{failure.Position}]  {failure.Attack.Kind}");
            Console.WriteLine($"      question: {failure.Attack.Question}");
            Console.WriteLine($"      answer  : {Truncate(failure.Answer, 200)}");
            Console.WriteLine($"      note    : {failure.Attack.Note}");
            Console.WriteLine();
        }
    }

    private static double Rate(IReadOnlyList<InjectionOutcome> outcomes, Func<InjectionOutcome, bool> predicate) =>
        outcomes.Count == 0 ? 0 : outcomes.Count(predicate) / (double)outcomes.Count;

    private static string Truncate(string text, int length) =>
        text.Length <= length ? text : string.Concat(text.AsSpan(0, length - 1), "…");

    public static async Task WriteJsonAsync(IReadOnlyList<InjectionOutcome> outcomes, string path)
    {
        var payload = new
        {
            runAtUtc = DateTime.UtcNow,
            attemptCount = outcomes.Count,
            resistance = Math.Round(Rate(outcomes, o => o.Resisted), 4),
            taskPreserved = Math.Round(Rate(outcomes, o => o.TaskPreserved), 4),
            byPosition = outcomes
                .GroupBy(o => o.Position)
                .ToDictionary(
                    group => group.Key.ToString(),
                    group => Math.Round(Rate(group.ToList(), o => o.Resisted), 4)),
            attempts = outcomes.Select(outcome => new
            {
                id = outcome.Attack.Id,
                kind = outcome.Attack.Kind,
                position = outcome.Position.ToString(),
                question = outcome.Attack.Question,
                obeyed = outcome.Obeyed,
                taskPreserved = outcome.TaskPreserved,
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
