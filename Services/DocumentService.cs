using CleanHub.Entities;
using CleanHub.ViewModels;
using Microsoft.AspNetCore.Http;
using System.Text.RegularExpressions;

namespace CleanHub.Services
{
    public class DocumentService
    {

        public static int ExtractMonth(string input)
        {
            int month = 0;

            // Regular expression to match the month and year
            Regex regex = new Regex(@"(?:\b(\d{1,2})/(\d{4})\b|\((\d{1,2})\) is the month and (\d{4}) is the year)");

            Match match = regex.Match(input);
            if (match.Success)
            {
                if (match.Groups[1].Success)
                {
                    // First format (e.g., "12/2020")
                    month = int.Parse(match.Groups[1].Value);
                }
                else if (match.Groups[3].Success)
                {
                    // Second format (e.g., "(08) is the month and 2021 is the year")
                    month = int.Parse(match.Groups[3].Value);
                }
            }
            return month;
        }

        public static int ExtractYear(string input)
        {
            int year = 0;
            Regex regex = new Regex(@"\b\d{4}\b");
            Match match = regex.Match(input);

            if (match.Success)
            {
                year = int.Parse(match.Value);
            }
            return year;
        }

        public static string GetMonthAsString(int month)
        {
            switch (month)
            {
                case 1:
                    return "Јануари";
                case 2:
                    return "Февруари";
                case 3:
                    return "Март";
                case 4:
                    return "Април";
                case 5:
                    return "Мај";
                case 6:
                    return "Јуни";
                case 7:
                    return "Јули";
                case 8:
                    return "Август";
                case 9:
                    return "Септември";
                case 10:
                    return "Октомври";
                case 11:
                    return "Ноември";
                case 12:
                    return "Декември";
                default:
                    return "";
            }
        }

        public static int GetMonthAsInteger(string input)
        {
            Regex regex = new Regex(@"^\w+");
            Match match = regex.Match(input);

            if (match.Success)
            {
                var month = match.Value;

                switch (month)
                {
                    case "Јануари":
                        return 1;
                    case "Февруари":
                        return 2;
                    case "Март":
                        return 3;
                    case "Април":
                        return 4;
                    case "Мај":
                        return 5;
                    case "Јуни":
                        return 6;
                    case "Јули":
                        return 7;
                    case "Август":
                        return 8;
                    case "Септември":
                        return 9;
                    case "Октомври":
                        return 10;
                    case "Ноември":
                        return 11;
                    case "Декември":
                        return 12;
                    default:
                        return 0;
                }
            }
            else return 0;
        }

        internal static PaymentStatus GetStatus(BookFinancial? bookFinancial, Document document)
        {
            if (bookFinancial != null)
            {
                if (document.DateReceived.Value < document.DateReceived.Value.AddMonths(1))
                {
                    return PaymentStatus.Задоцнето;
                }

                if (bookFinancial.Owes <= 0)
                {
                    return PaymentStatus.Платено;
                }

                if (bookFinancial.Owes > 0 && bookFinancial.Owes < bookFinancial.Demands)
                {
                    return PaymentStatus.Делумно;
                }

                if (bookFinancial.Owes != 0 && bookFinancial.Demands == 0)
                {
                    return PaymentStatus.Неплатено;
                }
            }
            return PaymentStatus.Неплатено;
        }
    }
}
