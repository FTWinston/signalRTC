using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Signal.Middleware;
using Signal.Models;
using Signal.Services;
using System;

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

            WebSocketOptions socketOptions = CreateWebSocketOptions();

            app.UseWebSockets(socketOptions);

            app.UseMiddleware<WebSocketMiddleware>(socketOptions);

            app.UseDefaultFiles();

            app.UseStaticFiles();
        }

        private WebSocketOptions CreateWebSocketOptions()
        {
            var socketOptions = new WebSocketOptions();

            try
            {
                var keepAliveInterval = Configuration.GetValue<int>("KeepAliveInterval");
                socketOptions.KeepAliveInterval = TimeSpan.FromSeconds(keepAliveInterval);
            }
            catch { }

            try
            {
                var allowedOrigins = Configuration.GetValue<string[]>("AllowedOrigins");
                foreach (var origin in allowedOrigins)
                {
                    socketOptions.AllowedOrigins.Add(origin);
                }
            }
            catch { }

            return socketOptions;
        }
    }
}
