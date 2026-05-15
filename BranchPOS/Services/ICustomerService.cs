using BranchPOS.DTOs;
using BranchPOS.Models;

namespace BranchPOS.Services;

public interface ICustomerService
{
    Task<Customer?> GetCustomerByPhoneAsync(string phone, CancellationToken cancellationToken = default);

    // Adds or updates a Customer in the current DbContext only. The caller owns SaveChanges/transaction.
    Task<Customer?> CreateOrUpdateCustomerAsync(CustomerDto dto, CancellationToken cancellationToken = default);
}
