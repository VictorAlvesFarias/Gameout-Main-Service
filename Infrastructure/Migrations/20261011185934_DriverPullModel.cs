using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class DriverPullModel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoValidateSync",
                table: "AppFile");

            migrationBuilder.DropColumn(
                name: "Observer",
                table: "AppFile");

            migrationBuilder.DropColumn(
                name: "Path",
                table: "AppFile");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastUsed",
                table: "UserApiKeys",
                type: "timestamp without time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AddColumn<bool>(
                name: "Completed",
                table: "AppStoredFiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Versions created before this migration were finished uploads.
            migrationBuilder.Sql("UPDATE \"AppStoredFiles\" SET \"Completed\" = true;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Completed",
                table: "AppStoredFiles");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastUsed",
                table: "UserApiKeys",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoValidateSync",
                table: "AppFile",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Observer",
                table: "AppFile",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Path",
                table: "AppFile",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
