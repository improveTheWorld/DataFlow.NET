namespace iCode.Framework.Syntaxi
{

    /// <summary>
    /// Enumerates the possible outcomes of a token digestion by an ITokenEater.
    /// </summary>
    [Flags]
    public enum TokenDigestion
    {
        /// <summary>
        /// Indicates that the token was not matched or processed.
        /// </summary>
        None = 0,
        /// <summary>
        /// Indicates that the token was matched and has triggered a state transition within the ITokenEater.
        /// </summary>
        Digested = 2,
        /// <summary>
        /// Indicates that the token was matched and has completed the current expectation of the ITokenEater.
        /// </summary>
        Completed = 4,
        /// <summary>
        /// Indicates
        /// </summary>
        Propagate = 8
    }


    /// <summary>
    /// Defines the interface for token processing entities within the parsing framework.
    /// </summary>
    public interface ITokenEater
    {
        /// <summary>
        /// Processes the given token and returns the digestion state.
        /// </summary>
        /// <param name="token">The token to be processed.</param>
        /// <returns>A TokenDigestion value indicating the outcome of the token processing.</returns>
        TokenDigestion AcceptToken(string token);

        /// <summary>
        /// Activates the ITokenEater, preparing it for token processing.
        /// </summary>
        public void Activate()
        {
            // Default empty implementation.
        }
    }

}
