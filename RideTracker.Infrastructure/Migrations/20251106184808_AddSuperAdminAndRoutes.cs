using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RideTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSuperAdminAndRoutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_route_points_order_index",
                table: "route_points");

            migrationBuilder.AddColumn<bool>(
                name: "is_super_admin",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "route_id",
                table: "route_points",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "route_id",
                table: "challenges",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "routes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    total_distance_km = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_routes", x => x.id);
                });

            // Create a default route for existing route_points
            migrationBuilder.Sql(@"
                INSERT INTO routes (name, description, total_distance_km, created_at, is_active)
                VALUES ('Sri Lanka Coastal Route', 'Default coastal route around Sri Lanka', 1585.0, NOW(), true);
            ");

            // Update existing route_points to reference the default route
            migrationBuilder.Sql(@"
                UPDATE route_points 
                SET route_id = (SELECT id FROM routes WHERE name = 'Sri Lanka Coastal Route' LIMIT 1)
                WHERE route_id IS NULL;
            ");

            // Now make route_id NOT NULL
            migrationBuilder.AlterColumn<int>(
                name: "route_id",
                table: "route_points",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_route_points_route_id_order_index",
                table: "route_points",
                columns: new[] { "route_id", "order_index" });

            migrationBuilder.CreateIndex(
                name: "IX_challenges_route_id",
                table: "challenges",
                column: "route_id");

            migrationBuilder.AddForeignKey(
                name: "FK_challenges_routes_route_id",
                table: "challenges",
                column: "route_id",
                principalTable: "routes",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_route_points_routes_route_id",
                table: "route_points",
                column: "route_id",
                principalTable: "routes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_challenges_routes_route_id",
                table: "challenges");

            migrationBuilder.DropForeignKey(
                name: "FK_route_points_routes_route_id",
                table: "route_points");

            migrationBuilder.DropTable(
                name: "routes");

            migrationBuilder.DropIndex(
                name: "IX_route_points_route_id_order_index",
                table: "route_points");

            migrationBuilder.DropIndex(
                name: "IX_challenges_route_id",
                table: "challenges");

            migrationBuilder.DropColumn(
                name: "is_super_admin",
                table: "users");

            migrationBuilder.DropColumn(
                name: "route_id",
                table: "route_points");

            migrationBuilder.DropColumn(
                name: "route_id",
                table: "challenges");

            migrationBuilder.CreateIndex(
                name: "IX_route_points_order_index",
                table: "route_points",
                column: "order_index");
        }
    }
}
