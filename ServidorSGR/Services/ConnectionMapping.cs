using System.Collections.Concurrent; 

namespace ServidorSGR.Services
{

    public interface IConnectionMapping
    {
        void Add(string alias, string connectionId);
        void RemoveByConnectionId(string connectionId);
        string? GetConnectionId(string alias);
        string? GetAliasByConnectionId(string connectionId); 
        int Count { get; }
    }

    public class ConnectionMapping : IConnectionMapping
    {
        private readonly ConcurrentDictionary<string, string> _aliasToConnectionId = new ConcurrentDictionary<string, string>();
        private readonly ConcurrentDictionary<string, string> _connectionIdToAlias = new ConcurrentDictionary<string, string>();

        public void Add(string alias, string connectionId)
        {
            _aliasToConnectionId.AddOrUpdate(alias, connectionId, (key, oldValue) => connectionId);
            _connectionIdToAlias.AddOrUpdate(connectionId, alias, (key, oldValue) => alias);
        }

        public void RemoveByConnectionId(string connectionId)
        {
            if (_connectionIdToAlias.TryRemove(connectionId, out string? alias))
            {
                _aliasToConnectionId.TryRemove(alias, out string? removedConnectionId);

            }
        }

        public string? GetConnectionId(string alias)
        {
            _aliasToConnectionId.TryGetValue(alias, out string? connectionId);
            return connectionId;
        }

        public string? GetAliasByConnectionId(string connectionId)
        {
            _connectionIdToAlias.TryGetValue(connectionId, out string? alias);
            return alias;
        }

        public int Count => _aliasToConnectionId.Count;
    }
}