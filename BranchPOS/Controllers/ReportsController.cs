using System.Collections;
using BranchPOS.Data;
using BranchPOS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Controllers;

[Authorize(Roles = "Admin")]
public class ReportsController : Controller
{
    private static readonly HashSet<string> HiddenColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash",
        "SecurityStamp",
        "ConcurrencyStamp",
        "AuthenticatorKey"
    };

    private readonly AppDbContext _context;

    public ReportsController(AppDbContext context)
    {
        _context = context;
    }

    public IActionResult Index()
    {
        var model = new ReportingViewModel();
        var dbSetMethod = typeof(DbContext).GetMethods()
            .Single(x => x.Name == nameof(DbContext.Set) && x.IsGenericMethodDefinition && x.GetParameters().Length == 0);

        foreach (var entityType in _context.Model.GetEntityTypes().OrderBy(x => x.GetTableName()).ThenBy(x => x.ClrType.Name))
        {
            var primaryKeyProperties = entityType.FindPrimaryKey()?.Properties.Select(x => x.Name).ToHashSet() ?? new HashSet<string>();
            var properties = entityType.GetProperties()
                .Where(x => !x.IsShadowProperty() && !HiddenColumns.Contains(x.Name))
                .OrderByDescending(x => primaryKeyProperties.Contains(x.Name))
                .ThenBy(x => x.Name)
                .ToList();

            if (properties.Count == 0)
            {
                continue;
            }

            var set = (IEnumerable?)dbSetMethod.MakeGenericMethod(entityType.ClrType).Invoke(_context, null);
            var rows = set?.Cast<object>().ToList() ?? new List<object>();

            model.Tables.Add(new ReportTableViewModel
            {
                Name = entityType.GetTableName() ?? entityType.ClrType.Name,
                RowCount = rows.Count,
                Columns = properties.Select(x => x.Name).ToList(),
                Rows = rows.Select(row => properties.Select(property =>
                {
                    var value = property.PropertyInfo?.GetValue(row);
                    return FormatValue(value);
                }).ToList()).ToList()
            });
        }

        return View(model);
    }

    private static string FormatValue(object? value) =>
        value switch
        {
            null => "",
            DateTime dateTime => dateTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz"),
            decimal number => number.ToString("0.###"),
            bool flag => flag ? "Yes" : "No",
            _ => value.ToString() ?? ""
        };
}
