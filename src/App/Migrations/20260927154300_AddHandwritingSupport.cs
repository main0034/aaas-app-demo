using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Migrations
{
    /// <inheritdoc />
    public partial class AddHandwritingSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "read_at",
                table: "strokes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "handwritten",
                table: "questions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "height",
                table: "questions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "width",
                table: "questions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "handwritten",
                table: "items",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "read_at",
                table: "strokes");

            migrationBuilder.DropColumn(
                name: "handwritten",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "height",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "width",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "handwritten",
                table: "items");
        }
    }
}
