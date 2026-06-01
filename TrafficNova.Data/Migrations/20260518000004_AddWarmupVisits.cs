using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TrafficNova.Data;

#nullable disable

namespace TrafficNova.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260518000004_AddWarmupVisits")]
public partial class AddWarmupVisits : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "WarmupVisits",
            table: "Campaigns",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "WarmupVisits", table: "Campaigns");
    }
}
