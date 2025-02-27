using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddLogging();
builder.Services
    .AddAuthorization()
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            RequireExpirationTime = false,
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,
            ValidateIssuerSigningKey = false,
            ValidIssuer = "yourIssuer",
            ValidAudience = "yourAudience",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["SecretKey"]!))
        };
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseExceptionHandler(configure =>
{
    configure.Run(async context =>
    {
        var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
        var exception = exceptionHandlerPathFeature?.Error;
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(exception, "An unhandled exception has occurred");
        var problemDetails = new ProblemDetails
        {
            Title = "An error occurred",
            Status = StatusCodes.Status500InternalServerError,
            Detail = exception?.Message,
        };
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(problemDetails);
    });
});

app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("HTTP Request: {method} {path}", context.Request.Method, context.Request.Path);

    await next();

    logger.LogInformation("HTTP Response: {statusCode}", context.Response.StatusCode);
});

var users = new List<User>();

app.MapPost("/generate-token", () =>
{
    var tokenHandler = new JwtSecurityTokenHandler();
    var key = Encoding.ASCII.GetBytes(builder.Configuration["SecretKey"]!);
    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "testuser")
        ]),
        Expires = DateTime.UtcNow.AddHours(12),
        Audience = "yourAudience",
        Issuer = "yourIssuer",
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
    };
    var token = tokenHandler.CreateToken(tokenDescriptor);
    var tokenString = tokenHandler.WriteToken(token);

    return Results.Ok(new { Token = tokenString });
});

var userGroup = app.MapGroup("/users").RequireAuthorization();

userGroup.MapGet("/", () => users);

userGroup.MapGet("/{id}", (int id) =>
{
    var user = users.FirstOrDefault(u => u.Id == id);
    return user is not null ? Results.Ok(user) : Results.NotFound($"User with id {id}, was not found!");
});

userGroup.MapPost("/", (User user) =>
{
    if (!ValidateUser(user, out var validationResults))
    {
        return Results.BadRequest(validationResults);
    }

    user.Id = users.Count + 1;
    users.Add(user);
    return Results.Created($"/users/{user.Id}", user);
});

userGroup.MapPut("/{id}", (int id, User updatedUser) =>
{
    var user = users.FirstOrDefault(u => u.Id == id);
    if (user is null) return Results.NotFound();

    if (!ValidateUser(updatedUser, out var validationResults))
    {
        return Results.BadRequest(validationResults);
    }

    user.FirstName = updatedUser.FirstName;
    user.LastName = updatedUser.LastName;
    user.Email = updatedUser.Email;
    user.Department = updatedUser.Department;

    return Results.NoContent();
});

userGroup.MapDelete("/{id}", (int id) =>
{
    var user = users.FirstOrDefault(u => u.Id == id);
    if (user is null) return Results.NotFound();

    users.Remove(user);
    return Results.NoContent();
});

app.Run();

static bool ValidateUser(User user, out List<ValidationResult> validationResults)
{
    var context = new ValidationContext(user);
    validationResults = [];
    return Validator.TryValidateObject(user, context, validationResults, true);
}