using System;
using System.Threading.Tasks;
using Hangfire.SqlServer;
using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(Hangfire.Highlighter.Startup))]

namespace Hangfire.Highlighter
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.UseHangfire(config =>
            {
                config.UseSqlServerStorage("HighlighterDb");
                config.UseServer();
            });
        }
    }
}
