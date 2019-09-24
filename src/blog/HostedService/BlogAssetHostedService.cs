﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Laobian.Share.BlogEngine;
using Laobian.Share.Config;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Laobian.Blog.HostedService
{
    public class BlogAssetHostedService : BackgroundService
    {
        private readonly AppConfig _appConfig;
        private readonly IBlogService _blogService;

        public BlogAssetHostedService(IBlogService blogService, IOptions<AppConfig> appConfig)
        {
            _blogService = blogService;
            _appConfig = appConfig.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
                try
                {
                    await _blogService.UpdateCloudAssetsAsync();
                }
                catch (Exception ex)
                {

                }
            }
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            await _blogService.UpdateLocalAssetsAsync();
            await base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await _blogService.UpdateCloudAssetsAsync();
            await base.StopAsync(cancellationToken);
        }
    }
}
