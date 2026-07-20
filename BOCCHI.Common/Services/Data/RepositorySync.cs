using Ocelot.Services.Data;
namespace BOCCHI.Common.Services.Data;

public static class RepositorySync
{
    public static void ApplySnapshot<TId, TEntity>(
        IDataRepository<TId, TEntity> data,
        IReadOnlyDictionary<TId, TEntity> current,
        Action<TEntity>? onAdded = null,
        Action<TId>? onRemoved = null
    )
    where TId : notnull
    {
        foreach((TId id, TEntity entity) in current)
        {
            if (data.TryAdd(id, entity))
            {
                onAdded?.Invoke(entity);
            }
        }

        foreach(TId id in data.GetKeys().Except(current.Keys).ToList())
        {
            if (data.Remove(id))
            {
                onRemoved?.Invoke(id);
            }
        }
    }
}
