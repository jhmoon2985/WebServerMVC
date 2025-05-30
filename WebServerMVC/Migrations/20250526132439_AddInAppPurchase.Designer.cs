﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using WebServerMVC.Data;

#nullable disable

namespace WebServerMVC.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20250526132439_AddInAppPurchase")]
    partial class AddInAppPurchase
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "9.0.4")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("WebServerMVC.Models.Client", b =>
                {
                    b.Property<string>("ClientId")
                        .HasColumnType("text");

                    b.Property<DateTime>("ConnectedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("ConnectionId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Gender")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<bool>("IsMatched")
                        .HasColumnType("boolean");

                    b.Property<double>("Latitude")
                        .HasColumnType("double precision");

                    b.Property<double>("Longitude")
                        .HasColumnType("double precision");

                    b.Property<string>("MatchedWithClientId")
                        .HasColumnType("text");

                    b.Property<int>("MaxDistance")
                        .HasColumnType("integer");

                    b.Property<int>("Points")
                        .HasColumnType("integer");

                    b.Property<DateTime?>("PreferenceActiveUntil")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("PreferredGender")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("ClientId");

                    b.HasIndex("ConnectionId");

                    b.ToTable("Clients");
                });

            modelBuilder.Entity("WebServerMVC.Models.ClientMatch", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("text");

                    b.Property<string>("ChatGroupName")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("ClientId1")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("ClientId2")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<double>("Distance")
                        .HasColumnType("double precision");

                    b.Property<DateTime?>("EndedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTime>("MatchedAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("MatchedAt");

                    b.HasIndex("ClientId1", "ClientId2");

                    b.ToTable("Matches");
                });

            modelBuilder.Entity("WebServerMVC.Models.InAppPurchase", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("text");

                    b.Property<decimal>("Amount")
                        .HasColumnType("decimal(18,2)");

                    b.Property<string>("ClientId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Currency")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("ErrorMessage")
                        .HasColumnType("text");

                    b.Property<int>("Points")
                        .HasColumnType("integer");

                    b.Property<string>("ProductId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("PurchaseToken")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("PurchasedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Store")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("TransactionId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("VerificationData")
                        .HasColumnType("text");

                    b.Property<DateTime?>("VerifiedAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("ClientId");

                    b.HasIndex("PurchaseToken")
                        .IsUnique();

                    b.HasIndex("PurchasedAt");

                    b.HasIndex("Status");

                    b.ToTable("InAppPurchases");
                });
#pragma warning restore 612, 618
        }
    }
}
