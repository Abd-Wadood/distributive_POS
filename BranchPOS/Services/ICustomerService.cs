using BranchPOS.DTOs;
using BranchPOS.Models;

namespace BranchPOS.Services;

public interface ICustomerService
{
    Task<Customer?> GetCustomerByPhoneAsync(string phone, CancellationToken cancellationToken = default);

    Task<Customer?> CreateOrUpdateCustomerAsync(CustomerDto dto, CancellationToken cancellationToken = default);
}
