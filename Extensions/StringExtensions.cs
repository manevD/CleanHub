using System.Text.RegularExpressions;

namespace CleanHub.Extensions
{
    public static class StringExtensions
    {
        public static int ExtractNumberAfterSt(this string input)
        {
            if (string.IsNullOrEmpty(input))
                return int.MaxValue; // Default to a large number if parsing fails

            var match = Regex.Match(input, @"ст\.(\d+)");
            if (match.Success)
            {
                if (int.TryParse(match.Groups[1].Value, out int number))
                {
                    return number;
                }
            }
            return int.MaxValue; // Default to a large number if parsing fails
        }
    }
}
