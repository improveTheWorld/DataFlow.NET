namespace DataFlow.Framework.Syntaxi
{
    public class Rule
    {
        public string LeftPart { get; set; }
        public List<string> RightPart { get; set; }

        public Rule(params string[] strings)
        {
            if (strings == null || strings.Length < 2)
            {
                throw new ArgumentException("A rule must consist of a left-hand non-terminal and at least one right-hand symbol (terminal or non-terminal)."
                                                  , nameof(strings));
            }

            LeftPart = strings[0];
            RightPart = new List<string>(strings.Skip(1));
        }
    }
}
