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

        public static int MergeByIdAndSave<T>(this DbContext dbContext, T entity)
            where T : class, new()
        {
            var merge = MergeByIdAndSaveAsync(dbContext, entity);
            merge.Wait();
            return merge.Result;
        }

        public static async Task<int> MergeByIdAndSaveAsync<T>(this DbContext dbContext, T entity,
            CancellationToken cancellationToken = default)
            where T : class, new()
        {
            var entityType = dbContext.Model.FindEntityType(typeof(T));
            var tableName = entityType.Relational().TableName;
            var key = entityType.FindPrimaryKey().Properties;
            var properties = entityType.GetProperties().ToList();
            var propertyIndex = 0;

            var condition = string.Join(" AND ", key.Select(x => $"[Target].[{x.Name}] = [Source].[{x.Name}]"));
            var parameters = string.Join(", ", properties.Select(x => $"@p{propertyIndex++}"));
            var columns = string.Join(", ", properties.Select(x => $"[{x.Name}]"));
            var update = string.Join(", ", properties.Select(x => $"[{x.Name}] = [Source].[{x.Name}]"));
            var values = string.Join(", ", properties.Select(x => $"[Source].[{x.Name}]"));
            var sql = $@"
MERGE [{tableName}] AS [Target]
USING (SELECT {parameters}) AS [Source] ({columns})
ON {condition}
WHEN MATCHED THEN
	UPDATE SET {update}
WHEN NOT MATCHED THEN
	INSERT ({columns})
	VALUES ({values})
;";

            var parameterValues = properties.Select(x => x.PropertyInfo.GetValue(entity));

#pragma warning disable EF1000 // Possible SQL injection vulnerability.
            return await dbContext.Database.ExecuteSqlCommandAsync(sql, parameterValues, cancellationToken);
#pragma warning restore EF1000 // Possible SQL injection vulnerability.
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
