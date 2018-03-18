using Microsoft.AspNet.SignalR;
using Microsoft.Owin;
using Microsoft.Owin.Cors;
using Microsoft.Owin.Security.DataHandler.Encoder;
using Microsoft.Owin.Security.Jwt;
using Owin;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Web.Http;

[assembly: OwinStartup(typeof(SignalRServer.Startup))]

namespace SignalRServer
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.Map("/signalr", map =>
            {
                map.UseCors(CorsOptions.AllowAll);
                var hubConfiguration = new HubConfiguration { };
                hubConfiguration.EnableDetailedErrors = true;
                map.RunSignalR(hubConfiguration);
            });

        }

     

    }

    //auth0
    public partial class Startup
    {
        private void ConfiguraeAuthZero(IAppBuilder app)
        {
            var issuer = "https://lab021.auth0.com/";
            var audience = "yUnhC31cajBLHwtZeLs9X2hLCsMQPtFV";
            var secret = TextEncodings.Base64Url.Decode("6BREytYCL5_WvQts_D697-vkwOmq0ClR6roKJAYUMktanY69xeeSsWDqtY2YVAQ8");

            // Api controllers with an [Authorize] attribute will be validated with JWT
            app.UseJwtBearerAuthentication(
                new JwtBearerAuthenticationOptions
                {
                    AuthenticationMode = Microsoft.Owin.Security.AuthenticationMode.Active,
                    AllowedAudiences = new[] { audience },
                    IssuerSecurityKeyProviders = new[]
                    {
                        new SymmetricKeyIssuerSecurityKeyProvider(issuer, secret)
                    },
                });
        }
    }
}