using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TrafficNova.Data;

#nullable disable

namespace TrafficNova.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260518000005_AddResourceBlockMode")]
public partial class AddResourceBlockMode : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ResourceBlockMode",
            table: "Campaigns",
            type: "TEXT",
            nullable: false,
            defaultValue: "None");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ResourceBlockMode", table: "Campaigns");
    }
}
