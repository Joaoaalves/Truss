namespace Truss.Remote
{
    /// <summary>
    /// Options of one remote context. Everything that makes the call a network
    /// call is here, visible in the composition root: where the service lives,
    /// how long a query may take, and the route prefix its host maps.
    /// </summary>
    public sealed class RemoteContextOptions
    {
        /// <summary>
        /// Gets or sets the base address of the remote context's host.
        /// </summary>
        public Uri? BaseAddress { get; set; }

        /// <summary>
        /// Gets or sets the route prefix the remote host maps its queries
        /// under. Defaults to "/truss/remote", the MapRemoteContext default.
        /// </summary>
        public string Prefix { get; set; } = "/truss/remote";

        /// <summary>
        /// Gets or sets how long a remote query may take before the call
        /// fails. Defaults to 5 seconds: a synchronous query that needs more
        /// probably wants to be an event.
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);
    }
}
