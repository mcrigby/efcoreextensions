using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Microsoft.EntityFrameworkCore
{
    public static class EFCoreExtensions
    {
        public static EntityEntry<T> AddOrUpdate<T>(this DbSet<T> dbSet, T entity, Expression<Func<T, bool>> predicate)
            where T : class, new()
        {
            var upsert = AddOrUpdateAsync(dbSet, entity, predicate);
            upsert.Wait();
            return upsert.Result;
        }

        public static async Task<EntityEntry<T>> AddOrUpdateAsync<T>(this DbSet<T> dbSet, T entity,
            Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            where T : class, new()
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            var existing = await dbSet.FirstOrDefaultAsync(predicate, cancellationToken);
            if (existing == default(T))
                return await dbSet.AddAsync(entity, cancellationToken);

            return dbSet.Update(entity);
        }

        public static IEnumerable<Type> GetClrTypes(this DbContext dbContext)
        {
            return dbContext.Model
                .GetEntityTypes()
                .Select(x => x.ClrType);
        }

        public static IEnumerable<Type> GetAssemblyTypes(this DbContext dbContext)
        {
            return Assembly.GetAssembly(dbContext.GetType())
                .GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface && !t.IsEnum 
                    && t.IsSealed && (t.Namespace?.Contains("Entities") ?? false));
        }

        public static IEnumerable<Type> GetTypesFromInterface(this IEnumerable<Type> types, Type requiredInterface)
        {
            return from x in types
                   where !x.IsAbstract && !x.IsInterface && x.GetInterfaces().Any(y =>
                             (y == requiredInterface) ||
                             (y.IsGenericType && y.GetGenericTypeDefinition() == requiredInterface))
                   select x;
        }
    }
}
