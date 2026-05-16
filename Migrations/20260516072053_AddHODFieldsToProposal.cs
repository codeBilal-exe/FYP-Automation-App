using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FYP_AutomationSystem.Migrations
{
    public partial class AddHODFieldsToProposal : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ApprovedByCoordinator",
                table: "Proposals",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ApprovedByHOD",
                table: "Proposals",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CoordinatorApprovedAt",
                table: "Proposals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HODFeedback",
                table: "Proposals",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "HODApprovedAt",
                table: "Proposals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "HODRejectedAt",
                table: "Proposals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SentAt",
                table: "Notifications",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<string>(
                name: "Semester",
                table: "Groups",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Department",
                table: "Groups",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "FinalGrade",
                table: "Groups",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LetterGrade",
                table: "Groups",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFinalGradeConfirmed",
                table: "Groups",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ApprovedByCoordinator", table: "Proposals");
            migrationBuilder.DropColumn(name: "ApprovedByHOD", table: "Proposals");
            migrationBuilder.DropColumn(name: "CoordinatorApprovedAt", table: "Proposals");
            migrationBuilder.DropColumn(name: "HODFeedback", table: "Proposals");
            migrationBuilder.DropColumn(name: "HODApprovedAt", table: "Proposals");
            migrationBuilder.DropColumn(name: "HODRejectedAt", table: "Proposals");
            migrationBuilder.DropColumn(name: "SentAt", table: "Notifications");
            migrationBuilder.DropColumn(name: "Semester", table: "Groups");
            migrationBuilder.DropColumn(name: "Department", table: "Groups");
            migrationBuilder.DropColumn(name: "FinalGrade", table: "Groups");
            migrationBuilder.DropColumn(name: "LetterGrade", table: "Groups");
            migrationBuilder.DropColumn(name: "IsFinalGradeConfirmed", table: "Groups");
        }
    }
}
