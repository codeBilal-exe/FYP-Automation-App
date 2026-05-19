using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FYP_AutomationSystem.Migrations
{
    public partial class AddAttendeeBasedEvaluationFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"ALTER TABLE ""Evaluations"" ADD COLUMN IF NOT EXISTS ""ItemId"" integer NOT NULL DEFAULT 0;
                  ALTER TABLE ""Evaluations"" ADD COLUMN IF NOT EXISTS ""ItemType"" character varying(32) NOT NULL DEFAULT '';
                  ALTER TABLE ""Evaluations"" ADD COLUMN IF NOT EXISTS ""EvaluatorRole"" character varying(32) NOT NULL DEFAULT '';
                  ALTER TABLE ""Evaluations"" ADD COLUMN IF NOT EXISTS ""GroupId"" integer NOT NULL DEFAULT 0;
                  ALTER TABLE ""Evaluations"" ADD COLUMN IF NOT EXISTS ""Marks"" numeric NOT NULL DEFAULT 0;
                  ALTER TABLE ""Evaluations"" ADD COLUMN IF NOT EXISTS ""Comment"" character varying(500);
                  ALTER TABLE ""Evaluations"" ADD COLUMN IF NOT EXISTS ""UpdatedAt"" timestamp with time zone;");

            migrationBuilder.Sql(
                @"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Evaluations_ItemId_ItemType_EvaluatorId""
                  ON ""Evaluations"" (""ItemId"", ""ItemType"", ""EvaluatorId"")
                  WHERE ""ItemType"" IN ('Milestone', 'Viva');");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"DROP INDEX IF EXISTS ""IX_Evaluations_ItemId_ItemType_EvaluatorId"";
                  ALTER TABLE ""Evaluations"" DROP COLUMN IF EXISTS ""UpdatedAt"";
                  ALTER TABLE ""Evaluations"" DROP COLUMN IF EXISTS ""Comment"";
                  ALTER TABLE ""Evaluations"" DROP COLUMN IF EXISTS ""Marks"";
                  ALTER TABLE ""Evaluations"" DROP COLUMN IF EXISTS ""GroupId"";
                  ALTER TABLE ""Evaluations"" DROP COLUMN IF EXISTS ""EvaluatorRole"";
                  ALTER TABLE ""Evaluations"" DROP COLUMN IF EXISTS ""ItemType"";
                  ALTER TABLE ""Evaluations"" DROP COLUMN IF EXISTS ""ItemId"";");
        }
    }
}
