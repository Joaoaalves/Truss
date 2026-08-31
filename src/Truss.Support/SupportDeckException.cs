namespace Truss.Support
{
    /// <summary>
    /// The deck did not answer, or answered outside the contract. The message
    /// names the operation and the address, so the log alone places the
    /// failure.
    /// </summary>
    public class SupportDeckException : Exception
    {
        public SupportDeckException(string message) : base(message)
        {
        }

        public SupportDeckException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
