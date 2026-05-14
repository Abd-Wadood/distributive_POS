using BranchPOS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BranchPOS.Controllers;

[Authorize(Roles = "Admin,Cashier")]
public class CustomersController : Controller
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    [HttpGet]
    public async Task<IActionResult> Lookup(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return Json(new { found = false });
        }

        var customer = await _customerService.GetCustomerByPhoneAsync(phone);
        if (customer is null)
        {
            return Json(new { found = false });
        }

        return Json(new
        {
            found = true,
            customer = new
            {
                customer.Id,
                customer.Name,
                customer.PhoneNumber,
                customer.Address
            }
        });
    }
}
