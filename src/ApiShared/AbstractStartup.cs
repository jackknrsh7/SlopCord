using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ApiShared;

public abstract class AbstractStartup(IConfiguration configuration)
{
    public abstract string ApiName { get; }

    public IConfiguration Config { get; protected set; } = configuration;

    public virtual void ConfigureServices(IServiceCollection services)
    {
        try
        {
            AddBusiness(services);
        }
        catch (Exception ex)
        {
            HandleServiceError(ex, nameof(AddBusiness));
        }

        try
        {
            AddServices(services);
        }
        catch (Exception ex)
        {
            HandleServiceError(ex, nameof(AddServices));
        }
    }

    protected virtual void AddBusiness(IServiceCollection services) { }

    protected virtual void AddServices(IServiceCollection services)
    {
        services.AddOpenApi();
        services.AddControllers();
    }

    public virtual void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            if (env.IsDevelopment())
            {
                endpoints.MapOpenApi();
            }
            endpoints.MapControllers();
        });
    }

    public static void HandleServiceError(Exception ex, string context)
    {
        Console.Error.WriteLine($"[Startup] Fatal error in {context}: {ex}");
    }
}
