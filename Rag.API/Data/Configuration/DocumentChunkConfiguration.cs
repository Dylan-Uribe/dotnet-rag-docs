using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rag.API.Domain;

namespace Rag.API.Data.Configuration;

public sealed class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> builder)
    {
        builder
            .HasKey(dch => dch.Id);

        builder
            .Property(dch => dch.TextContent)
            .IsRequired();

        builder
            .Property(dch => dch.Embedding)
            .HasColumnType("vector(1536)");

        builder
            .Property(dch => dch.PageNumber)
            .IsRequired();

        builder.HasIndex(c => c.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops");
    }
}
