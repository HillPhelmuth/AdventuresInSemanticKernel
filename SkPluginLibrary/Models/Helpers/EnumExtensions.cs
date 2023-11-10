using System.ComponentModel;
using System.Reflection;

namespace SkPluginLibrary.Models.Helpers
{
    public static class EnumExtensions
    {
        public static string GetDescription(this Enum value)
        {
            var fi = value.GetType().GetField(value.ToString());
            var attributes = (DescriptionAttribute[])fi.GetCustomAttributes(typeof(DescriptionAttribute), false);

            return attributes is { Length: > 0 } ? attributes[0].Description : value.ToString();
        }

        public static string GetLongDescription(this Enum value)
        {
            var fi = value.GetType().GetField(value.ToString());
            var attributes = (LongDescriptionAttribute[])fi.GetCustomAttributes(typeof(LongDescriptionAttribute), false);

            return attributes is { Length: > 0 } ? attributes[0].LongDescription : string.Empty;

        }
        public static Dictionary<TEnum, string> GetEnumsWithDescriptions<TEnum>(this Type enumType) where TEnum : Enum
        {
            var enumDict = Enum.GetValues(enumType).Cast<TEnum>()
                .ToDictionary(t => t, t => t.GetDescription());
            return enumDict;
        }
        public static TEnum GetEnumValueFromDescription<TEnum>(string description) where TEnum : Enum
        {
            foreach (var field in typeof(TEnum).GetFields())
            {
                if (field.GetCustomAttributes(typeof(DescriptionAttribute), false)
                        .FirstOrDefault() is DescriptionAttribute attribute && attribute.Description == description)
                {
                    return (TEnum)field.GetValue(null);
                }
            }
            return default;
        }
    }
}
