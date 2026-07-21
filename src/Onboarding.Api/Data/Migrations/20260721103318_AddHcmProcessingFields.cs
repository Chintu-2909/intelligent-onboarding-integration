using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Onboarding.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHcmProcessingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ErrorCode",
                table: "OnboardingTransactions",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "OnboardingTransactions",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HcmEmployeeId",
                table: "OnboardingTransactions",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRetryable",
                table: "OnboardingTransactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAttemptAtUtc",
                table: "OnboardingTransactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "OnboardingTransactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ErrorCode",
                table: "OnboardingTransactions");

            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "OnboardingTransactions");

            migrationBuilder.DropColumn(
                name: "HcmEmployeeId",
                table: "OnboardingTransactions");

            migrationBuilder.DropColumn(
                name: "IsRetryable",
                table: "OnboardingTransactions");

            migrationBuilder.DropColumn(
                name: "LastAttemptAtUtc",
                table: "OnboardingTransactions");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "OnboardingTransactions");
        }
    }
}
