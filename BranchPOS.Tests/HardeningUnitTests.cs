using BranchPOS.Controllers;
using BranchPOS.Data;
using BranchPOS.Models;
using BranchPOS.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace BranchPOS.Tests;

public class HardeningUnitTests
{
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
            new TestEnvironment());
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
