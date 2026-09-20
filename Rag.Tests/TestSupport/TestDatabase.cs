using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Pgvector;
using Rag.API.Data;
using Rag.API.Domain;

namespace Rag.Tests.TestSupport;

/// <summary>
/// Teaches the in-memory provider to store <see cref="Vector"/>, which only the
/// Npgsql provider knows how to map. Production mapping is untouched.
/// </summary>
internal sealed class VectorAsTextCustomizer(ModelCustomizerDependencies dependencies)
    : ModelCustomizer(dependencies)
{
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        modelBuilder.Entity<DocumentChunk>()
            .Property(chunk => chunk.Embedding)
            .HasConversion(
                vector => string.Join(",", vector.ToArray().Select(v => v.ToString(CultureInfo.InvariantCulture))),
                text => new Vector(text.Split(",", StringSplitOptions.None)
                    .Select(v => float.Parse(v, CultureInfo.InvariantCulture))
                    .ToArray()));
    }
}

internal static class TestDatabase
{
    /// <summary>Each call gets its own isolated store, so tests never share state.</summary>
    public static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ReplaceService<IModelCustomizer, VectorAsTextCustomizer>()
            .Options;

        return new ApplicationDbContext(options);
    }
}
