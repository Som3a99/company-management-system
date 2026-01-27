using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.DAL.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailUniqueConstrain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Employees_Email_Unique",
                table: "Employees",
                column: "Email",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Employees_Email_Unique",
                table: "Employees");
        }
    }
}
