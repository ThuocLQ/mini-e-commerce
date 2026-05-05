var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

//ReverseProxy
builder.Services
    .AddReverseProxy()// dang ky YARP
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy")); // get route/cluster 

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    service = "ApiGateway",
    version = "v1",
    status = "running"
}));

// Configure the HTTP request pipeline.

app.MapReverseProxy();

app.Run();