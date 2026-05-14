using System.Linq.Expressions;
using BranchPOS.Data;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    private readonly AppDbContext _context;
    private readonly DbSet<T> _dbSet;

    public Repository(AppDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public IQueryable<T> Query() => _dbSet;

    public Task<List<T>> ListAsync(CancellationToken cancellationToken = default) =>
        _dbSet.ToListAsync(cancellationToken);

    public Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _dbSet.FindAsync([id], cancellationToken).AsTask();

    public Task AddAsync(T entity, CancellationToken cancellationToken = default) =>
        _dbSet.AddAsync(entity, cancellationToken).AsTask();

    public void Update(T entity) => _dbSet.Update(entity);

    public void Remove(T entity) => _dbSet.Remove(entity);

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
        _dbSet.AnyAsync(predicate, cancellationToken);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
