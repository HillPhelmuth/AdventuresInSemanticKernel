using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace SemanticKernelAgentOrchestration.Models
{
    public class ChatContext
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string? UserId { get; set; } // Partition key
		public List<ChatMessageContent> ChatMessages { get; set; } = [];
        public ChatAgent? ActiveAgent { get; set; }
        public bool IsTranstionNext { get; set; }
        public bool IsIntentTranstion { get; set; }
        public List<FormData> ActiveForms { get; set; } = [];
        public List<FormData> CompletedForms { get; set; } = [];
        public FormData? ActiveForm { get; set; }
    }

    public abstract class FormData
    {
        [JsonConstructor]
        protected FormData(string formName, string formDescription)
        {
            FormName = formName;
            FormDescription = formDescription;
        }
        public string FormName { get; set; }
        public string FormDescription { get; set; }
        public abstract List<FormField> Fields { get; }
    }

    public class ContactForm() : FormData("Contact Form", "Form for collecting contact information")
    {
        private List<FormField> _startField = [
            new FormField { FieldName = "Name", FieldDescription = "Your Name", FieldType = "string" },
            new FormField
                { FieldName = "Email", FieldDescription = "Your Email", FieldType = "string" },
            new FormField
                { FieldName = "Phone", FieldDescription = "Your Phone", FieldType = "string" }
        ];

        private List<FormField>? _fields;
        public override List<FormField> Fields => _fields ?? _startField;

        /*return
           [
               new FormField { FieldName = "Name", FieldDescription = "Your Name", FieldType = "string" },
               new FormField
                   { FieldName = "Email", FieldDescription = "Your Email", FieldType = "string" },
               new FormField
                   { FieldName = "Phone", FieldDescription = "Your Phone", FieldType = "string" }
           ];*/
    }

    public class PersonalInfoForm() : FormData("Personal Info Form", "Form for collecting personal information")
    {
        private List<FormField> _fields;
        private List<FormField> _startFields = [
            new FormField { FieldName = "Age", FieldDescription = "Your Age", FieldType = "int" },
            new FormField
                { FieldName = "DOB", FieldDescription = "Your Birthday", FieldType = "DateTime" },
            new FormField
                { FieldName = "FavoriteColor", FieldDescription = "Your favorite color", FieldType = "string" }
        ];
        public override List<FormField> Fields => _fields ?? _startFields;

    }
    [TypeConverter(typeof(GenericTypeConverter<FormField>))]
    public class FormField
    {
        public string? FieldName { get; set; }
        public string? FieldDescription { get; set; }
        public string? FieldType { get; set; }
        public object? FieldValue { get; set; }
    }
    internal class GenericTypeConverter<T> : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => true;

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            Console.WriteLine($"Converting {value} to {typeof(T)}");
            return JsonSerializer.Deserialize<T>((string)value);
        }
        public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        {
            Console.WriteLine($"Converting {typeof(T)} to {value}");
            return JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
