using iCode.Framework.AutomizedFeeding;
namespace iCode.Framework
{
    public class ArgDescription
    {
        [Order] public string Parameter;
        [Order]  public string ShortName;
        [Order] public string LongName;
        [Order] public bool IsRequired;
        [Order] public string DefaultValue;
        [Order] public string HelpText;

        public override string ToString()
        {
            return $" Parameter :{Parameter}, ShortName :{ShortName}, LongName :{LongName}, required :{IsRequired}, DefaultValue :{DefaultValue}, HelpText :{HelpText}, ";
        }
    }

}
