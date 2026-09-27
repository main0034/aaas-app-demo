using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace App.Migrations
{
    /// <inheritdoc />
    public partial class NotebookSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "height",
                table: "items",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "width",
                table: "items",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "x",
                table: "items",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "y",
                table: "items",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "questions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    x = table.Column<double>(type: "double precision", nullable: false),
                    y = table.Column<double>(type: "double precision", nullable: false),
                    answer = table.Column<string>(type: "text", nullable: true),
                    error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    asked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    answered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_questions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "strokes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    points = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_strokes", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "questions");

            migrationBuilder.DropTable(
                name: "strokes");

            migrationBuilder.DropColumn(
                name: "height",
                table: "items");

            migrationBuilder.DropColumn(
                name: "width",
                table: "items");

            migrationBuilder.DropColumn(
                name: "x",
                table: "items");

            migrationBuilder.DropColumn(
                name: "y",
                table: "items");
        }
    }
}
