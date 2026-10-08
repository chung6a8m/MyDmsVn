using System;
using System.Collections.Generic;
using System.Linq;
using RepoDb;

namespace MyDmsVn.Server.Infrastructure.Persistence;

public sealed class RepoDbMappingInitializer
{
    private static readonly object SyncRoot = new();
    private static readonly HashSet<Type> ConfiguredMappingTypes = new();
    private readonly IReadOnlyCollection<IRepoDbMapping> _mappings;

    public RepoDbMappingInitializer(IEnumerable<IRepoDbMapping> mappings)
    {
        _mappings = (mappings ?? throw new ArgumentNullException(nameof(mappings))).ToArray();
    }

    public void Initialize()
    {
        lock (SyncRoot)
        {
            if (!SqlServerBootstrap.IsInitialized)
            {
                GlobalConfiguration.Setup().UseSqlServer();
            }

            foreach (var mapping in _mappings)
            {
                var mappingType = mapping.GetType();
                if (ConfiguredMappingTypes.Contains(mappingType))
                {
                    continue;
                }

                mapping.Configure();
                ConfiguredMappingTypes.Add(mappingType);
            }
        }
    }
}
