using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoadSafety.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Users",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                // Officers already in the database predate roles. They take the
                // narrowest one, not an empty string, which would not parse.
                defaultValue: "Officer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Role",
                table: "Users");
        }
    }
}
