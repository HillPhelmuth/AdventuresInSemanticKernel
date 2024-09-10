namespace SkPluginLibrary.Models.Helpers
{
    public static class KernelBuilderExtensions
    {
        // ReSharper disable once InconsistentNaming
        public static IKernelBuilder AddAIChatCompletion(this IKernelBuilder kernelBuilder)
        {
            var isAzure = TestConfiguration.CoreSettings!.Service == "AzureOpenAI";
            if (isAzure && (string.IsNullOrEmpty(TestConfiguration.CoreAISettings.Gpt35DeploymentName) || string.IsNullOrEmpty(TestConfiguration.CoreAISettings.Endpoint)))
            {
                Console.WriteLine("Azure endpoint and deployment name are required");
                return kernelBuilder;
            }

            if (!string.IsNullOrEmpty(TestConfiguration.CoreAISettings.Gpt35ModelId) &&
                !string.IsNullOrEmpty(TestConfiguration.CoreAISettings.ApiKey))
                return isAzure
                    ? kernelBuilder.AddAzureOpenAIChatCompletion(TestConfiguration.CoreAISettings!.Gpt35DeploymentName!,
                        TestConfiguration.CoreAISettings.Endpoint, TestConfiguration.CoreAISettings.ApiKey,
                        modelId: TestConfiguration.CoreAISettings.Gpt35ModelId)
                    : kernelBuilder.AddOpenAIChatCompletion(TestConfiguration.CoreAISettings!.Gpt35ModelId,
                        TestConfiguration.CoreAISettings.ApiKey);
            Console.WriteLine("ModelId and ApiKey are required");
            return kernelBuilder;
        }
        // ReSharper disable once InconsistentNaming
        public static IKernelBuilder AddAIChatCompletion(this IKernelBuilder kernelBuilder, AIModel aIModel)
        {
            var isAzure = TestConfiguration.CoreSettings!.Service == "AzureOpenAI";
            var modelName = "";
            string? modelOrDeploymentName;
            if (isAzure)
            {
                modelOrDeploymentName = aIModel == AIModel.Gpt4OMini ? TestConfiguration.AzureOpenAI.Gpt35DeploymentName : TestConfiguration.AzureOpenAI.Gpt4DeploymentName;
                modelName = aIModel.GetOpenAIModelName(isAzure);
            }
            else
            {
                modelOrDeploymentName = aIModel.GetOpenAIModelName(isAzure);
            }
            if (isAzure && (string.IsNullOrEmpty(TestConfiguration.CoreAISettings.Gpt35DeploymentName) || string.IsNullOrEmpty(TestConfiguration.CoreAISettings.Endpoint)))
            {
                Console.WriteLine("Azure endpoint and deployment name are required");
                return kernelBuilder;
            }

            if (!string.IsNullOrEmpty(modelOrDeploymentName) &&
                !string.IsNullOrEmpty(TestConfiguration.CoreAISettings.ApiKey))
                return isAzure switch
                {
                    true when string.IsNullOrEmpty(modelName) => kernelBuilder,
                    true => kernelBuilder.AddAzureOpenAIChatCompletion(modelOrDeploymentName,
                        TestConfiguration.CoreAISettings!.Endpoint,
                        TestConfiguration.CoreAISettings.ApiKey, modelId: modelName),
                    _ => kernelBuilder.AddOpenAIChatCompletion(modelOrDeploymentName,
                        TestConfiguration.CoreAISettings!.ApiKey)
                };
            Console.WriteLine("ModelId and ApiKey are required");
            return kernelBuilder;
        }
    }
}
