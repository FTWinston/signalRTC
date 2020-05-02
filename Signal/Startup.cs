using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Signal.Middleware;
using Signal.Models;
using Signal.Services;

namespace Signal
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSignalR();

            services.Configure<IdentifierConfiguration>
            (
                options => Configuration
                    .GetSection("Identifiers")
                    .Bind(options)
            );

            services.AddHttpContextAccessor();
            services.AddSingleton<IdentifierService>();
            services.AddSingleton<SessionService>();
            services.AddScoped<ConnectionService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            var socketOptions = new WebSocketOptions();
#if !DEBUG
            foreach (var origin in Configuration.GetValue<string[]>("AllowedOrigins"))
                socketOptions.AllowedOrigins.Add(origin);
#endif
            app.UseWebSockets(socketOptions);

            app.UseMiddleware<WebSocketMiddleware>(socketOptions, app.ApplicationServices.GetService<ConnectionService>());

            app.UseDefaultFiles();

            app.UseStaticFiles();
        }
    }
}
