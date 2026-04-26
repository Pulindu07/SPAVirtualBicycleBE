using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RideTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStravaWebhookEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "strava_webhook_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    subscription_id = table.Column<long>(type: "bigint", nullable: false),
                    object_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    object_id = table.Column<long>(type: "bigint", nullable: false),
                    aspect_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    owner_id = table.Column<long>(type: "bigint", nullable: false),
                    event_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    payload_json = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_strava_webhook_events", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_strava_webhook_events_dedup",
                table: "strava_webhook_events",
                columns: new[] { "object_type", "object_id", "aspect_type", "event_time" });

            migrationBuilder.CreateIndex(
                name: "ix_strava_webhook_events_owner_id",
                table: "strava_webhook_events",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_strava_webhook_events_status",
                table: "strava_webhook_events",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "strava_webhook_events");
        }
    }
}
