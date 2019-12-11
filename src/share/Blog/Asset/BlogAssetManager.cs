﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Laobian.Share.Blog.Extension;
using Laobian.Share.Blog.Model;
using Laobian.Share.Blog.Parser;
using Laobian.Share.Git;
using Laobian.Share.Helper;

namespace Laobian.Share.Blog.Asset
{
    public class BlogAssetManager : IBlogAssetManager
    {
        private readonly List<BlogCategory> _allCategories;
        private readonly List<BlogPost> _allPosts;
        private readonly List<BlogTag> _allTags;

        private readonly BlogCategoryParser _categoryParser;
        private readonly IGitClient _gitClient;
        private readonly GitConfig _gitConfig;
        private readonly ManualResetEventSlim _manualReset;
        private readonly BlogPostParser _postParser;
        private readonly SemaphoreSlim _semaphore;
        private readonly BlogTagParser _tagParser;
        private readonly BlogPostVisitParser _postVisitParser;

        private string _aboutHtml;
        private BlogPostAccess _allPostAccess;

        public BlogAssetManager(IGitClient gitClient)
        {
            _allTags = new List<BlogTag>();
            _allPosts = new List<BlogPost>();
            _allCategories = new List<BlogCategory>();
            _allPostAccess = new BlogPostAccess();
            _gitClient = gitClient;
            _postParser = new BlogPostParser();
            _categoryParser = new BlogCategoryParser();
            _tagParser = new BlogTagParser();
            _postParser = new BlogPostParser();
            _postVisitParser = new BlogPostVisitParser();
            _semaphore = new SemaphoreSlim(1, 1);
            _manualReset = new ManualResetEventSlim(true);
            _gitConfig = new GitConfig
            {
                GitHubRepositoryName = Global.Config.Blog.AssetGitHubRepoName,
                GitHubRepositoryBranch = Global.Config.Blog.AssetGitHubRepoBranch,
                GitHubRepositoryOwner = Global.Config.Blog.AssetGitHubRepoOwner,
                GitHubAccessToken = Global.Config.Blog.AssetGitHubRepoApiToken,
                GitCloneToDir = Global.Config.Blog.AssetRepoLocalDir,
                GitCommitEmail = Global.Config.Blog.AssetGitCommitEmail,
                GitCommitUser = Global.Config.Blog.AssetGitCommitUser
            };
        }

        public List<BlogPost> GetAllPosts()
        {
            _manualReset.Wait();
            return _allPosts;
        }

        public List<BlogCategory> GetAllCategories()
        {
            _manualReset.Wait();
            return _allCategories;
        }

        public List<BlogTag> GetAllTags()
        {
            _manualReset.Wait();
            return _allTags;
        }

        public string GetAboutHtml()
        {
            _manualReset.Wait();
            return _aboutHtml;
        }

        public BlogPostAccess GetPostVisit()
        {
            _manualReset.Wait();
            return _allPostAccess;
        }

        public async Task RemoteGitToLocalFileAsync()
        {
            await _gitClient.CloneToLocalAsync(_gitConfig);
        }

        public async Task UpdateRemoteGitTemplatePostAsync()
        {
            var templatePost = new BlogPost();
            templatePost.Raw.IsDraft = true;
            templatePost.Raw.PublishTime = new DateTime(2017, 08, 31, 16, 06, 05);
            templatePost.Raw.Category = new List<string> { "分类名称1", "分类名称2" };
            templatePost.Raw.Tag = new List<string> { "标签名称1", "标签名称2" };
            templatePost.Raw.CreateTime = new DateTime(2012, 10, 16, 02, 21, 01);
            templatePost.Raw.ContainsMath = false;
            templatePost.Raw.IsTopping = false;
            templatePost.Raw.LastUpdateTime = new DateTime(2019, 01, 01, 08, 08, 08);
            templatePost.Raw.Link = "Your-Post-Unique-Link";
            templatePost.Raw.Title = "Your Post Title";
            templatePost.Raw.Markdown = "Your Post Content in Markdown.";

            var text = await _postParser.ToTextAsync(templatePost);
            var templatePostLocalPath =
                Path.Combine(Global.Config.Blog.AssetRepoLocalDir, Global.Config.Blog.TemplatePostGitPath);
            await File.WriteAllTextAsync(templatePostLocalPath, text, Encoding.UTF8);
            await _gitClient.CommitAsync(Global.Config.Blog.AssetRepoLocalDir, "Update template post");
        }

