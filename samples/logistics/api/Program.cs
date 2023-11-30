using System.Security.Cryptography;

using Manifesto.AspNet;
using Manifesto.AspNet.FSharp;

var builder = WebApplication.CreateBuilder(args);

builder.AddManifestoV1();

// builder.Services.AddManifesto()
//     .AddKeySpaces((string kind, string version, string group) => {
//         return  (kind, group, version) switch {
//             ("stock", "logistics.stockr.io", "v1alpha1") => $"/registry/stocks",
//             ("stocks", "logistics.stockr.io", "v1alpha1") => $"/registry/stocks",
//             ("location", "logistics.stockr.io", "v1alpha1") => $"/registry/locations",
//             ("locations", "logistics.stockr.io", "v1alpha1") => $"/registry/locations",
//             _ => string.Empty
//         };
// });

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
