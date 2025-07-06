namespace DataFlow.Framework
{
    public class ArgRequirement
    {
        [Order] public string ArgName;
        [Order] public string ShortName;
        [Order] public string LongName;
        [Order] public bool IsMandatory;
        [Order] public string DefaultValue;
        [Order] public string Description;

        public ArgRequirement(string argName, string shortName, string longName,  string defaultValue, string description ="", bool isMandatory = false)
        {
            ArgName = argName;
            ShortName = shortName;
            LongName = longName;
            IsMandatory = isMandatory;
            DefaultValue = defaultValue;
            Description = description;
        }

        
        public override string ToString()
        {
            return $" Parameter :{ArgName}, ShortName :{ShortName}, LongName :{LongName}, required :{IsMandatory}, DefaultValue :{DefaultValue}, HelpText :{Description}, ";
        }
    }

}
