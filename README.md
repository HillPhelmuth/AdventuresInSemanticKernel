# Adventures in Semantic Kernel

## [Extensive Interactive Demo of the Semantic Kernel SDK](https://adventuresinsemantickernel.azurewebsites.net/)

Welcome to **Adventures in Semantic Kernel**, your interactive guide to exploring the functionalities of Microsoft's AI Orchestration library, Semantic Kernel. Dive into hands-on experiences ranging from dynamic plan generation and Agent building for a dynamic chat experience to memory management and tokenization. This isn't just a passive learning experience; you'll get to actively experiment with these features to understand their cohesive interactions. [Try it out here](https://adventuresinsemantickernel.azurewebsites.net/)

## About Semantic Kernel

Originally developed by Microsoft, [Semantic Kernel](https://learn.microsoft.com/en-us/semantic-kernel/overview/) aims to democratize AI integration for developers. While the project benefits from open-source contributions, its core mission is to simplify the integration of AI services with app code. It comes equipped with a smart set of connectors that essentially act as your app's "virtual brain", capable of executing LLM prompts, native code or external REST Apis.

## Configurations

Current configuration will work for all the main features of the demo, and for most (though not all) plugins. However, several [KernelSyntaxExamples](https://github.com/microsoft/semantic-kernel/tree/main/dotnet/samples/KernelSyntaxExamples) will require config values for specific resources (e.g. Pinecone, Chroma, Weaviate, etc.) not available by default. Any service config highlighted in red is missing values that will need to be added for the associated sample to work.

![Config Image](/Images/ConfigImage.jpeg)

You don't need to supply an OpenAI api key for most of the demo features, but if you want to use a gpt-4 model (or if you want to change the default service to Azure OAI), you will need to supply an api key in the `OpenAIConfig` or `AzureOpenAIConfig` section.
_**Note:**_ All configurations added/changed are encrypted and saved to your browser's local storage so they can be loaded across sessions while remaining secure.


## Application features

### Samples
View, modify, and execute dotnet examples. Examples are from [KernelSyntaxExamples](https://github.com/microsoft/semantic-kernel/tree/main/dotnet/samples/KernelSyntaxExamples) with small modifications.

![Samples Image](/Images/SkSamples.jpeg)

### Execute Function
Select a single plugin from a large variety of native, prompt and external plugins, then execute a function from that plugin.

![Function Image](/Images/ExecuteFunction.jpeg)

### Build Agent
Build a simple agent by providing a persona and collection of plugins used together with OpenAI Function Calling.

![Function Image](/Images/AgentBuilder.jpeg)

### Build Planner
Select plugins and functions to build and execute your own:
  - OpenAI Function Calling Agent
  - Handlebars planner
  - Stepwise planner

![Planner Image](/Images/BuildPlanner.jpeg)

### Custom Examples

#### Web Chat Agent
Chat with the web using _Bing_ search and a scrape-and-summarize plugin

![WebChat Image](/Images/WebChat.png)

#### Wikipedia Chat Agent
Chat with the Wikipedia articles using _Wikipedia_ Rest API

#### C# REPL Agent
Use natural language prompts to generate and execute c# code
 - Generate and execute a c# console application using prompts.
 - Generate and execute c# line-by-line using [Roslyn c# scripting api](https://github.com/dotnet/roslyn/blob/main/docs/wiki/Scripting-API-Samples.md).

 ![Repl Image](/Images/Repl.jpeg)

#### Dnd Story Agent
Example of a Stepwise Planner at work. Planner has access to the [D&D5e Api](https://www.dnd5eapi.co/) plugin and multiple prompt plugins. It uses these to create and execute a plan to generate a short story.
 - Leverages a native plugin from a Razor Class Library `AskUserPlugin` to provide user interaction during plan execution

### SK Memory

#### Vector Playground
Play around with embeddings and similarities using your own or generated text snippets

#### SK + Custom Hdbscan Clustering
See how embeddings can be used to cluster text items, and then generate a title and summmary for each cluster using prompt plugins

### Tokens

#### Chunking and Tokenization
 - Generate or add text, set the text chunking parameters, and then see the Semantic Kernel `TextChunker` work
 - Search over chunked text to see how the `TextChunker` can be used to improve search results

#### Tinker with Tokens
See how input text translates into tokens. Select specific tokens to set the LogitBias for a chat completion request/response.
