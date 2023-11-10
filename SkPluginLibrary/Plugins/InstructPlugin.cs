using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.TemplateEngine.Basic;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI;

namespace SkPluginLibrary.Plugins
{
    public class InstructPlugin
    {
        private readonly IKernel _kernel;
        public InstructPlugin(IKernel kernel)
        {
            _kernel = kernel;
        }

        private const string FunctionPrompt = """
            You are a generative AI chatbot instruction prompt generator.
            For this chatbot the user input will be used to find relevant [related content] that is up-to-date so chatbot doesn't have to rely on training data.
            As the instruction prompt generator, you are responsible for providing a short and succinct instruction that defines the [related content] in a way that is related to the user input and functions as a semantic bridge between them.

            You will be provided with the original user input and the [related content]. Your response should only be the instruction.

            [user input]
            {{$input}}

            [related content]
            {{$related}}
            """;
        [SKFunction, Description("Generates an instruction prompt that semantically bridges the gap between user input and relevant content")]
        public async Task<string> Bridge(string input, string related)
        {
            var ctx = _kernel.CreateNewContext();
            ctx.Variables["input"] = input;
            ctx.Variables["related"] = related;
            var function = _kernel.CreateSemanticFunction(FunctionPrompt, requestSettings: new OpenAIRequestSettings { Temperature = 0.0, TopP = 0.0, MaxTokens = 256 });
            var kernelResult = await _kernel.RunAsync(ctx.Variables, function);
            var result = kernelResult.Result();
            Console.WriteLine($"Bridge function generated instruction: '{result}'");
            return result;
        }
    }
}