        public async Task<BlogAssetReloadResult<object>> LocalFileToLocalMemoryAsync()
        {
            try
            {
                await _semaphore.WaitAsync();
                var postReloadResult = await ReloadLocalMemoryPostAsync();
                var categoryReloadResult = await ReloadLocalMemoryCategoryAsync();
                var tagReloadResult = await ReloadLocalMemoryTagAsync();
                var aboutReloadResult = await ReloadLocalMemoryAboutAsync();
                var postVisitReloadResult = await ReloadLocalMemoryPostVisitAsync();

                var warning = GetWarning(postReloadResult, categoryReloadResult, tagReloadResult, aboutReloadResult, postVisitReloadResult);
                var error = GetError(postReloadResult, categoryReloadResult, tagReloadResult, aboutReloadResult, postVisitReloadResult);
                var result = new BlogAssetReloadResult<object>
                {
                    Warning = warning,
                    Error = error
                };

                if (postReloadResult.Success &&
                    categoryReloadResult.Success &&
                    tagReloadResult.Success &&
                    aboutReloadResult.Success &&
                    postVisitReloadResult.Success)
                {
                    try
                    {
                        _manualReset.Reset();
                        RefreshMemoryAsset(postReloadResult, categoryReloadResult, tagReloadResult, aboutReloadResult, postVisitReloadResult);
                    }
                    finally
                    {
                        _manualReset.Set();
                    }
                }
                else
                {
                    result.Success = false;
                }

                return result;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task LocalMemoryToLocalFileAsync()
        {
            foreach (var blogPost in _allPosts)
            {
                var text = await _postParser.ToTextAsync(blogPost);
                await File.WriteAllTextAsync(blogPost.LocalPath, text, Encoding.UTF8);
            }

            var postVisitText = await _postVisitParser.ToTextAsync(_allPostAccess);
            await File.WriteAllTextAsync(
                Path.Combine(Global.Config.Blog.AssetRepoLocalDir, Global.Config.Blog.PostAccessGitPath),
                postVisitText,
                Encoding.UTF8);
        }

        public async Task LocalFileToRemoteGitAsync()
        {
            await _gitClient.CommitAsync(Global.Config.Blog.AssetRepoLocalDir, "Update assets");
        }

        private static string GetError(
            BlogAssetReloadResult<List<BlogPost>> postReloadResult,
            BlogAssetReloadResult<List<BlogCategory>> categoryReloadResult,
            BlogAssetReloadResult<List<BlogTag>> tagReloadResult,
            BlogAssetReloadResult<string> aboutReloadResult,
            BlogAssetReloadResult<BlogPostAccess> postVisitReloadResult)
        {
            var errors = new List<string>();
            if (!string.IsNullOrEmpty(postReloadResult.Error))
            {
                errors.Add(postReloadResult.Error);
            }

            if (!string.IsNullOrEmpty(categoryReloadResult.Error))
            {
                errors.Add(categoryReloadResult.Error);
            }

            if (!string.IsNullOrEmpty(tagReloadResult.Error))
            {
                errors.Add(tagReloadResult.Error);
            }

            if (!string.IsNullOrEmpty(aboutReloadResult.Error))
            {
                errors.Add(aboutReloadResult.Error);
            }

            if (!string.IsNullOrEmpty(postVisitReloadResult.Error))
            {
                errors.Add(postVisitReloadResult.Error);
            }

            var error = errors.Any() ? string.Join(Environment.NewLine, errors) : string.Empty;
            return error;
        }

        private static string GetWarning(
            BlogAssetReloadResult<List<BlogPost>> postReloadResult,
            BlogAssetReloadResult<List<BlogCategory>> categoryReloadResult,
            BlogAssetReloadResult<List<BlogTag>> tagReloadResult,
            BlogAssetReloadResult<string> aboutReloadResult,
            BlogAssetReloadResult<BlogPostAccess> postVisitReloadResult)
        {
            var warnings = new List<string>();
            if (!string.IsNullOrEmpty(postReloadResult.Warning))
            {
                warnings.Add(postReloadResult.Warning);
            }

            if (!string.IsNullOrEmpty(categoryReloadResult.Warning))
            {
                warnings.Add(categoryReloadResult.Warning);
            }

            if (!string.IsNullOrEmpty(tagReloadResult.Warning))
            {
                warnings.Add(tagReloadResult.Warning);
            }

            if (!string.IsNullOrEmpty(aboutReloadResult.Warning))
            {
                warnings.Add(aboutReloadResult.Warning);
            }

            if (!string.IsNullOrEmpty(postVisitReloadResult.Warning))
            {
                warnings.Add(postVisitReloadResult.Warning);
            }

            var warning = warnings.Any() ? string.Join(Environment.NewLine, warnings) : string.Empty;
            return warning;
        }

        private void RefreshMemoryAsset(
            BlogAssetReloadResult<List<BlogPost>> postReloadResult,
            BlogAssetReloadResult<List<BlogCategory>> categoryReloadResult,
            BlogAssetReloadResult<List<BlogTag>> tagReloadResult,
            BlogAssetReloadResult<string> aboutReloadResult,
            BlogAssetReloadResult<BlogPostAccess> postVisitReloadResult)
        {
            _allPosts.Clear();
            _allPosts.AddRange(postReloadResult.Result);

            _allCategories.Clear();
            _allCategories.AddRange(categoryReloadResult.Result);

            _allTags.Clear();
            _allTags.AddRange(tagReloadResult.Result);

            _aboutHtml = aboutReloadResult.Result;
            _allPostAccess = postVisitReloadResult.Result;

            foreach (var blogPost in _allPosts)
            {
                blogPost.Resolve(_allCategories, _allTags, _allPostAccess);
            }

            foreach (var blogCategory in _allCategories)
            {
                blogCategory.Resolve(_allPosts);
            }

            foreach (var blogTag in _allTags)
            {
                blogTag.Resolve(_allPosts);
            }
        }

        private async Task<BlogAssetReloadResult<List<BlogPost>>> ReloadLocalMemoryPostAsync()
        {
            var result = new BlogAssetReloadResult<List<BlogPost>> { Result = new List<BlogPost>() };
            var postLocalPath = Path.Combine(Global.Config.Blog.AssetRepoLocalDir, Global.Config.Blog.PostGitPath);
            if (!Directory.Exists(postLocalPath))
            {
                result.Warning = $"No post folder found under \"{postLocalPath}\".";
                return result;
            }

            var templatePostLocalPath =
                Path.Combine(Global.Config.Blog.AssetRepoLocalDir, Global.Config.Blog.TemplatePostGitPath);
            foreach (var file in Directory.EnumerateFiles(postLocalPath, $"*{Global.Config.Common.MarkdownExtension}"))
            {
                try
                {
                    if (CompareHelper.IgnoreCase(templatePostLocalPath, file))
                    {
                        continue; // skip template post
                    }

                    var text = await File.ReadAllTextAsync(file);
                    var parseResult = await _postParser.FromTextAsync(text);
                    if (parseResult.WarningMessages.Any())
                    {
                        result.Warning +=
                            $"Parse post warning: {file}.{Environment.NewLine}{string.Join(Environment.NewLine, parseResult.WarningMessages)}{Environment.NewLine}";
                    }

                    if (parseResult.WarningMessages.Any())
                    {
                        result.Warning +=
                            $"Parse post error: {file}.{Environment.NewLine}{string.Join(Environment.NewLine, parseResult.ErrorMessages)}{Environment.NewLine}";
                    }

                    if (parseResult.Success)
                    {
                        parseResult.Instance.GitPath =
                            file.Substring(
                                file.IndexOf(Global.Config.Blog.AssetRepoLocalDir, StringComparison.CurrentCulture) +
                                Global.Config.Blog.AssetRepoLocalDir.Length + 1);
                        parseResult.Instance.LocalPath = file;
                        result.Result.Add(parseResult.Instance);
                    }
                }
                catch (Exception ex)
                {
                    result.Error +=
                        $"Parse post throw exception: {file}.{Environment.NewLine}{ex}{Environment.NewLine}";
                }
            }

            return result;
        }

        private async Task<BlogAssetReloadResult<List<BlogCategory>>> ReloadLocalMemoryCategoryAsync()
        {
            var result = new BlogAssetReloadResult<List<BlogCategory>>();
            var categoryLocalPath =
                Path.Combine(Global.Config.Blog.AssetRepoLocalDir, Global.Config.Blog.CategoryGitPath);
            if (!File.Exists(categoryLocalPath))
            {
                result.Warning = $"No category asset found under \"{categoryLocalPath}\".";
                return result;
            }

            var text = await File.ReadAllTextAsync(categoryLocalPath);
            var parseResult = await _categoryParser.FromTextAsync(text);
            if (parseResult.WarningMessages.Any())
            {
                result.Warning =
                    $"Blog category parse warnings: {Environment.NewLine}{string.Join(Environment.NewLine, parseResult.WarningMessages)}";
            }

            if (parseResult.ErrorMessages.Any())
            {
                result.Warning =
                    $"Blog category parse errors: {Environment.NewLine}{string.Join(Environment.NewLine, parseResult.ErrorMessages)}";
            }

            result.Success = parseResult.Success;
            result.Result = parseResult.Instance;
            return result;
        }

        private async Task<BlogAssetReloadResult<List<BlogTag>>> ReloadLocalMemoryTagAsync()
        {
            var result = new BlogAssetReloadResult<List<BlogTag>>();
            var tagLocalPath = Path.Combine(Global.Config.Blog.AssetRepoLocalDir, Global.Config.Blog.TagGitPath);
            if (!File.Exists(tagLocalPath))
            {
                result.Warning = $"No tag asset found under \"{tagLocalPath}\".";
                return result;
            }

            var text = await File.ReadAllTextAsync(tagLocalPath);
            var parseResult = await _tagParser.FromTextAsync(text);
            if (parseResult.WarningMessages.Any())
            {
                result.Warning =
                    $"Blog tag parse warnings: {Environment.NewLine}{string.Join(Environment.NewLine, parseResult.WarningMessages)}";
            }

            if (parseResult.ErrorMessages.Any())
            {
                result.Warning =
                    $"Blog tag parse errors: {Environment.NewLine}{string.Join(Environment.NewLine, parseResult.ErrorMessages)}";
            }

            result.Success = parseResult.Success;
            result.Result = parseResult.Instance;
            return result;
        }

        private async Task<BlogAssetReloadResult<string>> ReloadLocalMemoryAboutAsync()
        {
            var result = new BlogAssetReloadResult<string>();
            var aboutLocalPath = Path.Combine(Global.Config.Blog.AssetRepoLocalDir, Global.Config.Blog.AboutGitPath);
            if (!File.Exists(aboutLocalPath))
            {
                result.Success = true;
                result.Warning = $"No about asset found under \"{aboutLocalPath}\".";
                return result;
            }

            var md = await File.ReadAllTextAsync(aboutLocalPath);
            result.Result = MarkdownHelper.ToHtml(md);
            return result;
        }

        private async Task<BlogAssetReloadResult<BlogPostAccess>> ReloadLocalMemoryPostVisitAsync()
        {
            var result = new BlogAssetReloadResult<BlogPostAccess> { Result = new BlogPostAccess() };
            var postVisitLocalPath = Path.Combine(Global.Config.Blog.AssetRepoLocalDir, Global.Config.Blog.PostAccessGitPath);
            if (!File.Exists(postVisitLocalPath))
            {
                result.Success = true;
                result.Warning = $"No post visit asset found under \"{postVisitLocalPath}\".";
                return result;
            }

            var text = await File.ReadAllTextAsync(postVisitLocalPath);
            var parseResult = await _postVisitParser.FromTextAsync(text);
            if (parseResult.WarningMessages.Any())
            {
                result.Warning =
                    $"Blog post visit parse warnings: {Environment.NewLine}{string.Join(Environment.NewLine, parseResult.WarningMessages)}";
            }

            if (parseResult.ErrorMessages.Any())
            {
                result.Warning =
                    $"Blog post visit parse errors: {Environment.NewLine}{string.Join(Environment.NewLine, parseResult.ErrorMessages)}";
            }

            result.Success = parseResult.Success;
            result.Result = parseResult.Instance;
            return result;
        }
    }
}