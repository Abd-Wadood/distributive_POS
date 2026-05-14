using BranchPOS.Models;

namespace BranchPOS.ViewModels;

public class OrderCreateViewModel
{
    public List<ProductOrderLineViewModel> Products { get; set; } = new();

    public static OrderCreateViewModel FromProducts(IEnumerable<Product> products) =>
        new()
        {
            Products = products.Select(x => new ProductOrderLineViewModel
            {
                ProductId = x.Id,
                Name = x.Name,
                Price = x.Price
            }).ToList()
        };
}

public class ProductOrderLineViewModel
{
    public int ProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int Quantity { get; set; }
}
