using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace fypSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddSemesterAndAcademicSessionToProjectProposal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcademicSession",
                table: "ProjectProposals",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Semester",
                table: "ProjectProposals",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcademicSession",
                table: "ProjectProposals");

            migrationBuilder.DropColumn(
                name: "Semester",
                table: "ProjectProposals");
        }
    }
}
