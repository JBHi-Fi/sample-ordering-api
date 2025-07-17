using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Jbh.SampleOrderingApi.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddHttpClient();
        services.AddScoped<MockExternalServices>();
        services.AddLogging();
    })
    .Build();

host.Run();
