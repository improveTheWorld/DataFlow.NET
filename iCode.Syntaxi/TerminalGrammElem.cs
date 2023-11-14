namespace iCode.Framework.Syntaxi
{
    public class TerminalGrammElem : ITokenEater
    {
        private readonly string _token;

        public TerminalGrammElem(string token)
        {
            _token = token ?? throw new ArgumentNullException(nameof(token));
        }

        public TokenDigestion AcceptToken(string token)
        {
            if (_token == token) return TokenDigestion.Completed;
            else return TokenDigestion.None;
        }
    }   
}
