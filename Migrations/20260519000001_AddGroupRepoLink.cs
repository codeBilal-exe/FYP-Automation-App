using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FYP_AutomationSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupRepoLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RepoLink",
                table: "Groups",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RepoLink",
                table: "Groups");
        }
    }
}
