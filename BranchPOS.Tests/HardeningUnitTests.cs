using BranchPOS.Controllers;
using BranchPOS.Data;
using BranchPOS.Models;
using BranchPOS.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BranchPOS.Tests;

public class HardeningUnitTests
{
    [Theory]
    [InlineData("Gram", "20kg Bag", 20000)]
    [InlineData("Gram", "Kg", 1000)]
    [InlineData("ML", "5L Can", 5000)]
    [InlineData("Piece", "Crate", 12)]
    public void Inventory_unit_catalog_accepts_valid_combinations(string baseUnit, string purchaseUnit, decimal conversion)
    {
        Assert.Equal(conversion, InventoryUnitCatalog.ValidateAndNormalize(baseUnit, purchaseUnit, conversion));
    }

    [Theory]
    [InlineData("Piece", "20kg Bag", 20000)]
    [InlineData("Gram", "Crate", 12)]
    [InlineData("ML", "Box", 12)]
    public void Inventory_unit_catalog_rejects_invalid_combinations(string baseUnit, string purchaseUnit, decimal conversion)
    {
        Assert.Throws<BranchPOS.Exceptions.PosValidationException>(() =>
            InventoryUnitCatalog.ValidateAndNormalize(baseUnit, purchaseUnit, conversion));
    }

    [Fact]
    public void Inventory_unit_catalog_enforces_fixed_and_custom_conversion_rules()
    {
        Assert.Equal(20000m, InventoryUnitCatalog.FindOption("Gram", "20kg Bag")?.DefaultConversionFactorToBase);
        Assert.Throws<BranchPOS.Exceptions.PosValidationException>(() =>
            InventoryUnitCatalog.ValidateAndNormalize("Gram", "20kg Bag", 19999m));
        Assert.Throws<BranchPOS.Exceptions.PosValidationException>(() =>
            InventoryUnitCatalog.ValidateAndNormalize("Piece", "Crate", null));
        Assert.Throws<BranchPOS.Exceptions.PosValidationException>(() =>
            InventoryUnitCatalog.ValidateAndNormalize("Piece", "Crate", 0m));
        Assert.Equal(12m, InventoryUnitCatalog.ValidateAndNormalize("Piece", "Crate", 12m));
    }

    [Fact]
    public void Inventory_unit_options_are_scoped_by_base_unit_for_ui_resets()
    {
        var gramOptions = InventoryUnitCatalog.GetOptionsForBaseUnit("Gram");
        var pieceOptions = InventoryUnitCatalog.GetOptionsForBaseUnit("Piece");

        Assert.Contains(gramOptions, x => x.DisplayName == "20kg Bag");
        Assert.DoesNotContain(pieceOptions, x => x.DisplayName == "20kg Bag");
        Assert.Contains(pieceOptions, x => x.DisplayName == "Crate");
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData(" main-01 ", "MAIN-01")]
    public void Terminal_code_normalization_is_null_safe(string? input, string expected)
    {
        Assert.Equal(expected, TerminalContextService.NormalizeCode(input));
    }

    [Theory]
    [InlineData("03001234567")]
    [InlineData("+923001234567")]
    [InlineData("923001234567")]
    [InlineData("0300-1234567")]
    [InlineData("(0300) 1234567")]
    public void Pakistani_phone_formats_normalize_to_single_customer_key(string input)
    {
        Assert.Equal("+923001234567", CustomerService.NormalizePhone(input));
    }

    [Fact]
    public void Branch_field_cleanup_normalizes_all_user_entered_fields()
    {
        var branch = new Branch
        {
            BranchCode = " main ",
            Name = " Main Branch ",
            Address = "  ",
            Phone = "  123  "
        };

        BranchesController.CleanBranchFields(branch);

        Assert.Equal("MAIN", branch.BranchCode);
        Assert.Equal("Main Branch", branch.Name);
        Assert.Null(branch.Address);
        Assert.Equal("123", branch.Phone);
    }

    [Fact]
    public void Terminal_cookie_tampering_is_rejected_before_database_lookup()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"{TerminalContextService.TerminalIdentityCookieName}=tampered";
        var service = CreateTerminalContextService(context);

        Assert.Null(service.GetTerminalCodeFromRequest());
    }

    [Fact]
    public void Terminal_token_hash_uses_one_way_hash_and_verifies_raw_token()
    {
        var token = TerminalContextService.GenerateTerminalToken();
        var hash = TerminalContextService.HashTerminalToken(token);

        Assert.NotEqual(token, hash);
        Assert.True(TerminalContextService.VerifyTerminalToken(token, hash));
        Assert.False(TerminalContextService.VerifyTerminalToken($"{token}x", hash));
    }

    private static TerminalContextService CreateTerminalContextService(HttpContext httpContext)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .Options;

        return new TerminalContextService(
            new AppDbContext(options),
            new HttpContextAccessor { HttpContext = httpContext },
            DataProtectionProvider.Create("BranchPOS.Tests"),
            Options.Create(new PosOperationalOptions()),
            new TestEnvironment(),
            new MemoryCache(Options.Create(new MemoryCacheOptions())));
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";

        public string ApplicationName { get; set; } = "BranchPOS.Tests";

        public string WebRootPath { get; set; } = string.Empty;

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
