using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harmony.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PropertiesAndSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Properties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ExternalIdentifier = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    NameEn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PropertyTypeCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    SubdivisionCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    TimeZoneId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AddressLine = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PostalCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    DefaultCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    DefaultLanguageCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    TotalRooms = table.Column<int>(type: "int", nullable: false),
                    LegalEnvironmentCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TaxEnvironmentCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AccountingEnvironmentCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AccommodationEnvironmentCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OpensOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ClosesOn = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ShortName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UniqueName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    BusinessId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    BusinessRegistrationCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    BusinessPremiseId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    FiscalYearBeginMonth = table.Column<int>(type: "int", nullable: true),
                    FiscalYearBeginDay = table.Column<int>(type: "int", nullable: true),
                    DefaultCulture = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    StarRating = table.Column<int>(type: "int", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Telephone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    WebsiteUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ProductChampionEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LegalOwnerName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Properties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Properties_Cities_CityId",
                        column: x => x.CityId,
                        principalTable: "Cities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Properties_Countries_CountryCode",
                        column: x => x.CountryCode,
                        principalTable: "Countries",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Properties_CountrySubdivisions_SubdivisionCode",
                        column: x => x.SubdivisionCode,
                        principalTable: "CountrySubdivisions",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Properties_Currencies_DefaultCurrency",
                        column: x => x.DefaultCurrency,
                        principalTable: "Currencies",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Properties_Languages_DefaultLanguageCode",
                        column: x => x.DefaultLanguageCode,
                        principalTable: "Languages",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Properties_PropertyTypes_PropertyTypeCode",
                        column: x => x.PropertyTypeCode,
                        principalTable: "PropertyTypes",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Properties_RegulatoryEnvironments_AccommodationEnvironmentCode",
                        column: x => x.AccommodationEnvironmentCode,
                        principalTable: "RegulatoryEnvironments",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Properties_RegulatoryEnvironments_AccountingEnvironmentCode",
                        column: x => x.AccountingEnvironmentCode,
                        principalTable: "RegulatoryEnvironments",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Properties_RegulatoryEnvironments_LegalEnvironmentCode",
                        column: x => x.LegalEnvironmentCode,
                        principalTable: "RegulatoryEnvironments",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Properties_RegulatoryEnvironments_TaxEnvironmentCode",
                        column: x => x.TaxEnvironmentCode,
                        principalTable: "RegulatoryEnvironments",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Properties_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Properties_TimeZones_TimeZoneId",
                        column: x => x.TimeZoneId,
                        principalTable: "TimeZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenantSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModuleCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    DisabledReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RenewsAutomatically = table.Column<bool>(type: "bit", nullable: false),
                    Rooms = table.Column<int>(type: "int", nullable: true),
                    Terminals = table.Column<int>(type: "int", nullable: true),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantSubscriptions_Modules_ModuleCode",
                        column: x => x.ModuleCode,
                        principalTable: "Modules",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenantSubscriptions_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenantSubscriptions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Properties_AccommodationEnvironmentCode",
                table: "Properties",
                column: "AccommodationEnvironmentCode");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_AccountingEnvironmentCode",
                table: "Properties",
                column: "AccountingEnvironmentCode");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_CityId",
                table: "Properties",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_CountryCode",
                table: "Properties",
                column: "CountryCode");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_CreatedDate",
                table: "Properties",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_DefaultCurrency",
                table: "Properties",
                column: "DefaultCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_DefaultLanguageCode",
                table: "Properties",
                column: "DefaultLanguageCode");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_LegalEnvironmentCode",
                table: "Properties",
                column: "LegalEnvironmentCode");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_PropertyTypeCode",
                table: "Properties",
                column: "PropertyTypeCode");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_SubdivisionCode",
                table: "Properties",
                column: "SubdivisionCode");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_TaxEnvironmentCode",
                table: "Properties",
                column: "TaxEnvironmentCode");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_TenantId",
                table: "Properties",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_TimeZoneId",
                table: "Properties",
                column: "TimeZoneId");

            migrationBuilder.CreateIndex(
                name: "UX_Properties_TenantId_Code",
                table: "Properties",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantSubscriptions_CreatedDate",
                table: "TenantSubscriptions",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_TenantSubscriptions_ModuleCode",
                table: "TenantSubscriptions",
                column: "ModuleCode");

            migrationBuilder.CreateIndex(
                name: "IX_TenantSubscriptions_PropertyId",
                table: "TenantSubscriptions",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "UX_TenantSubscriptions_Line",
                table: "TenantSubscriptions",
                columns: new[] { "TenantId", "PropertyId", "ModuleCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantSubscriptions");

            migrationBuilder.DropTable(
                name: "Properties");
        }
    }
}
