using Microsoft.AspNet.SignalR;
using Microsoft.Owin;
using Microsoft.Owin.Cors;
using Microsoft.Owin.Security.DataHandler.Encoder;
using Microsoft.Owin.Security.Jwt;
using Owin;
using System;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Web.Http;
using System.Web.Http.Cors;

[assembly: OwinStartup(typeof(MessageServer.Startup))]

namespace MessageServer
{
    public class Startup
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
            ConfiguraeAuthZero(app);
            var webApiConfiguration = ConfigureWebApi();
            app.UseWebApi(webApiConfiguration);
            app.UseCors(CorsOptions.AllowAll);

            // Make long polling connections wait a maximum of 110 seconds for a
            // response. When that time expires, trigger a timeout command and
            // make the client reconnect.
            GlobalHost.Configuration.ConnectionTimeout = TimeSpan.FromSeconds(40);
            // Wait a maximum of 30 seconds after a transport connection is lost
            // before raising the Disconnected event to terminate the SignalR connection.
            GlobalHost.Configuration.DisconnectTimeout = TimeSpan.FromSeconds(30);
            // For transports other than long polling, send a keepalive packet every
            // 10 seconds. 
            // This value must be no more than 1/3 of the DisconnectTimeout value.
            GlobalHost.Configuration.KeepAlive = TimeSpan.FromSeconds(10);
            //Setting up the message buffer size
            GlobalHost.Configuration.DefaultMessageBufferSize = 500;

        }



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


        private HttpConfiguration ConfigureWebApi()
        {
            var config = new System.Web.Http.HttpConfiguration();
            var enableCorsAttribute = new EnableCorsAttribute("*", "*", "*");
            config.EnableCors(enableCorsAttribute);
            config.Formatters.JsonFormatter.SupportedMediaTypes.Add(new MediaTypeWithQualityHeaderValue("text/html")
    );
            config.Formatters.Remove(config.Formatters.XmlFormatter);
            config.Formatters.JsonFormatter.AddUriPathExtensionMapping("json", "application/json");

            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            return config;
        }

    }
}