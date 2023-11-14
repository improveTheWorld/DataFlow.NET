namespace iCode.Framework.Syntaxi
{
    // This class represents a grammar rule.
    public class GrammarRule : ITokenEater
    {
        private readonly ITokenEater[] _tokenEaters;
        private HashSet<int> _cursors;

        // The constructor must be public to be used outside of the class.
        public GrammarRule(IEnumerable<ITokenEater> grammar)
        {
            _tokenEaters = grammar.ToArray() ?? throw new ArgumentNullException(nameof(grammar), "The grammar array cannot be null.");

            if (_tokenEaters.Length == 0)
            {
                throw new ArgumentException("The grammar array cannot be empty.", nameof(grammar));
            }

            Activate();
        }

        // This method checks if the grammar rule is active.
        public bool IsActive() => _cursors.Count > 0;

        // This method activates the grammar rule.
        public void Activate()
        {
            _cursors = new HashSet<int>() { 0 };
        }

        public TokenDigestion AcceptToken(string token)
        {
            TokenDigestion result = TokenDigestion.None;
            var toAdd = new HashSet<int>();
            var toRemove = new HashSet<int>();

            // Iterate in reverse to safely remove items without affecting earlier indices
            // We use ToList to create a snapshot of the current state of _tags for iteration
            foreach (var cursor in _cursors)
            {
                TokenDigestion digestion = _tokenEaters[cursor].AcceptToken(token);

                if (digestion == TokenDigestion.None)
                {
                    // Remove the cursor immediately since it's no longer needed
                    toRemove.Add(cursor);
                }
                else
                {
                    if (digestion.HasFlag(TokenDigestion.Digested))
                    {
                        result |= TokenDigestion.Digested;
                    }
                    else
                    {
                        toRemove.Add(cursor);
                    }

                    if (digestion.HasFlag(TokenDigestion.Completed))
                    {
                        if (cursor == _tokenEaters.Length - 1)
                        {
                            result |= TokenDigestion.Completed;
                        }
                        else
                        {
                            toAdd.Add(cursor + 1);
                        }
                    }
                }
            }

            // Now apply the changes recorded during iteration
            _cursors.ExceptWith(toRemove);
            _cursors.UnionWith(toAdd);

            return result;
        }
    }
}
