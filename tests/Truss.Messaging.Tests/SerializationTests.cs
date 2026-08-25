using Truss.Messaging.Tests.Fakes;
using Xunit;
using Truss.Messaging.Serialization;

namespace Truss.Messaging.Tests
{
    public class SerializationTests
    {
        private static JsonIntegrationEventSerializer CreateSerializer()
        {
            var registry = IntegrationEventTypeRegistry.FromAssemblies([typeof(ItemCreated).Assembly]);
            return new JsonIntegrationEventSerializer(registry);
        }

        [Fact]
        public void Serialize_UsesAttributeNameAndVersion()
        {
            var serializer = CreateSerializer();
            var integrationEvent = new ItemCreated(Guid.NewGuid());

            var envelope = serializer.Serialize(integrationEvent);

            Assert.Equal("test.item-created", envelope.Name);
            Assert.Equal(1, envelope.Version);
            Assert.Equal(integrationEvent.Id, envelope.MessageId);
        }

        [Fact]
        public void Serialize_WithoutAttribute_DefaultsToFullTypeName()
        {
            var serializer = CreateSerializer();

            var envelope = serializer.Serialize(new UnnamedEvent("abc"));

            Assert.Equal(typeof(UnnamedEvent).FullName, envelope.Name);
            Assert.Equal(1, envelope.Version);
        }

        [Fact]
        public void Roundtrip_PreservesEventData()
        {
            var serializer = CreateSerializer();
            var original = new ItemCreated(Guid.NewGuid());

            var deserialized = serializer.Deserialize(serializer.Serialize(original));

            var restored = Assert.IsType<ItemCreated>(deserialized);
            Assert.Equal(original.ItemId, restored.ItemId);
            Assert.Equal(original.Id, restored.Id);
        }

        [Fact]
        public void Deserialize_ResolvesTypeByVersion()
        {
            var serializer = CreateSerializer();
            var v2 = new ItemCreatedV2(Guid.NewGuid(), "Beam");

            var deserialized = serializer.Deserialize(serializer.Serialize(v2));

            var restored = Assert.IsType<ItemCreatedV2>(deserialized);
            Assert.Equal("Beam", restored.Name);
        }

        [Fact]
        public void Deserialize_UnknownName_Throws()
        {
            var serializer = CreateSerializer();
            var envelope = new IntegrationEventEnvelope(Guid.NewGuid(), "unknown.event", 1, DateTimeOffset.UtcNow, "{}");

            Assert.Throws<UnknownIntegrationEventException>(() => serializer.Deserialize(envelope));
        }

        [Fact]
        public void Deserialize_UnknownVersion_Throws()
        {
            var serializer = CreateSerializer();
            var envelope = new IntegrationEventEnvelope(Guid.NewGuid(), "test.item-created", 9, DateTimeOffset.UtcNow, "{}");

            Assert.Throws<UnknownIntegrationEventException>(() => serializer.Deserialize(envelope));
        }
    }
}
