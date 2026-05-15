using BranchPOS.Data;
using BranchPOS.DTOs;
using BranchPOS.Exceptions;
using BranchPOS.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace BranchPOS.Services;

public class CustomerService : ICustomerService
{
    private readonly AppDbContext _context;
    private readonly IBranchContextService _branchContextService;

    public CustomerService(AppDbContext context, IBranchContextService branchContextService)
    {
        _context = context;
        _branchContextService = branchContextService;
    }

    public Task<Customer?> GetCustomerByPhoneAsync(string phone, CancellationToken cancellationToken = default)
    {
        return GetCustomerByPhoneInternalAsync(phone, cancellationToken);
    }

    private async Task<Customer?> GetCustomerByPhoneInternalAsync(string phone, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizePhone(phone);
        var branchId = await _branchContextService.GetCurrentBranchIdAsync(cancellationToken);
        return await _context.Customers.FirstOrDefaultAsync(x => x.BranchId == branchId && x.PhoneNumber == normalized, cancellationToken);
    }

    public async Task<Customer?> CreateOrUpdateCustomerAsync(CustomerDto dto, CancellationToken cancellationToken = default)
    {
        var phone = NormalizePhone(dto.PhoneNumber);
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var branchId = await _branchContextService.GetCurrentBranchIdAsync(cancellationToken);
        var customer = await _context.Customers.FirstOrDefaultAsync(x => x.BranchId == branchId && x.PhoneNumber == phone, cancellationToken);
        if (customer is null)
        {
            customer = new Customer { BranchId = branchId, PhoneNumber = phone };
            _context.Customers.Add(customer);
        }

        customer.Name = string.IsNullOrWhiteSpace(dto.Name) ? phone : dto.Name.Trim();
        customer.Address = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim();
        return customer;
    }

    public static string NormalizePhone(string? phone)
    {
        var cleaned = Regex.Replace(phone ?? string.Empty, @"[\s\-\(\)]", "").Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return string.Empty;
        }

        if (cleaned.StartsWith("00", StringComparison.Ordinal))
        {
            cleaned = $"+{cleaned[2..]}";
        }

        if (cleaned.StartsWith("+92", StringComparison.Ordinal) && cleaned.Length == 13)
        {
            return cleaned;
        }

        if (cleaned.StartsWith("92", StringComparison.Ordinal) && cleaned.Length == 12)
        {
            return $"+{cleaned}";
        }

        if (cleaned.StartsWith("0", StringComparison.Ordinal) && cleaned.Length == 11)
        {
            return $"+92{cleaned[1..]}";
        }

        return cleaned;
    }
}
