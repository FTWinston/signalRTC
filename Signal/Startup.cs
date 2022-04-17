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

            services.AddSingleton(Configuration.Get<AppSettings>());
            services.AddSingleton(Configuration.GetSection("Identifiers").Get<IdentifierSettings>());

            services.AddHttpContextAccessor();
            services.AddSingleton<ProfanityFilter.ProfanityFilter>();
            services.AddSingleton<IdentifierService>();
            services.AddSingleton<SessionService>();
            services.AddScoped<ConnectionService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, AppSettings settings)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            WebSocketOptions socketOptions = CreateWebSocketOptions(settings);

            app.UseWebSockets(socketOptions);

            app.UseMiddleware<WebSocketMiddleware>();

            app.UseDefaultFiles();

            app.UseStaticFiles();
        }

        private WebSocketOptions CreateWebSocketOptions(AppSettings settings)
        {
            var socketOptions = new WebSocketOptions
            {
                KeepAliveInterval = TimeSpan.FromSeconds(settings.KeepAliveInterval)
            };

            if (settings.AllowedOrigins != null)
            {
                foreach (var origin in settings.AllowedOrigins)
                {
                    socketOptions.AllowedOrigins.Add(origin);
                }
            }

            return socketOptions;
        }
    }
}
