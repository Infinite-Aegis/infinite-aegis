using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class InfiniteAegisEntityStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "character_wallet",
                columns: table => new
                {
                    profile_id = table.Column<int>(type: "INTEGER", nullable: false),
                    balance = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_wallet", x => x.profile_id);
                    table.ForeignKey(
                        name: "FK_character_wallet_profile_profile_id",
                        column: x => x.profile_id,
                        principalTable: "profile",
                        principalColumn: "profile_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "persistent_character_entity",
                columns: table => new
                {
                    persistent_character_entity_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    profile_id = table.Column<int>(type: "INTEGER", nullable: false),
                    offer_id = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    prototype_id = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    purchase_request_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    entity_state = table.Column<string>(type: "TEXT", nullable: false),
                    revision = table.Column<long>(type: "INTEGER", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_persistent_character_entity", x => x.persistent_character_entity_id);
                    table.ForeignKey(
                        name: "FK_persistent_character_entity_profile_profile_id",
                        column: x => x.profile_id,
                        principalTable: "profile",
                        principalColumn: "profile_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_persistent_character_entity_profile_id_offer_id",
                table: "persistent_character_entity",
                columns: new[] { "profile_id", "offer_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_persistent_character_entity_purchase_request_id",
                table: "persistent_character_entity",
                column: "purchase_request_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "character_wallet");

            migrationBuilder.DropTable(
                name: "persistent_character_entity");
        }
    }
}
