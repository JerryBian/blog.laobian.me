﻿using System;
using System.Linq;
using System.Reflection;
using Laobian.Blog.HostedService;
using Laobian.Share.BlogEngine;
using Laobian.Share.Config;
using Laobian.Share.Infrastructure.Cache;
using Laobian.Share.Infrastructure.Command;
using Laobian.Share.Infrastructure.Email;
using Laobian.Share.Infrastructure.Git;
using Laobian.Share.Log;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Laobian.Blog.Helpers
{
    public class StartupHelper
    {
        public static void RegisterService(IServiceCollection services, IConfiguration config)
        {
            
            services.Configure<AppConfig>(ac => MapConfig(config, ac));

            services.AddSingleton<IMemoryCacheClient, MemoryCacheClient>();
            services.AddSingleton<ICommand, PowerShellCommand>();
            services.AddSingleton<IBlogService, BlogService>();
            services.AddSingleton<IGitClient, GitClient>();
            services.AddSingleton<IEmailClient, SendGridEmailClient>();
            services.AddSingleton<ILogStore, LogStore>();

            services.AddHostedService<PostHostedService>();
            services.AddHostedService<LogHostedService>();

            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
                {
                    options.LoginPath = "/login";
                    options.LogoutPath = "/logout";
                    options.ReturnUrlParameter = "r";
                    options.Cookie.HttpOnly = true;
                    options.ExpireTimeSpan = TimeSpan.FromDays(30);
                });
        }

        private static void MapConfig(IConfiguration config, AppConfig ac)
        {
            foreach (var domainProp in typeof(AppConfig).GetProperties().Where(p=>typeof(IConfig).IsAssignableFrom(p.PropertyType)))
            {
                var obj = Activator.CreateInstance(domainProp.PropertyType);
                foreach (var propertyInfo in domainProp.PropertyType.GetProperties())
                {
                    var attr = propertyInfo.GetCustomAttribute<ConfigMetaAttribute>();
                    if (attr != null)
                    {
                        var configValue = config.GetValue(propertyInfo.PropertyType, attr.Name);
                        if (configValue == null)
                        {
                            if (attr.Required)
                            {
                                throw new AppConfigException($"Missing AppConfig. Domain: {domainProp.Name}, config: {propertyInfo.Name}.");
                            }

                            configValue = Convert.ChangeType(attr.DefaultValue, propertyInfo.PropertyType);
                        }

                        propertyInfo.SetValue(obj, Convert.ChangeType(configValue, propertyInfo.PropertyType));
                    }
                }

                domainProp.SetValue(ac, obj);
            }
        }
    }
}
