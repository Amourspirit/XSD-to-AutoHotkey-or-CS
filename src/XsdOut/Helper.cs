using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BigByteTechnologies.XsdOut
{
    /// <summary>
    /// Various Helper Methods.
    /// </summary>
    public static class Helper
    {
        /// <summary>
        /// Converts the first Letter of a string to Uppercase
        /// </summary>
        /// <param name="s">The string to convert</param>
        /// <returns>
        /// String with first letter in Uppercase if <paramref name="s"/> has a value; Otherwise empty string.
        /// </returns>
        public static string UpperCaseFirst(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }
            string retval = char.ToUpper(s[0]) + s.Substring(1);
            return retval;
        }

        /// <summary>
        /// Converts the first Letter of a string to Lowercase
        /// </summary>
        /// <param name="s">The string to convert</param>
        /// <returns>
        /// String with first letter in lowercase if <paramref name="s"/> has a value; Otherwise empty string.
        /// </returns>
        public static string LowerCaseFirst(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }
            string retval = char.ToLower(s[0]) + s.Substring(1);
            return retval;
        }
    }
}
