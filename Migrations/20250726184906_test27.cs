using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace fypSystem.Migrations
{
    /// <inheritdoc />
    public partial class test27 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Evaluator1Recommendation",
                table: "ProjectProposals",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Evaluator2Recommendation",
                table: "ProjectProposals",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Evaluator1Recommendation",
                table: "ProjectProposals");

            migrationBuilder.DropColumn(
                name: "Evaluator2Recommendation",
                table: "ProjectProposals");
        }
    }
}
