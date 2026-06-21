using BranchPOS.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace BranchPOS.Tests;

public class AdminRoleSeparationTests
{
    [Theory]
    [InlineData(typeof(OrdersController), "Cashier")]
    [InlineData(typeof(InventoryItemsController), "StockManager")]
    [InlineData(typeof(InventoryReportsController), "StockManager")]
    [InlineData(typeof(KitchenRequestsController), "StockManager")]
    [InlineData(typeof(RecipesController), "StockManager")]
    [InlineData(typeof(PurchasesController), "StockManager")]
    [InlineData(typeof(ProductsController), "StockManager")]
    public void Operational_controllers_do_not_authorize_admin(Type controllerType, string expectedRole)
    {
        if (controllerType == typeof(OrdersController))
        {
            var createRoles = GetActionRoles<OrdersController>(nameof(OrdersController.Create));
            var indexRoles = GetActionRoles<OrdersController>(nameof(OrdersController.Index));
            Assert.Contains("Cashier", createRoles);
            Assert.DoesNotContain("Admin", createRoles);
            Assert.Contains("Admin", indexRoles);
            return;
        }

        var roles = controllerType
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .SelectMany(x => (x.Roles ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToList();

        Assert.Contains(expectedRole, roles);
        if (expectedRole == "Cashier")
        {
            Assert.DoesNotContain("Admin", roles);
        }
    }

    [Theory]
    [InlineData(nameof(SessionsController.Index))]
    [InlineData(nameof(SessionsController.Start))]
    [InlineData(nameof(SessionsController.Continue))]
    [InlineData(nameof(SessionsController.End))]
    [InlineData(nameof(SessionsController.Heartbeat))]
    public void Operational_session_actions_do_not_authorize_admin(string actionName)
    {
        var action = typeof(SessionsController).GetMethods().Single(x => x.Name == actionName);
        var roles = action
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .SelectMany(x => (x.Roles ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToList();

        Assert.Contains("Cashier", roles);
        Assert.Contains("StockManager", roles);
        Assert.DoesNotContain("Admin", roles);
    }

    [Theory]
    [InlineData(typeof(UsersController))]
    [InlineData(typeof(BranchesController))]
    [InlineData(typeof(CategoriesController))]
    [InlineData(typeof(TerminalsController))]
    public void Management_controllers_remain_admin_only(Type controllerType)
    {
        var roles = controllerType
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .SelectMany(x => (x.Roles ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToList();

        Assert.Equal(["Admin"], roles);
    }

    private static List<string> GetActionRoles<TController>(string actionName) =>
        typeof(TController).GetMethods()
            .Single(x => x.Name == actionName)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .SelectMany(x => (x.Roles ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToList();
}
