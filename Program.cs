using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LeccionesAprendidas.Services;
using LeccionesAprendidas.Models;
using FluentValidation;
using LeccionesAprendidas.Validations;

var host = new HostBuilder()
    .ConfigureAppConfiguration(config =>
    {
        config.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);
        config.AddEnvironmentVariables();
    })
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        // Registro de opciones desde la configuración
        services.Configure<OpenAIOptions>(context.Configuration.GetSection("OpenAI"));
        services.Configure<CosmosDbOptions>(context.Configuration.GetSection("CosmosDb"));
        services.Configure<AzureSearchOptions>(context.Configuration.GetSection("AzureSearch"));

        // Registro de servicios principales
        services.AddSingleton<OpenAIService>();
        services.AddSingleton<CosmosDbService>();
        services.AddSingleton<SearchService>();

        //Registro de Validaciones
        services.AddScoped<IValidator<CreateLessonRequest>, CreateLessonRequestValidator>();
        services.AddScoped<IValidator<SearchLessonRequest>, SearchLessonRequestValidator>();

    })
    .Build();

// Inicializar el índice de búsqueda
var searchService = host.Services.GetRequiredService<SearchService>();
await searchService.CreateIndexIfNotExistsAsync();

await host.RunAsync();