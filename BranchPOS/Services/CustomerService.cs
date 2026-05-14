using BranchPOS.Data;
using BranchPOS.DTOs;
using BranchPOS.Models;
using Microsoft.EntityFrameworkCore;

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

        var branchId = dto.BranchId > 0 ? dto.BranchId : await _branchContextService.GetCurrentBranchIdAsync(cancellationToken);
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

    private static string NormalizePhone(string? phone) => (phone ?? string.Empty).Trim();
}
