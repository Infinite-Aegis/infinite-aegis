using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Content.Server.Database;

public abstract partial class ServerDbContext
{
    public DbSet<CharacterWallet> CharacterWallet => Set<CharacterWallet>();
    public DbSet<PersistentCharacterEntity> PersistentCharacterEntity => Set<PersistentCharacterEntity>();

    private static void ConfigureInfiniteAegis(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CharacterWallet>()
            .HasOne(x => x.Profile)
            .WithOne()
            .HasForeignKey<CharacterWallet>(x => x.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PersistentCharacterEntity>()
            .HasOne(x => x.Profile)
            .WithMany()
            .HasForeignKey(x => x.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PersistentCharacterEntity>()
            .HasIndex(x => new { x.ProfileId, x.OfferId })
            .IsUnique();

        modelBuilder.Entity<PersistentCharacterEntity>()
            .HasIndex(x => x.PurchaseRequestId)
            .IsUnique();
    }
}

public sealed class CharacterWallet
{
    [Key]
    public int ProfileId { get; set; }

    public long Balance { get; set; }

    public Profile Profile { get; set; } = null!;
}

public sealed class PersistentCharacterEntity
{
    [Key]
    public Guid Id { get; set; }

    public int ProfileId { get; set; }
    public Profile Profile { get; set; } = null!;

    [MaxLength(128)]
    public string OfferId { get; set; } = string.Empty;

    [MaxLength(128)]
    public string PrototypeId { get; set; } = string.Empty;

    public Guid PurchaseRequestId { get; set; }

    public string EntityState { get; set; } = string.Empty;

    public long Revision { get; set; }

    public DateTime UpdatedAt { get; set; }
}
