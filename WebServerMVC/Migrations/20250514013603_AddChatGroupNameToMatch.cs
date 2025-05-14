using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebServerMVC.Migrations
{
    /// <inheritdoc />
    public partial class AddChatGroupNameToMatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChatGroupName",
                table: "Matches",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChatGroupName",
                table: "Matches");
        }
    }
}
