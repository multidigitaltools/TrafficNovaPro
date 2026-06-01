using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TrafficNova.Data;

#nullable disable

namespace TrafficNova.Data.Migrations;

[DbContext(typeof(AppDbContext))]
partial class AppDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder.HasAnnotation("ProductVersion", "9.0.0");

        modelBuilder.Entity("TrafficNova.Core.Models.Campaign", b =>
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER");
            b.Property<string>("Name").IsRequired().HasMaxLength(200).HasColumnType("TEXT");
            b.Property<string>("TargetUrlsJson").IsRequired().HasColumnType("TEXT");
            b.Property<string>("Status").IsRequired().HasColumnType("TEXT");
            b.Property<string>("ReferrerMode").IsRequired().HasColumnType("TEXT");
            b.Property<string>("UserAgentMode").IsRequired().HasColumnType("TEXT");
            b.Property<string>("DeviceType").IsRequired().HasColumnType("TEXT");
            b.Property<string>("ProxyRotation").IsRequired().HasColumnType("TEXT");
            b.Property<string>("GeoCountry").IsRequired().HasColumnType("TEXT");
            b.Property<string>("ResourceBlockMode").IsRequired().HasColumnType("TEXT");
            b.Property<DateTime>("CreatedAt").HasColumnType("TEXT");
            b.Property<DateTime>("UpdatedAt").HasColumnType("TEXT");
            b.HasKey("Id");
            b.ToTable("Campaigns");
        });

        modelBuilder.Entity("TrafficNova.Core.Models.ProxyEntry", b =>
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER");
            b.Property<string>("Host").IsRequired().HasMaxLength(253).HasColumnType("TEXT");
            b.Property<int>("Port").HasColumnType("INTEGER");
            b.Property<string>("Protocol").IsRequired().HasColumnType("TEXT");
            b.HasKey("Id");
            b.ToTable("ProxyEntries");
        });

        modelBuilder.Entity("TrafficNova.Core.Models.TrafficSession", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER");
            b.Property<int>("CampaignId").HasColumnType("INTEGER");
            b.Property<string>("TargetUrl").IsRequired().HasMaxLength(2000).HasColumnType("TEXT");
            b.Property<DateTime>("StartedAt").HasColumnType("TEXT");
            b.Property<bool>("Success").HasColumnType("INTEGER");
            b.HasKey("Id");
            b.HasIndex("CampaignId");
            b.HasIndex("StartedAt");
            b.ToTable("TrafficSessions");
        });

        modelBuilder.Entity("TrafficNova.Core.Models.ScheduledJob", b =>
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER");
            b.Property<int>("CampaignId").HasColumnType("INTEGER");
            b.Property<string>("CronExpression").IsRequired().HasColumnType("TEXT");
            b.Property<bool>("IsEnabled").HasColumnType("INTEGER");
            b.HasKey("Id");
            b.HasIndex("CampaignId");
            b.ToTable("ScheduledJobs");
            b.HasOne("TrafficNova.Core.Models.Campaign", "Campaign")
                .WithMany()
                .HasForeignKey("CampaignId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });
#pragma warning restore 612, 618
    }
}
