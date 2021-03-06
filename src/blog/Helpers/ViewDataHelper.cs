﻿using Laobian.Blog.Models;
using Laobian.Share;
using Microsoft.Extensions.Hosting;

namespace Laobian.Blog.Helpers
{
    public class ViewDataHelper
    {
        public static string GetCanonical(dynamic viewData)
        {
            var canonical = "/";
            if (viewData[ViewDataConstant.Canonical] != null)
            {
                canonical = viewData[ViewDataConstant.Canonical].ToString();
            }

            return canonical;
        }

        public static string GetDescription(dynamic viewData)
        {
            var description = string.Empty;
            if (viewData[ViewDataConstant.Description] != null)
            {
                description = viewData[ViewDataConstant.Description];
            }

            description += Global.Config.Blog.Description;
            return description;
        }

        public static string GetTitle(dynamic viewData)
        {
            var title = string.Empty;
            if (viewData[ViewDataConstant.Title] != null)
            {
                title = viewData[ViewDataConstant.Title].ToString();
            }

            return string.IsNullOrEmpty(title) ? Global.Config.Blog.Name : $"{title} - {Global.Config.Blog.Name}";
        }

        public static string GetRobots(dynamic viewData)
        {
            var robots = "index, follow, archive";

            if (Global.Environment.IsStaging())
            {
                robots = "noindex, nofollow";
            }
            else if (viewData[ViewDataConstant.VisibleToSearchEngine] != null)
            {
                if (!bool.TryParse(viewData[ViewDataConstant.VisibleToSearchEngine].ToString(), out bool result) ||
                    !result)
                {
                    robots = "noindex, nofollow";
                }
            }

            return robots;
        }
    }
}