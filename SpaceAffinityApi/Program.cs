using Npgsql;
using SpaceAffinityApi.Context;
using SpaceAffinityApi.Domain;
using SpaceAffinityApi.Messages.Commands.SpaceNotes;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("Default");

// Manually build the slim data source
var dataSourceBuilder = new NpgsqlSlimDataSourceBuilder(connectionString);
var slimDataSource = dataSourceBuilder.Build();

// Register the built NpgsqlDataSource as a Singleton
builder.Services.AddSingleton(slimDataSource);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

builder.Services.AddSingleton(SpaceNoteMap.Instance);
builder.Services.AddScoped<IPlainSqlContext<SpaceNote>, PostgresPlainSqlContext<SpaceNote>>();

builder.Services.AddHealthChecks();

var app = builder.Build();

// Create the table on startup if it doesn't exist (guarded by an advisory lock, so multiple pods are safe).
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<IPlainSqlContext<SpaceNote>>().EnsureTableExists();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Kubernetes uses this to check the API is alive and ready for traffic
app.MapHealthChecks("/healthz");

var api = app.MapGroup("/api");

api.MapGet("/spacenotes", async (IPlainSqlContext<SpaceNote> datasource, int? page, int? itemsPerPage) =>
{
    var pageNumber = Math.Max(page ?? 1, 1);
    var count = Math.Clamp(itemsPerPage ?? 100, 1, 100);
    return await datasource.GetData((pageNumber - 1) * count, count);
});
   

api.MapPost("/spacenotes", async (AddSpaceNote request, IPlainSqlContext<SpaceNote> datasource) =>
{
    if (string.IsNullOrEmpty(request.PictureUrl) || string.IsNullOrEmpty(request.Description))
    {
        return Results.BadRequest();
    }

    var note = new SpaceNote { Description = request.Description, PictureUrl = request.PictureUrl};
    var id = await datasource.UpsertItem(note);
    note.Id = id;
    return Results.Created($"/api/spacenotes/{id}", note);
});

app.Run();