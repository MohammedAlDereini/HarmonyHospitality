using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harmony.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CapabilitiesToModules : Migration
    {
        /// <summary>
        /// Capabilities becomes Modules and every module says on which floor it is sold. A rename, not a drop: the rows
        /// stay (EF scaffolds a drop-and-create for a renamed entity, so this is written by hand). The existing rows are
        /// hotel modules, so Level starts as Property.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(name: "Capabilities", newName: "Modules");
            migrationBuilder.RenameIndex(name: "IX_Capabilities_CreatedDate", table: "Modules", newName: "IX_Modules_CreatedDate");
            migrationBuilder.DropPrimaryKey(name: "PK_Capabilities", table: "Modules");
            migrationBuilder.AddPrimaryKey(name: "PK_Modules", table: "Modules", column: "Code");

            migrationBuilder.AddColumn<string>(
                name: "Level",
                table: "Modules",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Property");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Level", table: "Modules");
            migrationBuilder.DropPrimaryKey(name: "PK_Modules", table: "Modules");
            migrationBuilder.AddPrimaryKey(name: "PK_Capabilities", table: "Modules", column: "Code");
            migrationBuilder.RenameIndex(name: "IX_Modules_CreatedDate", table: "Modules", newName: "IX_Capabilities_CreatedDate");
            migrationBuilder.RenameTable(name: "Modules", newName: "Capabilities");
        }
    }
}
