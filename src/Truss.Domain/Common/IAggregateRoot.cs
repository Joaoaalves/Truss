namespace Truss.Domain.Common
{
    /// <summary>
    /// Marker interface used to indicate that an entity is an aggregate root.
    /// Aggregate roots are the entry point to a cluster of domain objects and enforce consistency rules.
    /// </summary>
    public interface IAggregateRoot
    {
    }
}
