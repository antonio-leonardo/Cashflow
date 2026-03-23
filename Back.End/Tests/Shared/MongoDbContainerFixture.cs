using DotNet.Testcontainers.Networks;
using Testcontainers.MongoDb;

namespace Infrastructure.Test
{
    public class MongoDbContainerFixture : IAsyncLifetime
    {
        private const string MongoImage = "mongo:7.0.31";
        private readonly string _alias;
        public MongoDbContainer Mongo { get; }

        public MongoDbContainerFixture(INetwork network, string alias)
        {
            _alias = alias;
            Mongo = new MongoDbBuilder(MongoImage)
                .WithNetwork(network)
                .WithNetworkAliases(alias)
                .Build();
        }

        public async Task InitializeAsync() => await Mongo.StartAsync();
        public async Task DisposeAsync() => await Mongo.DisposeAsync();

        public string ConnectionString => Mongo.GetConnectionString();

        public string NetworkConnectionString => Mongo.GetConnectionString()
            .Replace("localhost", _alias)
            .Replace($":{Mongo.GetMappedPublicPort(27017)}", ":27017");
    }
}
