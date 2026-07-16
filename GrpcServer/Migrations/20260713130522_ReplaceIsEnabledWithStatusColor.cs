using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrpcServer.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceIsEnabledWithStatusColor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsEnabled",
                table: "Products");

            migrationBuilder.AddColumn<string>(
                name: "StatusColor",
                table: "Products",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "green");

            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "ProductRules",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "orange");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StatusColor",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Color",
                table: "ProductRules");

            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }
    }
}
