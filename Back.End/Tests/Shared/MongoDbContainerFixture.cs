using Testcontainers.MongoDb;

namespace Infrastructure.Test
{
    public class MongoDbContainerFixture : IAsyncLifetime
    {        public MongoDbContainer Mongo { get; }

        public MongoDbContainerFixture()
        {
            Mongo = new MongoDbBuilder("mongo:7").Build();
        }

        public async Task InitializeAsync()
        {
            await Mongo.StartAsync();        }

        public async Task DisposeAsync()
        {
            await Mongo.DisposeAsync();
        }

        public string ConnectionString => Mongo.GetConnectionString();
    }
}