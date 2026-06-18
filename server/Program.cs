using System.Text;
using ConnectoDb.Server.Hubs;
using ConnectoDb.Server.Services;
using dotenv.net;
using dotenv.net.Utilities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(opts =>
    {
        opts.WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "connecto db",
            ValidAudience = "connecto db",
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(EnvReader.GetStringValue("SECRET")))
        };

        // Allow SignalR hubs to authenticate via query string token
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    (path.StartsWithSegments("/data-stream") || path.StartsWithSegments("/collection-stream")))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddSignalR();
builder.Services.AddControllers();

// register IoC
builder.Services.AddScoped<AuthService>();

var app = builder.Build();

app.UseRouting();
app.UseCors();
// app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapHub<DataStreamHub>("/data-stream");
app.MapHub<CollectionStreamHub>("/collection-stream");

app.MapControllers();

app.Run();
