using iCode.Framework;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace iCode.Extensions
{
    public static class StringExtensions
    {
        /// <summary>
        /// Try to Convert a string to a typed value ( in order bool,  int, Int64, double, dataTime ) 
        /// </summary>
        /// <param name="value"></param>
        /// <returns> Return the first succefull convertion result. If none , return the input string as it</returns>
        public static object Convert(this string value)
        {
            bool boolValue;
            Int32 intValue;
            Int64 bigintValue;
            double doubleValue;
            DateTime dateValue;

            if (bool.TryParse(value, out boolValue))
                return boolValue;
            else if (Int32.TryParse(value, out intValue))
                return intValue;
            else if (Int64.TryParse(value, out bigintValue))
                return bigintValue;
            else if (double.TryParse(value, out doubleValue))
                return doubleValue;
            else if (DateTime.TryParse(value, out dateValue))
                return dateValue;
            else return value;

        }
        public static bool IsNullOrEmpty(this string input) => (input?.Length??0) == 0 ;

       
        public static bool StartsEnds(this string text, string start, string end)
        {
            return text.StartsWith(start) && text.EndsWith(end);
        }

        public static bool StartsWith(this string value, IEnumerable<string> acceptedStarts)
        {
            return acceptedStarts?.Select(possibleStart => value.StartsWith(possibleStart))?.FirstOrDefault(x => x) ?? false;
        }

        public static bool Contains(this string value, IEnumerable<string> any)
        {
            Guard.AgainstNullArgument( nameof(value), value);
            return !(any?.FirstOrDefault(x => value.Contains(x)) ?? string.Empty).IsNullOrEmpty();
        }

        public static string  ReplaceAt(this string value,int index, int length, string toInsert)
        {
            return value.Substring(0, index + 1) + toInsert + value.Substring(index + length);
        }
    }

    public class Subpart
    {
        private readonly string _originalString;
        private int _startIndex; // Champ de sauvegarde pour StartIndex
        private int _endIndex;   // Champ de sauvegarde pour EndIndex

        private int StartIndex
        {
            get => _startIndex;
            set
            {
                if( value < 0) value = 0; 
                if( value > _endIndex) value = _endIndex;
                _startIndex = value;
            }
        }

        private int EndIndex
        {
            get => _endIndex;

            set
            {
                if (value < _startIndex) value = _startIndex;
                if(value > _originalString.Length-1) value = _originalString.Length-1;
                _endIndex = value;
            }
            
        }



   
        private int Length { get => EndIndex - StartIndex; }
        public bool IsNullOrEmpty() => _originalString.IsNullOrEmpty() || EndIndex == StartIndex;

        internal Subpart(string originalString, int startIndex, int endIndex)
        {
            _originalString = originalString ?? throw new ArgumentNullException(nameof(originalString));

            _startIndex = startIndex;
            _endIndex = endIndex;

        }

       


        public override string ToString()
        {
            return _originalString.Substring(StartIndex, Length);
        }

        public override bool Equals(object obj)
        {
            if (obj is string str)
            {
                return this.Equals(str);
            }

            return false;
        }

        public bool Equals(string other)
        {
            if (other == null || other.Length != this.Length)
            {
                return false;
            }

            for (int i = 0; i < this.Length; i++)
            {
                if (_originalString[StartIndex + i] != other[i])
                {
                    return false;
                }
            }

            return true;
        }

        public override int GetHashCode()
        {
            // Simple hash code calculation; you might want to improve this
            return (_originalString, StartIndex, EndIndex).GetHashCode();
        }

        public static bool operator ==(Subpart subpart, string str)
        {
            return subpart?.Equals(str) ?? str == null;
        }

        public static bool operator !=(Subpart subpart, string str)
        {
            return !(subpart == str);
        }

        public Subpart Trim(int start, int end)
        {
            TrimStart(start);
            TrimEnd(end);
            return this;

        }

        public Subpart TrimStart(int steps)
        {
            EndIndex -= steps;
            return this;
        }
  

    public Subpart TrimEnd(int steps)
        {
            StartIndex += steps;
            return this;

        }


    }

    public static class StringSubPartExtensions
    {
        public static Subpart SubPart(this string originalString, int startIndex, int endIndex)
        {
            Guard.AgainstOutOfRange(nameof(startIndex), startIndex, 0, endIndex);
            Guard.AgainstOutOfRange(nameof(endIndex), endIndex, startIndex, originalString.Length -1);               
            return new Subpart(originalString, startIndex, endIndex);
        }
     
    }

}


