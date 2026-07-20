using Newtonsoft.Json.Linq;
namespace BOCCHI.Common.Config.Migrations;

public class ConfigurationMigrationResolver : IConfigurationMigrationResolver
{
    public ConfigurationMigrationResolver(IEnumerable<IMigrator> migrators)
    {
        foreach(IMigrator migrator in migrators)
        {
            if (!migratorMap.TryAdd(migrator.FromVersion, migrator))
            {
                throw new DuplicateMigrationBaseException(migrator.FromVersion);
            }
        }
    }
    private Dictionary<int, IMigrator> migratorMap { get; } = [];

    public IMigrator? Resolve(int from) => migratorMap.TryGetValue(from, out IMigrator? migrator) ? migrator : null;

    public IMigrator? Resolve(JObject obj) => Resolve(obj["Version"]?.Value<int>() ?? 1);

    public bool CanMigrateTo(int from, int to)
    {
        if (from == to)
        {
            return true;
        }

        if (to < from)
        {
            return false;
        }

        HashSet<int> visited = new();
        int current = from;

        while(migratorMap.TryGetValue(current, out IMigrator? migrator))
        {
            if (!visited.Add(current))
            {
                return false;
            }

            int next = migrator.ToVersion;

            if (next == to)
            {
                return true;
            }

            if (next == current)
            {
                return false;
            }

            current = next;
        }

        return false;
    }
}
