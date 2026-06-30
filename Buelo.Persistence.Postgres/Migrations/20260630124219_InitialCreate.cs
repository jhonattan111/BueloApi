using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Buelo.Persistence.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Definitions",
                columns: table => new
                {
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Definitions", x => new { x.Kind, x.Name });
                });

            migrationBuilder.CreateTable(
                name: "GlobalArtefacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Extension = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    TagsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalArtefacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RenderEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReportName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Engine = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Format = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ByteCount = table.Column<int>(type: "integer", nullable: false),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RenderEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Json = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Templates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TemplateVersions",
                columns: table => new
                {
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Json = table.Column<string>(type: "text", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateVersions", x => new { x.TemplateId, x.Version });
                });

            migrationBuilder.CreateTable(
                name: "WorkspaceItems",
                columns: table => new
                {
                    Path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    IsFolder = table.Column<bool>(type: "boolean", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    LastModifiedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceItems", x => x.Path);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GlobalArtefacts_Name_Extension",
                table: "GlobalArtefacts",
                columns: new[] { "Name", "Extension" });

            migrationBuilder.CreateIndex(
                name: "IX_RenderEvents_CreatedAt",
                table: "RenderEvents",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Templates_UpdatedAt",
                table: "Templates",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceItems_IsFolder",
                table: "WorkspaceItems",
                column: "IsFolder");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Definitions");

            migrationBuilder.DropTable(
                name: "GlobalArtefacts");

            migrationBuilder.DropTable(
                name: "RenderEvents");

            migrationBuilder.DropTable(
                name: "Templates");

            migrationBuilder.DropTable(
                name: "TemplateVersions");

            migrationBuilder.DropTable(
                name: "WorkspaceItems");
        }
    }
}
