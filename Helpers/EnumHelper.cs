using System.ComponentModel;
using System.Reflection;

namespace CleanHub.Helpers
{
    public class EnumHelper
    {
        public static string GetEnumDescriptionFromId<TEnum>(int id) where TEnum : Enum
        {
            // Convert integer ID to enum value
            if (!Enum.IsDefined(typeof(TEnum), id))
                return $"Unknown ({id})"; // Handle undefined IDs gracefully

            TEnum enumValue = (TEnum)Enum.ToObject(typeof(TEnum), id);

            // Retrieve the field info and the DescriptionAttribute
            FieldInfo? field = enumValue.GetType().GetField(enumValue.ToString());
            DescriptionAttribute? attribute = field?.GetCustomAttribute<DescriptionAttribute>();

            return attribute != null ? attribute.Description : enumValue.ToString();
        }

        public static List<KeyValuePair<int, string>> GetEnumDescriptions<T>() where T : Enum
        {
            return Enum.GetValues(typeof(T))
                .Cast<T>()
                .Select(e => new KeyValuePair<int, string>(
                    Convert.ToInt32(e),
                    GetEnumDescription(e)
                ))
                .ToList();
        }

        private static string GetEnumDescription<T>(T enumValue) where T : Enum
        {
            var fieldInfo = enumValue.GetType().GetField(enumValue.ToString());
            var attributes = fieldInfo?.GetCustomAttributes(typeof(DescriptionAttribute), false) as DescriptionAttribute[];
            return attributes?.FirstOrDefault()?.Description ?? enumValue.ToString();
        }
    }
}
