using System.ComponentModel;

namespace SkPluginLibrary.Models.Helpers
{
    public static class EnumHelpers
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
        public static string GetOpenAIModelName(this Enum value, bool isAzure = false)
        {
            var fi = value.GetType().GetField(value.ToString());
            if (isAzure)
            {
                var attribs = (AzureOpenAIModelAttribute[])fi.GetCustomAttributes(typeof(AzureOpenAIModelAttribute), false);
                return attribs is { Length: > 0 } ? attribs[0].Model : string.Empty;
            }
            var attributes = (ModelNameAttribute[])fi.GetCustomAttributes(typeof(ModelNameAttribute), false);

            return attributes is { Length: > 0 } ? attributes[0].Model : string.Empty;
        }
        public static IEnumerable<string> GetModelProvidors(this AIModel model)
        {
	        var type = model.GetType();
	        var memberInfo = type.GetMember(model.ToString()).FirstOrDefault();
	        if (memberInfo != null)
	        {
		        var attributes = memberInfo.GetCustomAttributes(typeof(ModelProvidorAttribute), false);
		        return attributes.Select(attr => ((ModelProvidorAttribute)attr).Providor);
	        }
	        return Enumerable.Empty<string>();
        }
		public static Dictionary<AIModel, string> GetAIModelWithName()
        {
            return Enum.GetValues<AIModel>().ToDictionary(x => x, x => x.GetOpenAIModelName());
        }
        public static bool IsActive(this Enum value)
        {
            var fi = value.GetType().GetField(value.ToString());
            var attributes = (IsActiveAttribute[])fi.GetCustomAttributes(typeof(IsActiveAttribute), false);

            return attributes is { Length: > 0 } && attributes[0].IsActive;

        }
        public static Dictionary<TEnum, string> GetEnumsWithDescriptions<TEnum>() where TEnum : Enum
        {
            var enumDict = Enum.GetValues(typeof(TEnum)).Cast<TEnum>()
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
                    return (TEnum)field.GetValue(null)!;
                }
            }
            return default!;
        }
    }
}
