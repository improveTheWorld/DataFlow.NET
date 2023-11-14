namespace iCode.Framework.Syntaxi
{

    public class FieldGrammarElem : ITokenEater
    {        
        string _name;
        Type _type;
        int _size;


        public FieldGrammarElem(string name,Type type, int size) 
        {
            _name = name;
            _type = type;
            _size = size;
        }

        public TokenDigestion AcceptToken(string token)
        {
            throw new NotImplementedException();
        }
    }
    // This class represents a grammar element.
    public class GrammarElem : ITokenEater
    {
        GrammarRule[] _rules = new GrammarRule[0];

        
        void AddRule(GrammarRule grammarRule) => _rules.Append(grammarRule);

        public TokenDigestion AcceptToken(string token)
        {
            TokenDigestion returnValue = TokenDigestion.None;

            foreach (var rule in _rules)
            {
                if (rule.IsActive())
                {
                    returnValue |= rule.AcceptToken(token);
                }
            }

            return returnValue;
        }

        public void Activate()
        {
            foreach (var rule in _rules)
            {
                rule.Activate();
            }
        }
        private GrammarElem() 
        {
            // Used internally for gradual building of the object.
        }
        static public class Builder
        {     

            static GrammarElem Build(params Rule[] rules)
            {
                Dictionary<string, ITokenEater> gramm = new Dictionary<string, ITokenEater>();

                // Create or get the GrammarElem for each left part of the rule.
                foreach (var rule in rules)
                {
                    if (!gramm.ContainsKey(rule.LeftPart))
                    {
                        gramm[rule.LeftPart] = new GrammarElem();
                    }                   
                }

                // For each rule, process the right part elements
                foreach (var rule in rules)
                {
                    var currentRule = new List<ITokenEater>();
                    foreach (var token in rule.RightPart)
                    {
                        // For terminal elements, create a TerminalGrammElem, otherwise get from the dictionary
                        var gramElem = gramm.ContainsKey(token) ? gramm[token] : gramm[token] = new TerminalGrammElem(token);
                        currentRule.Add(gramElem);
                    }

                    ((GrammarElem)gramm[rule.LeftPart]).AddRule( new GrammarRule(currentRule));
                }
                
                // Assuming we return the first element as the starting point of the grammar
                return (GrammarElem) gramm.Values.First();
            }
        }
    }
}
