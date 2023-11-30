using dotnet_etcd;
using dotnet_etcd.interfaces;
using Manifesto.AspNet.FSharp;

var builder = WebApplication.CreateBuilder(args);

builder.AddManifestoV1();
builder.Services.AddSingleton<IEtcdClient>(new EtcdClient("http://localhost:2379"));

var x = (string kind, string version, string group) => {
        return  (kind, group, version) switch {
            ("stock", "logistics.stockr.io", "v1alpha1") => $"/registry/stocks",
            ("stocks", "logistics.stockr.io", "v1alpha1") => $"/registry/stocks",
            ("location", "logistics.stockr.io", "v1alpha1") => $"/registry/locations",
            ("locations", "logistics.stockr.io", "v1alpha1") => $"/registry/locations",
            _ => string.Empty
        };
    };

var app = builder.Build();

HostingExtensions.UseManifestoV1(app, x);

await app.RunAsync();
