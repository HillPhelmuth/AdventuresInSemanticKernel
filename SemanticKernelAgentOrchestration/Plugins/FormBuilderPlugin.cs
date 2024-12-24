using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using SemanticKernelAgentOrchestration.Models;

namespace SemanticKernelAgentOrchestration.Plugins;

public class FormBuilderPlugin
{
    [KernelFunction, Description("Save field data to form")]
    public string SaveFieldData(Kernel kernel,[Description("The form field object including the FormValue")] FormField field)
    {
        
        var chatContext = kernel.GetRequiredService<ChatContext>();
        var activeForm = chatContext.ActiveForm;
        if (activeForm == null)
        {
            return "No active form found";
        }
        var formField = activeForm.Fields.FirstOrDefault(x => x.FieldName == field.FieldName);

        if (formField == null)
        {
            return "Field not found in active form";
        }
        activeForm.Fields.Remove(formField);
        activeForm.Fields.Add(field);
        Console.WriteLine($"Saved field data.\n{JsonSerializer.Serialize(activeForm.Fields.FirstOrDefault(x => x.FieldName == field.FieldName))}\n Updated fields: {JsonSerializer.Serialize(activeForm.Fields, new JsonSerializerOptions(){WriteIndented = true})}");
        return $"Field data for {field.FieldName} saved";
    }

    [KernelFunction, Description("Start a new form or request the next form")]
    public string GetNextForm(Kernel kernel)
    {
        var chatContext = kernel.GetRequiredService<ChatContext>();
        var activeForm = chatContext.ActiveForm;
        if (activeForm != null)
        {
            return "Please complete the current form before starting a new one";
        }
        chatContext.ActiveForm = chatContext.ActiveForms.FirstOrDefault();
        return JsonSerializer.Serialize(chatContext.ActiveForm!.Fields, new JsonSerializerOptions() { WriteIndented = true });
    }
    [KernelFunction, Description("Validate and submit active form")]
    public string ValidateAndSubmitForm(Kernel kernel)
    {
        var chatContext = kernel.GetRequiredService<ChatContext>();
        var activeForm = chatContext.ActiveForm;
        if (activeForm.Fields.Any(x => x.FieldValue == null))
        {
            return "Please fill out all fields before submitting";
        }
        chatContext.CompletedForms.Add(activeForm);
        if (chatContext.ActiveForms.Count == 0)
        {
            chatContext.ActiveForm = null;
            return "Form submitted!";
        }
        chatContext.ActiveForms.RemoveAt(0);
        chatContext.ActiveForm = null;
        return "Form submitted, request next form";
    }
    [KernelFunction]
    public string FormFields(Kernel kernel)
    {
        var chatContext = kernel.GetRequiredService<ChatContext>();
        var activeForm = chatContext.ActiveForm;
        return activeForm == null ? "No active form found" : JsonSerializer.Serialize(activeForm.Fields, new JsonSerializerOptions(){WriteIndented = true});
    }
    [KernelFunction]
    public string FormDescription(Kernel kernel)
    {
        var chatContext = kernel.GetRequiredService<ChatContext>();
        var activeForm = chatContext.ActiveForm;
        return activeForm == null ? "No active form found" : $"""
                                                              {activeForm.FormName}
                                                              {activeForm.FormDescription}
                                                              """;
    }
    [KernelFunction]
    public string GetFormList(Kernel kernel)
    {
        var chatContext = kernel.GetRequiredService<ChatContext>();
        return string.Join("\n", chatContext.ActiveForms.Select(x => $"- {x.FormName}"));
    }
}