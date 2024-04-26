using iCode.Framework;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace iCode.Extensions
{
    public static class StringExtensions
    {
        public static int LastIdx(this string text) => text.Length - 1;
        
       
        public static bool IsBetween(this string text, string start, string end)
        {
            return text.StartsWith(start) && text.EndsWith(end);
        }

        public static bool StartsWith(this string value, IEnumerable<string> acceptedStarts)
        {
            return acceptedStarts?.Select(possibleStart => value.StartsWith(possibleStart))?.FirstOrDefault(x => x) ?? false;
        }


        public static bool IsNullOrEmpty(this string text) => string.IsNullOrEmpty(text);
        public static bool IsNullOrWhiteSpace( this string text) => string.IsNullOrWhiteSpace(text);
        public static bool ContainsAny(this string line, IEnumerable<string> tokens)
        {
            Guard.AgainstNullArgument( nameof(line), line);
            return !(tokens?.FirstOrDefault(x => line.Contains(x)) ?? string.Empty).IsNullOrEmpty();
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


