# Manifesto

Manifesto allows you to build statefull applications. It allows developers to build K8S like APIs for their systems that handle the state and it's changes and allows components like controllers to track changes in real time. It leverages ETCD as a distirbuted KV store and in order to provide change streams when watching changes.

The API Server can be implemented simply by installing the NuGet package with the following startup code:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddManifesto()
    .AddKeySpaces((string kind, string version, string group) => {
        return  (kind, group, version) switch {
            ("stock", "logistics.stockr.io", "v1alpha1") => $"/registry/stocks",
            ("stocks", "logistics.stockr.io", "v1alpha1") => $"/registry/stocks",
            ("location", "logistics.stockr.io", "v1alpha1") => $"/registry/locations",
            ("locations", "logistics.stockr.io", "v1alpha1") => $"/registry/locations",
            _ => string.Empty
        };
});

var app = builder.Build();
app.MapManifestApiControllers();
await app.RunAsync();
```

Running the example allows the user to manage two kings of resources: `locations` and `stocks`. Manifesto does not prescribe the schema of these two resource types and leaves this up to the user of the API. All resources types are treated as schemaless by Manifesto.

# Build and run

To build the repository just run:

```
dotnet build
```

Then pick a sample API server you want to run from the `samples/` directory and run the API server

```
cd ./samples/logistics/api/
dotnet run
```

The API server should now start listeing on port 5000. This can vary depending on the availability of ports on your machine.

The API can be used directly by using a tool like Postman. There is also samples of controllers that fetch and watch state in order to reconcile it.

While most samples are written in C# or F#, Manifesto can be used by any language to implement statefull applications.
