using Infrastructure.Test;

namespace Balance.Domain.Tests;

[CollectionDefinition("MongoDbCollection")]
public class MongoDbCollectionDefinition : ICollectionFixture<MongoDbContainerFixture> { }
