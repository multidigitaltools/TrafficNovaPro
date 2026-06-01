using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TrafficNova.Data;

#nullable disable

namespace TrafficNova.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260518000003_AddProxyChain")]
public partial class AddProxyChain : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "UseChain",
            table: "ProxyEntries",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "ChainProxyId",
            table: "ProxyEntries",
            type: "INTEGER",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "UseChain", table: "ProxyEntries");
        migrationBuilder.DropColumn(name: "ChainProxyId", table: "ProxyEntries");
    }
}
