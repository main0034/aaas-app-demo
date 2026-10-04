using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace App.Migrations
{
    /// <inheritdoc />
    public partial class AddProjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "project_id",
                table: "items",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_lower = table.Column<string>(type: "text", nullable: true, computedColumnSql: "lower(name)", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_items_project_id",
                table: "items",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_projects_name_lower",
                table: "projects",
                column: "name_lower",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_items_projects_project_id",
                table: "items",
                column: "project_id",
                principalTable: "projects",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_items_projects_project_id",
                table: "items");

            migrationBuilder.DropTable(
                name: "projects");

            migrationBuilder.DropIndex(
                name: "IX_items_project_id",
                table: "items");

            migrationBuilder.DropColumn(
                name: "project_id",
                table: "items");
        }
    }
}
