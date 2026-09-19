using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rag.API.Domain;

namespace Rag.API.Data.Configuration;

public sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder
            .HasKey(d => d.Id);

        builder
            .Property(d => d.Name)
            .HasMaxLength(255)
            .IsRequired();

        builder
            .HasMany(d => d.DocumentChunks)
            .WithOne(dch => dch.Document)
            .HasForeignKey(dch => dch.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
