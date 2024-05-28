using connecto.server.Hubs;
using connecto.server.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(opts =>
    {
        opts.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .WithOrigins("http://localhost:4200");
    });
});
builder.Services.AddSignalR();
builder.Services.AddControllers();

// register IoC
builder.Services.AddScoped<AuthService>();

var app = builder.Build();

app.UseRouting();
app.UseCors();
// app.UseHttpsRedirection();
app.UseAuthorization();

app.MapHub<DataStreamHub>("/data-stream");
app.MapHub<CollectionStreamHub>("/collection-stream");

app.MapControllers();

app.Run();
