namespace DataFlow.Log
{
    public class NameSpaceComparer
    {
        string[] namespaces;
        public bool includeSubSpaces = false;


        public NameSpaceComparer ParentNameSpace(int level)
        {
            return new NameSpaceComparer(this,level);
        }
        NameSpaceComparer(NameSpaceComparer inputNameSpace, int ignore)
        {

            if (0 <= ignore && ignore < inputNameSpace.namespaces.Length)
            {
                namespaces = inputNameSpace.namespaces.Take(inputNameSpace.namespaces.Length - ignore).ToArray();
            }
            else
            {
                throw new ArgumentOutOfRangeException($"Second argument = {ignore}, should be between 0 and {inputNameSpace.namespaces.Length-1}");
            }
        }
        public NameSpaceComparer(string nameSpace)
        {

            namespaces = nameSpace.Split('.')
                                  .Select(s => s.Trim())
                                  .Where( s => s!= string.Empty)
                                  .ToArray();
        }

        bool IsMatching( string pattern, string input)
        {
            if(pattern == "*"|| pattern == input)
            {
                return true;
            }
            else
            {
                if (pattern.EndsWith("*") && input.StartsWith(pattern.TrimEnd('*')))
                {
                    return true;
                }

                if (pattern.StartsWith("*") && input.EndsWith(pattern.TrimStart('*')))
                {
                    return true;
                }

                if (pattern.StartsWith("*") && pattern.EndsWith("*"))
                {
                    string trimmedPattern = pattern.Trim('*');
                    if (input.Contains(trimmedPattern))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool IsMatching(NameSpaceComparer input)
        {
            
            if(input.namespaces.Length > namespaces.Length  && includeSubSpaces)
            {
                return IsMatching(new NameSpaceComparer(input, input.namespaces.Length - namespaces.Length)) ;
            }
            else if (namespaces.Length != input.namespaces.Length)
            {
                return false;
            }
            else
            { 
                for (int i=0; i<  namespaces.Length; i++)
                {
                    if ( !IsMatching(namespaces[i], input.namespaces[i] ) )
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        //override public string ToString()
        //{
        //    return namespaces
        //}
    }
}
