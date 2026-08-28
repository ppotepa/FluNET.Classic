using FluNET.Classic.Hosting;
using FluNET.Classic.LanguageServer;
using FluNET.Classic.Tooling;
using Microsoft.Extensions.DependencyInjection;

using ServiceProvider host = FluNetHost.Create();
ClassicDocumentService documents = host.GetRequiredService<ClassicDocumentService>();
var server = new LspServer(documents, Console.OpenStandardInput(), Console.OpenStandardOutput());
await server.RunAsync();
