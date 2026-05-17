using System.Collections;
using System.Linq.Expressions;
using BranchPOS.Data;
using BranchPOS.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BranchPOS.Services;

public class ReportService : IReportService
{
    private static readonly HashSet<string> HiddenColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash",
        "SecurityStamp",
        "ConcurrencyStamp",
        "AuthenticatorKey"
    };

    private readonly AppDbContext _context;
    private readonly SecurityRateLimitOptions _limits;

    public ReportService(AppDbContext context, IOptions<SecurityRateLimitOptions> limits)
    {
        _context = context;
        _limits = limits.Value;
    }

    public async Task<ReportingViewModel> BuildReportAsync(
        int page,
        int? pageSize,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize ?? _limits.DefaultReportPageSize, 1, _limits.MaxReportPageSize);
        var model = new ReportingViewModel
        {
            Page = page,
            PageSize = safePageSize,
            From = from,
            To = to
        };

        var loadMethod = typeof(ReportService)
            .GetMethod(nameof(LoadTableAsync), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Report loader was not found.");

        foreach (var entityType in _context.Model.GetEntityTypes().OrderBy(x => x.GetTableName()).ThenBy(x => x.ClrType.Name))
        {
            var task = (Task<ReportTableViewModel?>)loadMethod
                .MakeGenericMethod(entityType.ClrType)
                .Invoke(this, [entityType, page, safePageSize, from, to, cancellationToken])!;
            var table = await task;
            if (table is not null)
            {
                model.Tables.Add(table);
            }
        }

        return model;
    }

    private async Task<ReportTableViewModel?> LoadTableAsync<TEntity>(
        Microsoft.EntityFrameworkCore.Metadata.IEntityType entityType,
        int page,
        int pageSize,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var primaryKeyProperties = entityType.FindPrimaryKey()?.Properties.Select(x => x.Name).ToHashSet() ?? new HashSet<string>();
        var properties = entityType.GetProperties()
            .Where(x => !x.IsShadowProperty() && !HiddenColumns.Contains(x.Name))
            .OrderByDescending(x => primaryKeyProperties.Contains(x.Name))
            .ThenBy(x => x.Name)
            .ToList();

        if (properties.Count == 0)
        {
            return null;
        }

        IQueryable<TEntity> query = _context.Set<TEntity>().AsNoTracking();
        if (entityType.FindProperty("CreatedAt")?.ClrType == typeof(DateTime))
        {
            query = ApplyDateFilter(query, "CreatedAt", from, to);
        }

        var rowCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new ReportTableViewModel
        {
            Name = entityType.GetTableName() ?? entityType.ClrType.Name,
            RowCount = rowCount,
            Columns = properties.Select(x => x.Name).ToList(),
            Rows = rows.Select(row => properties.Select(property =>
            {
                var value = property.PropertyInfo?.GetValue(row);
                return FormatValue(value);
            }).ToList()).ToList()
        };
    }

    private static IQueryable<TEntity> ApplyDateFilter<TEntity>(IQueryable<TEntity> query, string propertyName, DateTime? from, DateTime? to)
    {
        if (!from.HasValue && !to.HasValue)
        {
            return query;
        }

        var parameter = Expression.Parameter(typeof(TEntity), "x");
        var property = Expression.Call(typeof(EF), nameof(EF.Property), [typeof(DateTime)], parameter, Expression.Constant(propertyName));
        Expression? body = null;
        if (from.HasValue)
        {
            body = Expression.GreaterThanOrEqual(property, Expression.Constant(from.Value.ToUniversalTime()));
        }
        if (to.HasValue)
        {
            var upper = Expression.LessThan(property, Expression.Constant(to.Value.ToUniversalTime()));
            body = body is null ? upper : Expression.AndAlso(body, upper);
        }

        return body is null ? query : query.Where(Expression.Lambda<Func<TEntity, bool>>(body, parameter));
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
