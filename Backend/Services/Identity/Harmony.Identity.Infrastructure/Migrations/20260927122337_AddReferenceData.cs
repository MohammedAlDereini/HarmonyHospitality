using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harmony.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReferenceData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Capabilities",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OwnerService = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    HasLimit = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Capabilities", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "Currencies",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    MinorUnits = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "Languages",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameNative = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IsRightToLeft = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Languages", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "PropertyGroupTypes",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IsExclusive = table.Column<bool>(type: "bit", nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyGroupTypes", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "PropertyTypes",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyTypes", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "TimeZones",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeZones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Countries",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Alpha3 = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DialCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    DefaultCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Countries", x => x.Code);
                    table.ForeignKey(
                        name: "FK_Countries_Currencies_DefaultCurrency",
                        column: x => x.DefaultCurrency,
                        principalTable: "Currencies",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CountrySubdivisions",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CountrySubdivisions", x => x.Code);
                    table.UniqueConstraint("AK_CountrySubdivisions_CountryCode_Code", x => new { x.CountryCode, x.Code });
                    table.ForeignKey(
                        name: "FK_CountrySubdivisions_Countries_CountryCode",
                        column: x => x.CountryCode,
                        principalTable: "Countries",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RegulatoryEnvironments",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OwnerService = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegulatoryEnvironments", x => x.Code);
                    table.ForeignKey(
                        name: "FK_RegulatoryEnvironments_Countries_CountryCode",
                        column: x => x.CountryCode,
                        principalTable: "Countries",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Cities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    SubdivisionCode = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: true),
                    NameEn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cities_Countries_CountryCode",
                        column: x => x.CountryCode,
                        principalTable: "Countries",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cities_CountrySubdivisions_CountryCode_SubdivisionCode",
                        columns: x => new { x.CountryCode, x.SubdivisionCode },
                        principalTable: "CountrySubdivisions",
                        principalColumns: new[] { "CountryCode", "Code" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cities_TimeZones_TimeZoneId",
                        column: x => x.TimeZoneId,
                        principalTable: "TimeZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Capabilities_CreatedDate",
                table: "Capabilities",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Cities_CountryCode_NameEn",
                table: "Cities",
                columns: new[] { "CountryCode", "NameEn" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cities_CountryCode_SubdivisionCode",
                table: "Cities",
                columns: new[] { "CountryCode", "SubdivisionCode" });

            migrationBuilder.CreateIndex(
                name: "IX_Cities_CreatedDate",
                table: "Cities",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Cities_TimeZoneId",
                table: "Cities",
                column: "TimeZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_Countries_Alpha3",
                table: "Countries",
                column: "Alpha3",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Countries_CreatedDate",
                table: "Countries",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Countries_DefaultCurrency",
                table: "Countries",
                column: "DefaultCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_CountrySubdivisions_CreatedDate",
                table: "CountrySubdivisions",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_CreatedDate",
                table: "Currencies",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Languages_CreatedDate",
                table: "Languages",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyGroupTypes_CreatedDate",
                table: "PropertyGroupTypes",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyTypes_CreatedDate",
                table: "PropertyTypes",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_RegulatoryEnvironments_CountryCode_Kind",
                table: "RegulatoryEnvironments",
                columns: new[] { "CountryCode", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_RegulatoryEnvironments_CreatedDate",
                table: "RegulatoryEnvironments",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_TimeZones_CreatedDate",
                table: "TimeZones",
                column: "CreatedDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Capabilities");

            migrationBuilder.DropTable(
                name: "Cities");

            migrationBuilder.DropTable(
                name: "Languages");

            migrationBuilder.DropTable(
                name: "PropertyGroupTypes");

            migrationBuilder.DropTable(
                name: "PropertyTypes");

            migrationBuilder.DropTable(
                name: "RegulatoryEnvironments");

            migrationBuilder.DropTable(
                name: "CountrySubdivisions");

            migrationBuilder.DropTable(
                name: "TimeZones");

            migrationBuilder.DropTable(
                name: "Countries");

            migrationBuilder.DropTable(
                name: "Currencies");
        }
    }
}
