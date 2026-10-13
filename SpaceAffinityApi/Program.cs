using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using SpaceAffinityApi.Context;
using SpaceAffinityApi.Domain;
using SpaceAffinityApi.Messages.Commands.Security;
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

var jwtKey = builder.Configuration["Authentication:JwtKey"]!;
var issuer = builder.Configuration["Authentication:JwtIssuer"]!;
var audience = builder.Configuration["Authentication:JwtAudience"]!;
var username = builder.Configuration["Authentication:Username"]!;
var password = builder.Configuration["Authentication:Password"]!;

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

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
   

api.MapPost("/spacenotes", [Authorize] async (AddSpaceNote request, IPlainSqlContext<SpaceNote> datasource) =>
{
    if (string.IsNullOrEmpty(request.PictureUrl) || string.IsNullOrEmpty(request.Description))
    {
        return Results.BadRequest();
    }

    var isEdit = request.Id != 0;
    var note = new SpaceNote { Id = request.Id, Description = request.Description, PictureUrl = request.PictureUrl};
    var id = await datasource.UpsertItem(note);
    if (id == -1)
    {
        // Edits only update existing rows, so an unknown id matched nothing
        return Results.NotFound();
    }

    note.Id = id;
    return isEdit ? Results.Ok(note) : Results.Created($"/api/spacenotes/{id}", note);
});

api.MapPost("/login", (LoginCommand command) =>
{
    // Replace with your real user verification database logic
    if (command.Username == username && command.Password == password)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(jwtKey);
        var expiresAt = DateTime.UtcNow.AddHours(1);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, command.Username),
                new Claim(ClaimTypes.Role, "Admin")
            }),
            Expires = expiresAt,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        return Results.Ok(new LoginResponse(tokenString, expiresAt));
    }

    return Results.Unauthorized();
});

app.Run();