using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SkPluginComponents.Models
{
    public static class Helpers
    {
        public static bool TryExractArrayFromJson(string jsonString, out List<string> value)
        {
            value = new List<string>();
            try
            {
                using var doc = JsonDocument.Parse(jsonString);
                var root = doc.RootElement;

                if (root.ValueKind != JsonValueKind.Object) 
                    return false;
                var jsonProperty = root.EnumerateObject()
                    .FirstOrDefault(property => property.Value.ValueKind == JsonValueKind.Array);
                var firstArray = jsonProperty.Value;
                value = ConvertToArrayOfDynamic(firstArray);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Not Valid Json? " + ex.Message);
                return false;
            }

            
        }
        private static List<string> ConvertToArrayOfDynamic(JsonElement array)
        {
            return array.EnumerateArray().Select(element => element.GetRawText()).ToList();

        }
    }
}
