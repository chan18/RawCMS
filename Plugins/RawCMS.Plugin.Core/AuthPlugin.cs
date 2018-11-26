﻿using IdentityModel.AspNetCore.OAuth2Introspection;
using IdentityServer4.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RawCMS.Library.Core;
using RawCMS.Library.Core.Interfaces;
using RawCMS.Plugins.Core.Configuration;
using RawCMS.Plugins.Core.Extensions;
using RawCMS.Plugins.Core.Model;
using RawCMS.Plugins.Core.MVC;
using RawCMS.Plugins.Core.Stores;
using System.Security.Claims;

namespace RawCMS.Plugins.Core
{
    public class AuthPlugin : RawCMS.Library.Core.Extension.Plugin, IConfigurablePlugin<AuthConfig>
    {
        public override string Name => "Authorization";

        public override string Description => "Add authorizaton capabilities";

        public override void Init()
        {
            Logger.LogInformation("AuthPlugin plugin loaded");
        }

        private RawUserStore userStore = new RawUserStore();

        public override void ConfigureServices(IServiceCollection services)
        {
            Logger.LogInformation("AuthPlugin plugin ConfigureServices");

            base.ConfigureServices(services);

            

            services.AddSingleton<IUserStore<IdentityUser>>(x => { return userStore; });
            services.AddSingleton<IUserPasswordStore<IdentityUser>>(x => { return userStore; });
            services.AddSingleton<IPasswordValidator<IdentityUser>>(x => { return userStore; });
            services.AddSingleton<IUserClaimStore<IdentityUser>>(x => { return userStore; });
            services.AddSingleton<IPasswordHasher<IdentityUser>>(x => { return userStore; });
            services.AddSingleton<IProfileService>(x => { return userStore; });
            services.AddSingleton<IUserClaimsPrincipalFactory<IdentityUser>, RawClaimsFactory>();

            //Add apikey authentication

            RawRoleStore roleStore = new RawRoleStore();
            services.AddSingleton<IRoleStore<IdentityRole>>(x => { return roleStore; });

            services.AddIdentity<IdentityUser, IdentityRole>();

            // configure identity server with in-memory stores, keys, clients and scopes
            services.AddIdentityServer()
            .AddDeveloperSigningCredential()
            .AddInMemoryPersistedGrants()
            .AddInMemoryIdentityResources(config.GetIdentityResources())
            .AddInMemoryApiResources(config.GetApiResources())
            .AddInMemoryClients(config.GetClients())
            .AddAspNetIdentity<IdentityUser>()
            .AddProfileServiceCustom(userStore);

            if (config.Mode == OAuthMode.External)
            {
                OAuth2IntrospectionOptions options = new OAuth2IntrospectionOptions
                {
                    //base - address of your identityserver
                    Authority = config.Authority,
                    ClientSecret = config.ClientSecret,
                    ClientId = config.ClientId,
                    BasicAuthenticationHeaderStyle = IdentityModel.Client.BasicAuthenticationHeaderStyle.Rfc2617
                };
                if (!string.IsNullOrWhiteSpace(config.IntrospectionEndpoint))
                {
                    options.IntrospectionEndpoint = config.IntrospectionEndpoint;
                }
                options.TokenTypeHint = "Bearer";
                if (!string.IsNullOrWhiteSpace(config.TokenTypeHint))
                {
                    options.TokenTypeHint = config.TokenTypeHint;
                }

                options.Validate();

                services.AddAuthentication(OAuth2IntrospectionDefaults.AuthenticationScheme)
                    .AddOAuth2Introspection(x =>
                    {
                        x = options;
                    });
            }
            else
            {
                services.AddAuthentication(OAuth2IntrospectionDefaults.AuthenticationScheme)
                 //.AddOAuth2Introspection( x => {
                 //    x = options;
                 //});
                 .AddIdentityServerAuthentication("Bearer", options =>
                 {
                     options.Authority = config.Authority;
                     options.ApiName = config.ApiResource;
                     options.ApiSecret = config.ClientSecret;
                     options.RequireHttpsMetadata = false;
                     options.SaveToken = true;
                     options.NameClaimType = ClaimTypes.NameIdentifier;
                 });
            }

            //services.AddMvc(options =>
            //{
            //    options.Filters.Add(new RawAuthorizationAttribute(config.ApiKey, config.AdminApiKey));
            //});
        }

        private IConfigurationRoot configuration;

        public override void Setup(IConfigurationRoot configuration)
        {
            Logger.LogInformation("AuthPlugin plugin Setup");

            base.Setup(configuration);
            this.configuration = configuration;
        }

        private AppEngine appEngine;

        public override void Configure(IApplicationBuilder app, AppEngine appEngine)
        {
            Logger.LogInformation("AuthPlugin plugin Configure");

            this.appEngine = appEngine;

            userStore.SetCRUDService(this.appEngine.Service);
            userStore.SetLogger(this.appEngine.GetLogger(this));
            userStore.InitData().Wait();

            base.Configure(app, appEngine);

            //JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

            app.UseAuthentication();
            app.UseIdentityServer();

            app.UseMvc();
        }

        public AuthConfig GetDefaultConfig()
        {
            return new AuthConfig()
            {
                Mode = OAuthMode.Standalone,
                Authority = "http://localhost:50093",
                ClientId = "raw.client",
                ClientSecret = "raw.secret",
                ApiResource = "rawcms"
            };
        }

        private AuthConfig config;

        public void SetActualConfig(AuthConfig config)
        {
            this.config = config;
        }


        public override void OnPluginLoaded()
        {
            base.OnPluginLoaded();
            Logger.LogInformation("Auth plugin Activated!");
        }
    }
}