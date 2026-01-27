using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.DAL.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixDepartmentCodeCollation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop ALL indexes on DepartmentCode
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Departments_DepartmentCode')
                    DROP INDEX IX_Departments_DepartmentCode ON Departments;

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Departments_DepartmentCode_Unique')
                    DROP INDEX IX_Departments_DepartmentCode_Unique ON Departments;");

            // Drop CHECK constraint
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.check_constraints 
                    WHERE name = 'CK_Department_DepartmentCode_Format')
                ALTER TABLE Departments
                    DROP CONSTRAINT CK_Department_DepartmentCode_Format;");

            // Alter column collation
            migrationBuilder.AlterColumn<string>(
                name: "DepartmentCode",
                table: "Departments",
                type: "nvarchar(50)",
                nullable: false,
                collation: "Latin1_General_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)"
            );

            // Re-add CHECK constraint
            migrationBuilder.Sql(@"
                ALTER TABLE Departments
                ADD CONSTRAINT CK_Department_DepartmentCode_Format
                CHECK (DepartmentCode LIKE '[A-Z][A-Z][A-Z]_[0-9][0-9][0-9]');");

            // Re-add UNIQUE index (case-insensitive via collation)
            migrationBuilder.CreateIndex(
                name: "IX_Departments_DepartmentCode_Unique",
                table: "Departments",
                column: "DepartmentCode",
                unique: true,
                filter: "[IsDeleted] = 0"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Departments_DepartmentCode_Unique",
                table: "Departments");
            migrationBuilder.Sql(@"
                ALTER TABLE Departments
                DROP CONSTRAINT CK_Department_DepartmentCode_Format");
            migrationBuilder.AlterColumn<string>(
                name: "DepartmentCode",
                table: "Departments",
                type: "nvarchar(50)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldCollation: "Latin1_General_CI_AS");
            migrationBuilder.Sql(@"
                ALTER TABLE Departments
                ADD CONSTRAINT CK_Department_DepartmentCode_Format
                CHECK (DepartmentCode LIKE '[A-Z][A-Z][A-Z]_[0-9][0-9][0-9]')");
        }
    }
}
