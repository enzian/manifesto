using dotnet_etcd;
using dotnet_etcd.interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Manifesto.AspNet.Controllers;
using Manifesto.AspNet.Manifests;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace Manifesto.AspNet;

public static class WebApplicationBuilderExtensions
{
    public static IServiceCollection AddManifesto(this IServiceCollection services)
    {
        services.AddSingleton<IEtcdClient>(new EtcdClient("http://localhost:2379"));

        var assembly = typeof(ManifestV1CreationController).Assembly;
        services
            .AddControllers(options =>
            {
                options.InputFormatters.Insert(0, MyJPIF.GetJsonPatchInputFormatter());
            })
            // .AddNewtonsoftJson()
            .PartManager.ApplicationParts.Add(new AssemblyPart(assembly));
        services.AddKeySpaces((string kind, string version, string group) => $"{group}/{version}/{kind}");


        return services;
    }

    public static IServiceCollection AddKeySpaces(this IServiceCollection subject, Func<string, string, string, string> keyspaceFunction)
    {
        return subject.AddSingleton<IKeyspaceSource>(new DelegateKeyspaceSource(keyspaceFunction));
    }

    public static void MapManifestApiControllers(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapControllers();
    }

    public static class MyJPIF
    {
        public static NewtonsoftJsonPatchInputFormatter GetJsonPatchInputFormatter()
        {
            var builder = new ServiceCollection()
                .AddLogging()
                .AddMvc()
                .AddNewtonsoftJson()
                .Services.BuildServiceProvider();

            return builder
                .GetRequiredService<IOptions<MvcOptions>>()
                .Value
                .InputFormatters
                .OfType<NewtonsoftJsonPatchInputFormatter>()
                .First();
        }
    }
}

