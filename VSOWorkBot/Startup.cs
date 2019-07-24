// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Generated with Bot Builder V4 SDK Template for Visual Studio EmptyBot v4.3.0

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VSOWorkBot.Bots;
using VSOWorkBot.Dialogs;
using VSOWorkBot.Extensions;
using Newtonsoft.Json.Linq;
using VSOWorkBot.Api;

namespace VSOWorkBot
{
public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration
    {
        get;
    }

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddMvc()
        .SetCompatibilityVersion(CompatibilityVersion.Version_2_1);

        // Create the Bot Framework Adapter with error handling enabled.
        services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();

        // Create the storage we'll be using for User and Conversation state. (Memory is great for testing purposes.)
        services.AddSingleton<IStorage, MemoryStorage>();

        // Create the User state. (Used in this bot's Dialog implementation.)
        services.AddSingleton<UserState>();

        // Create the Conversation state. (Used by the Dialog system itself.)
        services.AddSingleton<ConversationState>();

        // Create the credential provider to be used with the Bot Framework Adapter.
        services.AddSingleton<ICredentialProvider, ConfigurationCredentialProvider>();

        // The Dialog that will be run by the bot.
        services.AddSingleton<MainDialog>();

        // Helper to access APIs that perform Authentication
        services.AddSingleton<AuthHelper>();

        // Provides support for InMemory session stores
        services.AddDistributedMemoryCache();

        // Enable ASP.Net Core sessions
        services.AddSession(options =>
        {
            // Set the session expiry to 5 minutes.
            // After that we clear it all out.
            options.IdleTimeout = TimeSpan.FromMinutes(5);
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
        });

        // Create the bot as a transient. In this case the ASP Controller is expecting an IBot.
        services.AddTransient<IBot, AuthBot<MainDialog>>();
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
        app.UseSession();

        //app.UseHttpsRedirection();
        app.UseMvc();
    }
}
}