using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RoadSafety.Web.Migrations
{
    /// <inheritdoc />
    public partial class RolesAsRows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Order matters here. The scaffolded version dropped Users.Role
            // before the Roles table existed, which would have thrown away
            // every officer's role, and defaulted RoleId to 0, which no role
            // has. Roles are created and seeded first, existing officers are
            // carried across by name, and only then is the old column dropped.

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SeesEveryBranch = table.Column<bool>(type: "INTEGER", nullable: false),
                    CanManageOfficers = table.Column<bool>(type: "INTEGER", nullable: false),
                    CanAssignRoles = table.Column<bool>(type: "INTEGER", nullable: false),
                    CanManageRoles = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsBuiltIn = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "CanAssignRoles", "CanManageOfficers", "CanManageRoles", "Description", "IsBuiltIn", "Name", "SeesEveryBranch" },
                values: new object[,]
                {
                    { 1, false, false, false, "Reads their own station's records. Cannot add or change officers.", true, "Officer", false },
                    { 2, true, true, false, "Adds, edits and removes the officers posted to their own station.", true, "Station administrator", false },
                    { 3, true, true, true, "Manages every station, and the roles themselves.", true, "System administrator", true }
                });

            // Everyone lands on Officer unless the old column says otherwise.
            migrationBuilder.AddColumn<int>(
                name: "RoleId",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql(@"
                UPDATE Users
                SET RoleId = CASE Role
                    WHEN 'SystemAdministrator'  THEN 3
                    WHEN 'StationAdministrator' THEN 2
                    ELSE 1
                END;");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId",
                table: "Users",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Roles_RoleId",
                table: "Users",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Users",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "Officer");

            migrationBuilder.Sql(@"
                UPDATE Users
                SET Role = CASE RoleId
                    WHEN 3 THEN 'SystemAdministrator'
                    WHEN 2 THEN 'StationAdministrator'
                    ELSE 'Officer'
                END;");

            migrationBuilder.DropForeignKey(name: "FK_Users_Roles_RoleId", table: "Users");
            migrationBuilder.DropIndex(name: "IX_Users_RoleId", table: "Users");
            migrationBuilder.DropColumn(name: "RoleId", table: "Users");
            migrationBuilder.DropTable(name: "Roles");
        }
    }
}
