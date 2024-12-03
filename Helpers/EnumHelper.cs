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
            FieldInfo field = enumValue.GetType().GetField(enumValue.ToString());
            DescriptionAttribute attribute = field?.GetCustomAttribute<DescriptionAttribute>();

            return attribute != null ? attribute.Description : enumValue.ToString();
        }
    }
}
