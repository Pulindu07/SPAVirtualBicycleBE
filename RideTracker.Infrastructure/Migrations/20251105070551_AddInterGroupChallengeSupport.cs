using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RideTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInterGroupChallengeSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_challenges_groups_group_id",
                table: "challenges");

            migrationBuilder.DropIndex(
                name: "IX_challenges_group_id",
                table: "challenges");

            migrationBuilder.DropColumn(
                name: "group_id",
                table: "challenges");

            migrationBuilder.CreateTable(
                name: "challenge_groups",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    challenge_id = table.Column<int>(type: "integer", nullable: false),
                    group_id = table.Column<int>(type: "integer", nullable: false),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_challenge_groups", x => x.id);
                    table.ForeignKey(
                        name: "FK_challenge_groups_challenges_challenge_id",
                        column: x => x.challenge_id,
                        principalTable: "challenges",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_challenge_groups_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_challenge_groups_challenge_id_group_id",
                table: "challenge_groups",
                columns: new[] { "challenge_id", "group_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_challenge_groups_group_id",
                table: "challenge_groups",
                column: "group_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "challenge_groups");

            migrationBuilder.AddColumn<int>(
                name: "group_id",
                table: "challenges",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_challenges_group_id",
                table: "challenges",
                column: "group_id");

            migrationBuilder.AddForeignKey(
                name: "FK_challenges_groups_group_id",
                table: "challenges",
                column: "group_id",
                principalTable: "groups",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
