using MLCalc.Web.Client;
using MLCalc.Web.Ui;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.Services.AddSingleton<IMLCalcSession, MLCalcConnection>();
await builder.Build().RunAsync();
