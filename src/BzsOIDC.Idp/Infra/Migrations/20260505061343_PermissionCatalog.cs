using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BzsOIDC.Idp.Infra.Migrations
{
    /// <inheritdoc />
    public partial class PermissionCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bzs_protected_resources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bzs_protected_resources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "bzs_permission_definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bzs_permission_definitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bzs_permission_definitions_bzs_protected_resources_Resource~",
                        column: x => x.ResourceId,
                        principalTable: "bzs_protected_resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bzs_permission_release_scopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bzs_permission_release_scopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bzs_permission_release_scopes_bzs_permission_definitions_Pe~",
                        column: x => x.PermissionDefinitionId,
                        principalTable: "bzs_permission_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bzs_permission_definitions_Name",
                table: "bzs_permission_definitions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bzs_permission_definitions_ResourceId_Name",
                table: "bzs_permission_definitions",
                columns: new[] { "ResourceId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bzs_permission_release_scopes_PermissionDefinitionId_Scope",
                table: "bzs_permission_release_scopes",
                columns: new[] { "PermissionDefinitionId", "Scope" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bzs_protected_resources_Key",
                table: "bzs_protected_resources",
                column: "Key",
                unique: true);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bzs_permission_release_scopes");

            migrationBuilder.DropTable(
                name: "bzs_permission_definitions");

            migrationBuilder.DropTable(
                name: "bzs_protected_resources");
        }
    }
}
