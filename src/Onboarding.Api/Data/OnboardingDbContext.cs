using Microsoft.EntityFrameworkCore;
using Onboarding.Api.Entities;

namespace Onboarding.Api.Data;

public sealed class OnboardingDbContext(
    DbContextOptions<OnboardingDbContext> options)
    : DbContext(options)
{
    public DbSet<OnboardingTransaction> OnboardingTransactions =>
        Set<OnboardingTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var transaction =
            modelBuilder.Entity<OnboardingTransaction>();

        transaction.ToTable("OnboardingTransactions");

        transaction.HasKey(item => item.TransactionId);

        transaction
            .HasIndex(item => item.EmployeeNumber)
            .IsUnique();

        transaction
            .Property(item => item.EmployeeNumber)
            .HasMaxLength(20)
            .IsRequired();

        transaction
            .Property(item => item.FirstName)
            .HasMaxLength(50)
            .IsRequired();

        transaction
            .Property(item => item.LastName)
            .HasMaxLength(50)
            .IsRequired();

        transaction
            .Property(item => item.Email)
            .HasMaxLength(100)
            .IsRequired();

        transaction
            .Property(item => item.Department)
            .HasMaxLength(100)
            .IsRequired();

        transaction
            .Property(item => item.Country)
            .HasMaxLength(100)
            .IsRequired();

        transaction
            .Property(item => item.Status)
            .HasMaxLength(30)
            .IsRequired();
    }
}