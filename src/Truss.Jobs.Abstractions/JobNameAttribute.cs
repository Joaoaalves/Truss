namespace Truss.Jobs
{
    /// <summary>
    /// Declares the stable name of a job type.
    /// The name is stored with each job record, so renaming the CLR type does not orphan
    /// queued jobs. Without the attribute, the full CLR type name is used.
    /// </summary>
    /// <param name="name">The stable name of the job, for example "reports.generate".</param>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class JobNameAttribute(string name) : Attribute
    {
        /// <summary>
        /// Gets the stable name of the job.
        /// </summary>
        public string Name { get; } = name;
    }
}
