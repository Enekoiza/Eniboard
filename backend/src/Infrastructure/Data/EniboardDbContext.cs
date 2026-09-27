using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class EniboardDbContext(DbContextOptions<EniboardDbContext> options)
    : IdentityDbContext<ApplicationUser>(options), IEniboardDbContext
{
    public DbSet<AppEntity> Apps => Set<AppEntity>();

    public DbSet<Board> Boards => Set<Board>();

    public DbSet<BoardColumn> BoardColumns => Set<BoardColumn>();

    public DbSet<Card> Cards => Set<Card>();

    public async Task<IEniboardTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await Database.BeginTransactionAsync(cancellationToken);
        return new EfEniboardTransaction(transaction);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AppEntity>(entity =>
        {
            entity.ToTable("Apps");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Name).IsRequired().HasMaxLength(200);
            entity.Property(a => a.Color).IsRequired().HasMaxLength(32);
            entity.Property(a => a.RepoUrl).HasMaxLength(500);

            entity.HasOne(a => a.Board)
                .WithOne(b => b.App)
                .HasForeignKey<Board>(b => b.AppId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Board>(entity =>
        {
            entity.ToTable("Boards");
            entity.HasKey(b => b.Id);
        });

        builder.Entity<BoardColumn>(entity =>
        {
            entity.ToTable("BoardColumns");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(100);

            entity.HasOne(c => c.Board)
                .WithMany(b => b.Columns)
                .HasForeignKey(c => c.BoardId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Card>(entity =>
        {
            entity.ToTable("Cards");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Title).IsRequired().HasMaxLength(200);
            entity.Property(c => c.Description).HasMaxLength(4000);
            entity.Property(c => c.LinkedBranch).HasMaxLength(250);

            entity.Property(c => c.CardType)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(c => c.Priority)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.HasOne(c => c.Board)
                .WithMany()
                .HasForeignKey(c => c.BoardId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(c => c.Column)
                .WithMany(col => col.Cards)
                .HasForeignKey(c => c.ColumnId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}

file sealed class EfEniboardTransaction(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction) : IEniboardTransaction
{
    public Task CommitAsync(CancellationToken cancellationToken = default) => transaction.CommitAsync(cancellationToken);

    public Task RollbackAsync(CancellationToken cancellationToken = default) => transaction.RollbackAsync(cancellationToken);

    public ValueTask DisposeAsync() => transaction.DisposeAsync();
}
