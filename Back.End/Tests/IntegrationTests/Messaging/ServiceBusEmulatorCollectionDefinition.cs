using Infrastructure.Test;

namespace Messaging.Integration.Tests
{
    [CollectionDefinition("ServiceBusEmulatorCollection")]
    public class ServiceBusEmulatorCollectionDefinition
        : ICollectionFixture<ServiceBusEmulatorFixture> { }
}
