// WebServerMVC/Migrations/[날짜]_AddPointsAndPreferences.cs
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebServerMVC.Migrations
{
    /// <inheritdoc />
    public partial class AddPointsAndPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Points 컬럼 추가 (기본값 0)
            migrationBuilder.AddColumn<int>(
                name: "Points",
                table: "Clients",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // PreferenceActiveUntil 컬럼 추가 (null 허용)
            migrationBuilder.AddColumn<DateTime>(
                name: "PreferenceActiveUntil",
                table: "Clients",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 롤백 시 컬럼 제거
            migrationBuilder.DropColumn(
                name: "PreferenceActiveUntil",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "Points",
                table: "Clients");
        }
    }
}
