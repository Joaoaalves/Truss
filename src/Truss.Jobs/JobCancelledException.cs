namespace Truss.Jobs
{
    internal sealed class JobCancelledException : Exception
    {
        public JobCancelledException() : base("The job was cancelled.")
        {
        }
    }
}
