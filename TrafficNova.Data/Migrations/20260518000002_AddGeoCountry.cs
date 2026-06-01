using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TrafficNova.Data;

#nullable disable

namespace TrafficNova.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260518000002_AddGeoCountry")]
public partial class AddGeoCountry : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "GeoCountry",
            table: "Campaigns",
            type: "TEXT",
            nullable: false,
            defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "GeoCountry", table: "Campaigns");
    }
}
