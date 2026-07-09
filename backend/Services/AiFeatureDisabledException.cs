namespace PhotoLibApi.Services
{
    /// <summary>
    /// Thrown when an AI-backed operation is requested while the feature is
    /// turned off via the <c>Ai:Enabled</c> config flag.
    /// </summary>
    public class AiFeatureDisabledException : Exception
    {
        public AiFeatureDisabledException()
            : base("AI features are disabled on this server.")
        {
        }
    }
}
