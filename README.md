# Adventures in Semantic Kernel

Welcome to **Adventures in Semantic Kernel**, your interactive guide to exploring the functionalities of Microsoft's AI Orchestration library, Semantic Kernel. Dive into hands-on experiences ranging from memory management and plan execution, to chaining functions for a dynamic chat experience. This isn't just a passive learning experience; you'll get to actively experiment with these features to understand their cohesive interactions. [Try it out here](https://adventuresinsemantickernel.azurewebsites.net/)

## About Semantic Kernel

Originally developed by Microsoft, [Semantic Kernel](https://learn.microsoft.com/en-us/semantic-kernel/overview/) aims to democratize AI integration for developers. While the project benefits from open-source contributions, its core mission is to simplify AI services deployment in apps. It comes equipped with a smart set of connectors that essentially act as your app's "virtual brain", capable of storing and processing information.

## Application features

### Samples
View, modify, and execute dotnet examples from the [Semantic Kernel Repo](https://github.com/microsoft/semantic-kernel/tree/main/dotnet/samples/KernelSyntaxExamples)

### Execute Function
Select a single plugin from a large variety of native, semantic and external plugins, then execute a function from that plugin.

### Build Planner
Select plugins and functions to build and execute your own:
  - Action planner
  - Sequential planner
  - Kernel execution chain

### Custom Examples

#### Web Chat
Chat with the web using _Bing_

#### Wikipedia Chat
Chat with the web using _Wikipedia_

#### C# REPL Plugin
Use natural language prompts to generate and execute c# code
 - Generate and execute a c# console application using prompts.
 - Generate and execute c# line-by-line using [Roslyn c# scripting api](https://github.com/dotnet/roslyn/blob/main/docs/wiki/Scripting-API-Samples.md).

#### Dnd Story Gen
Example of a Sequential Planner at work. Planner has access to the [D&D5e Api](https://www.dnd5eapi.co/) plugin and multiple semantic plugins. It uses these to create and execute a plan to generate a short story. 

### SK Memory

#### Vector Playground
Play around with embeddings and similarities using your own or generated text snippets

#### SK + Custom Hdbscan Clustering
See how embeddings can be used to cluster text items, and then generate a title and topic list for each cluster using semantic plugins

### Tokens

#### Chunking and Tokenization
Generate or add text, set the text chuncking paramters, and then see the Semantic Kernel `TextChunker` work

#### Tinker with Tokens
Description: See how input text translates into tokens. Select specific tokens to set the LogitBias for a chat completion request/response.
