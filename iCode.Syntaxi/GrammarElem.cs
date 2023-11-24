using iCode.Extensions;
using System.Data;
using System.Text.RegularExpressions;

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
            _rules.Where(x => x.IsActive()).ForEach(x => returnValue |= x.AcceptToken(token));
            return returnValue;
        }

        public void Activate()
        {
            _rules.ForEach(x => x.Activate());
        }
        private GrammarElem() 
        {
            // Used internally for gradual building of the object.
        }
        static public class Builder
        {

            static ITokenEater getOrCreate(Dictionary<string, ITokenEater> gramm, string key) =>  gramm.ContainsKey(key) ? gramm[key] : gramm[key] = new TerminalGrammElem(key);

            static GrammarElem Build(params Rule[] rules)
            {
                // Create or get the GrammarElem for each left part of the rule.
                Dictionary<string, ITokenEater> gramm = new(rules.Select(x => x.LeftPart)
                                                                 .Distinct()
                                                                 .Select(x => new KeyValuePair<string, ITokenEater>(x, new GrammarElem())));

            // For each rule, process the right part elements
            rules.ForEach(rule => ((GrammarElem)gramm[rule.LeftPart]).AddRule(new(rule.RightPart.Select(token => getOrCreate(gramm, token)))));
                
            // Assuming we return the first element as the starting point of the grammar
            return (GrammarElem) gramm.Values.First();
            }
        }
    }


    class Program
    {
        static void Main()
        {
            string input = "debuttt:{donné1}+suiviDD:{donné2}";
            string pattern = @"debuttt:\{(.+?)\}\+suiviDD:\{(.+?)\}";
             
            string output = Regex.Replace(input, pattern, m =>
            {
                // Lire les valeurs
                string donné1 = m.Groups[1].Value;
                string donné2 = m.Groups[2].Value;

                Console.WriteLine("Donné 1: " + donné1);
                Console.WriteLine("Donné 2: " + donné2);

                // Définir les nouvelles valeurs
                string nouvelleValeur1 = "nouvelleDonnée1";
                string nouvelleValeur2 = "nouvelleDonnée2";
                // Retourner la chaîne de remplacement
                return $"debuttt:{{{nouvelleValeur1}}}+suiviDD:{{{nouvelleValeur2}}}";
            });

            Console.WriteLine("Chaîne modifiée: " + output);
        }
    }
}