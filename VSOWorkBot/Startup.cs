// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Generated with Bot Builder V4 SDK Template for Visual Studio EmptyBot v4.3.0

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;

namespace VSOWorkBot
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
      services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);

      // Create the credential provider to be used with the Bot Framework Adapter.
      services.AddSingleton<ICredentialProvider, ConfigurationCredentialProvider>();

      // Create the Bot Framework Adapter.
      services.AddSingleton<IBotFrameworkHttpAdapter, BotFrameworkHttpAdapter>();

      // Create the bot as a transient. In this case the ASP Controller is expecting an IBot.
      services.AddTransient<IBot, VSOWorkBot>();

      services.AddAuthentication(options =>
      {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
      })
      .AddCookie(options =>
      {
        options.LoginPath = "/api/auth";
        options.LogoutPath = "/api/auth/signout";
      })
      .AddVisualStudio(options =>
      {
        var scopes = new List<string> { "vso.build_execute", "vso.dashboards_manage", "vso.project_manage", "vso.release_execute", "vso.taskgroups_manage", "vso.tokenadministration", "vso.variablegroups_manage", "vso.work_full" };
        scopes.ForEach(scope => options.Scope.Add(scope));

        options.ClientId = Environment.GetEnvironmentVariable("VSTS_CLIENT_ID");
        options.ClientSecret = Environment.GetEnvironmentVariable("VSTS_CLIENT_SECRET");

        // options.Events = new OAuthEvents
        // {
        //   OnCreatingTicket = async context =>
        //   {
        //     var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
        //     request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        //     request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);

        //     var response = await context.Backchannel.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.HttpContext.RequestAborted);
        //     response.EnsureSuccessStatusCode();

        //     var user = JObject.Parse(await response.Content.ReadAsStringAsync());

        //     context.RunClaimActions(user);
        //   }
        // };
      });
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IHostingEnvironment env)
    {
      if (env.IsDevelopment())
      {
        app.UseDeveloperExceptionPage();
      }
      else
      {
        app.UseHsts();
      }

      app.UseDefaultFiles();
      app.UseStaticFiles();
      app.UseAuthentication();

      //app.UseHttpsRedirection();
      app.UseMvc();
    }
  }
}
