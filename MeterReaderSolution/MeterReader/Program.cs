#define cert

using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureHttpsDefaults(
        httpCtx => httpCtx.ClientCertificateMode = ClientCertificateMode.AllowCertificate);
});

RegisterServices(builder);

var app = builder.Build();

SetupMiddleware(app);
SetupApi(app);

app.Run();

static void SetupApi(WebApplication app)
{
    // Token Generation
    app.MapPost("/api/token", async Task<IResult>(CredentialModel model, JwtTokenValidationService tokenService) =>
    {
        var result = await tokenService.GenerateTokenModelAsync(model);

        if (result.Success)
        {
            return Results.Created("", new { token = result.Token, expiration = result.Expiration });
        }

        return Results.BadRequest();
    }).AllowAnonymous();

    // REST API
    app.MapGet("/api/customers", async Task<IResult> (IReadingRepository repo) =>
    {
        var result = await repo.GetCustomersWithReadingsAsync();

        return Results.Ok(result);
    });

    app.MapGet("/api/customers/{id:int}", async Task<IResult> (int id, IReadingRepository repo) =>
    {
        var result = await repo.GetCustomerWithReadingsAsync(id);

        return Results.Ok(result);
    });
}

static void SetupMiddleware(WebApplication webApp)
{
    // Configure the HTTP request pipeline.
    if (webApp.Environment.IsDevelopment())
    {
        webApp.UseMigrationsEndPoint();
    }
    else
    {
        webApp.UseExceptionHandler("/Error");
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        webApp.UseHsts();
        webApp.UseHttpsRedirection();
    }

    webApp.UseStaticFiles();

    webApp.UseRouting();

    webApp.UseCors();

    webApp.UseAuthentication();
    webApp.UseAuthorization();

    webApp.UseGrpcWeb();

    webApp
     .MapGrpcService<MeterReader.Services.MeterReadingService>()
     .EnableGrpcWeb()
     .RequireCors("AllowAll");


    webApp.MapRazorPages();

}


static void RegisterServices(WebApplicationBuilder bldr)
{
    bldr.Services.AddScoped<JwtTokenValidationService>();
    bldr.Services
        .AddAuthentication()
        .AddJwtBearer(cfg =>
        {
            cfg.TokenValidationParameters = new MeterReaderTokenValidationParameters(bldr.Configuration);
        }).AddCertificate(opt =>
        {
            opt.AllowedCertificateTypes = CertificateTypes.All;
            opt.RevocationMode = X509RevocationMode.NoCheck; // Self-Signed
            opt.Events = new CertificateAuthenticationEvents
            {
#if cert
                OnCertificateValidated = ctx =>
                {
                    if (ctx.ClientCertificate.Issuer == "CN=MeterRootCert")
                    {
                        ctx.Success();
                        return Task.CompletedTask;
                    }
                    ctx.Fail("Invalid Certificate Issuer");
#endif
                    return Task.CompletedTask;
                }
            };
        });

    bldr.Services.AddCors(cfg =>
    {
        cfg.AddDefaultPolicy(opt =>
        {
            opt.AllowAnyOrigin();
            opt.AllowAnyMethod();
            opt.AllowAnyHeader();
        });
    });
  
    var connectionString = bldr.Configuration.GetConnectionString("DefaultConnection");
    bldr.Services.AddDbContext<ReadingContext>(options =>
        options.UseSqlServer(connectionString));
    bldr.Services.AddDatabaseDeveloperPageExceptionFilter();

    bldr.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
        .AddEntityFrameworkStores<ReadingContext>();

    bldr.Services.AddScoped<IReadingRepository, ReadingRepository>();

    bldr.Services.AddRazorPages();

    bldr.Services.AddGrpc(cfg => cfg.EnableDetailedErrors = true);
}
