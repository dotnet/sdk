// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CreateBaselineUpdatePR;

using Octokit;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

public class PRCreator
{
    private readonly string _repoOwner;
    private readonly string _repoName;
    private readonly GitHubClient _client;
    private const string BuildLink = "https://dev.azure.com/dnceng/internal/_build/results?buildId=";
    private const string DefaultLicenseBaselineContent = "{\n  \"files\": []\n}";
    private const string TreeMode = "040000";
    public PRCreator(string repo, string gitHubToken)
    {
        // Create a new GitHub client
        _client = new GitHubClient(new ProductHeaderValue(System.Reflection.Assembly.GetExecutingAssembly().GetName().Name));
        var authToken = new Credentials(gitHubToken);
        _client.Credentials = authToken;
        _repoOwner = repo.Split('/')[0];
        _repoName = repo.Split('/')[1];
    }

    public async Task<int> ExecuteAsync(
        string originalFilesDirectory,
        string updatedFilesDirectory,
        int buildId,
        string title,
        string targetBranch,
        Pipelines pipeline)
    {
        DateTime startTime = DateTime.Now.ToUniversalTime();

        Log.LogInformation($"Starting PR creation at {startTime} UTC for pipeline {pipeline}.");

        var updatedTestsFiles = GetUpdatedFiles(updatedFilesDirectory);

        // Create a new tree for the originalFilesDirectory based on the target branch
        var originalTreeResponse = await _client.Git.Tree.GetRecursive(_repoOwner, _repoName, targetBranch);
        var testResultsTreeItems = originalTreeResponse.Tree
            .Where(file => file.Path.Contains(originalFilesDirectory) && file.Path != originalFilesDirectory)
            .Select(file => new NewTreeItem
            {
                Path = Path.GetRelativePath(originalFilesDirectory, file.Path),
                Mode = file.Mode,
                Type = file.Type.Value,
                Sha = file.Sha
            })
            .ToList();

        // Update the test results tree based on the pipeline
        testResultsTreeItems = await UpdateAllFilesAsync(updatedTestsFiles, testResultsTreeItems, pipeline);
        var testResultsTreeResponse = await CreateTreeFromItemsAsync(testResultsTreeItems);
        var parentTreeResponse = await CreateParentTreeAsync(testResultsTreeResponse, originalTreeResponse, originalFilesDirectory);

        await CreateOrUpdatePullRequestAsync(parentTreeResponse, buildId, title, targetBranch);

        return Log.GetExitCode();
    }

    // Return a dictionary using the filename without the 
    // "Updated" prefix and anything after the first '.' as the key
    private Dictionary<string, HashSet<string>> GetUpdatedFiles(string updatedFilesDirectory) =>
        Directory
            .GetFiles(updatedFilesDirectory, "Updated*", SearchOption.AllDirectories)
            .GroupBy(updatedTestsFile => ParseUpdatedFileName(updatedTestsFile).Split('.')[0])
            .ToDictionary(
                group => group.Key,
                group => new HashSet<string>(group)
            );

    private async Task<List<NewTreeItem>> UpdateAllFilesAsync(Dictionary<string, HashSet<string>> updatedFiles, List<NewTreeItem> tree, Pipelines pipeline)
    {
        bool isSdkPipeline = pipeline == Pipelines.Sdk;
        string? defaultContent = pipeline == Pipelines.License ? DefaultLicenseBaselineContent : null;
        foreach (var updatedFile in updatedFiles)
        {
            if (updatedFile.Key.Contains("Exclusions"))
            {
                tree = await UpdateExclusionFileAsync(updatedFile.Key, updatedFile.Value, tree, union: isSdkPipeline);
            }
            else
            {
                tree = await UpdateRegularFilesAsync(updatedFile.Value, tree, defaultContent);
            }
        }
        return tree;
    }

    private async Task<List<NewTreeItem>> UpdateExclusionFileAsync(string fileNameKey, HashSet<string> updatedFiles, List<NewTreeItem> tree, bool union = false)
    {
        string? content = null;
        IEnumerable<string> parsedFile = Enumerable.Empty<string>();

        // Combine the lines of the updated files
        foreach (var filePath in updatedFiles)
        {
            var updatedFileLines = File.ReadAllLines(filePath);
            if (!parsedFile.Any())
            {
                parsedFile = updatedFileLines;
            }
            else if (union == true)
            {
                parsedFile = parsedFile.Union(updatedFileLines);
            }
            else
            {
                parsedFile = parsedFile.Where(parsedLine => updatedFileLines.Contains(parsedLine));
            }
        }

        if (union == true)
        {
            // Need to compare to the original file and remove any lines that are not in the parsed updated file

            // Find the key in the tree, download the blob, and convert it to utf8
            var originalTreeItem = tree
                .Where(item => item.Path.Contains(fileNameKey))
                .FirstOrDefault();

            if (originalTreeItem != null)
            {
                var originalBlob = await _client.Git.Blob.Get(_repoOwner, _repoName, originalTreeItem.Sha);
                content = Encoding.UTF8.GetString(Convert.FromBase64String(originalBlob.Content));
                var originalContent = content.Split("\n");

                foreach (var line in originalContent)
                {
                    if (!parsedFile.Contains(line))
                    {
                        // If the newline character is not present, the line is at the end of the file
                        content = content.Contains(line + "\n") ? content.Replace(line + "\n", "") : content.Replace(line, "");
                    }
                }
            }
        }

        else
        {
            if (parsedFile.Any())
            {
                // No need to compare to the original file, just log the parsed lines
                content = string.Join("\n", parsedFile) + "\n";
            }
        }

        string updatedFilePath = fileNameKey + ".txt";
        return await UpdateFileAsync(tree, content, fileNameKey, updatedFilePath);
    }

    private async Task<List<NewTreeItem>> UpdateRegularFilesAsync(HashSet<string> updatedFiles, List<NewTreeItem> tree, string? compareContent = null)
    {
        foreach (var filePath in updatedFiles)
        {
            var content = File.ReadAllText(filePath);
            if (compareContent != null && content == compareContent)
            {
                content = null;
            }
            string originalFileName = Path.GetFileName(ParseUpdatedFileName(filePath));
            tree = await UpdateFileAsync(tree, content, originalFileName, originalFileName);
        }
        return tree;
    }

    private async Task<List<NewTreeItem>> UpdateFileAsync(List<NewTreeItem> tree, string? content, string searchFileName, string updatedPath)
    {
        var originalTreeItem = tree
            .Where(item => item.Path.Contains(searchFileName))
            .FirstOrDefault();

        if (content == null)
        {
            // Content is null, delete the file if it exists
            if (originalTreeItem != null)
            {
                tree.Remove(originalTreeItem);
            }
        }
        else if (originalTreeItem == null)
        {
            // Path not in the tree, add a new tree item
            var blob = await CreateBlobAsync(content);
            tree.Add(new NewTreeItem
            {
                Type = TreeType.Blob,
                Mode = FileMode.File,
                Path = updatedPath,
                Sha = blob.Sha
            });
        }
        else
        {
            // Path in the tree, update the sha and the content
            var blob = await CreateBlobAsync(content);
            originalTreeItem.Sha = blob.Sha;
        }
        return tree;
    }

    private async Task<BlobReference> CreateBlobAsync(string content)
    {
        var blob = new NewBlob
        {
            Content = content,
            Encoding = EncodingType.Utf8
        };
        return await _client.Git.Blob.Create(_repoOwner, _repoName, blob);
    }

    private string ParseUpdatedFileName(string updatedFile) => updatedFile.Split("Updated")[1];

    private async Task<TreeResponse> CreateTreeFromItemsAsync(List<NewTreeItem> items, string path = "")
    {
        var newTreeItems = new List<NewTreeItem>();

        var groups = items.GroupBy(item => Path.GetDirectoryName(item.Path));
        foreach (var group in groups)
        {
            if (string.IsNullOrEmpty(group.Key) || group.Key == path)
            {
                // These items are in the current directory, so add them to the new tree items
                foreach (var item in group)
                {
                    if(item.Type != TreeType.Tree)
                    {
                        newTreeItems.Add(new NewTreeItem
                        {
                            Path = path == string.Empty ? item.Path : Path.GetRelativePath(path, item.Path),
                            Mode = item.Mode,
                            Type = item.Type,
                            Sha = item.Sha
                        });
                    }
                }
            }
            else
            {
                // These items are in a subdirectory, so recursively create a tree for them
                var subtreeResponse = await CreateTreeFromItemsAsync(group.ToList(), group.Key);
                newTreeItems.Add(new NewTreeItem
                {
                    Path = group.Key,
                    Mode = TreeMode,
                    Type = TreeType.Tree,
                    Sha = subtreeResponse.Sha
                });
            }
        }

        var newTree = new NewTree();
        foreach (var item in newTreeItems)
        {
            newTree.Tree.Add(item);
        }
        return await _client.Git.Tree.Create(_repoOwner, _repoName, newTree);
    }

    private async Task<TreeResponse> CreateParentTreeAsync(TreeResponse testResultsTreeResponse, TreeResponse originalTreeResponse, string originalFilesDirectory)
    {
        // Create a new tree for the parent directory
        NewTree parentTree = new NewTree { BaseTree = originalTreeResponse.Sha };

        //  Connect the updated test results tree
        parentTree.Tree.Add(new NewTreeItem
        {
            Path = originalFilesDirectory,
            Mode = TreeMode,
            Type = TreeType.Tree,
            Sha = testResultsTreeResponse.Sha
        });

        return await _client.Git.Tree.Create(_repoOwner, _repoName, parentTree);
    }

    private async Task CreateOrUpdatePullRequestAsync(TreeResponse parentTreeResponse, int buildId, string title, string targetBranch)
    {
        var existingPullRequest = await GetExistingPullRequestAsync(title, targetBranch);

        // Create the branch name and get the head reference
        string newBranchName = string.Empty;
        string headSha = await GetHeadShaAsync(targetBranch);
        if (existingPullRequest == null)
        {
            string utcTime = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            newBranchName = $"pr-baseline-{utcTime}";
        }
        else
        {
            newBranchName = existingPullRequest.Head.Ref;

            try
            {
                // Merge the target branch into the existing pull request
                var merge = new NewMerge(newBranchName, headSha);
                await _client.Repository.Merging.Create(_repoOwner, _repoName, merge);
            }
            catch (Exception e)
            {
                Log.LogWarning($"Failed to merge the target branch into the existing pull request: {e.Message}");
                Log.LogWarning("Continuing with updating the existing pull request. You may need to resolve conflicts manually in the PR.");
            }

            headSha = await GetHeadShaAsync(newBranchName);
        }

        var commitSha = await CreateCommitAsync(parentTreeResponse.Sha, headSha, $"Update baselines for build {BuildLink}{buildId} (internal Microsoft link)");
        if (await ShouldMakeUpdatesAsync(headSha, commitSha))
        {
            string pullRequestBody = $"This PR was created by the `CreateBaselineUpdatePR` tool for build {buildId}. \n\n" +
                                 $"The updated test results can be found at {BuildLink}{buildId} (internal Microsoft link)";
            if (existingPullRequest != null)
            {
                await UpdatePullRequestAsync(newBranchName, commitSha, pullRequestBody, existingPullRequest);
            }
            else
            {
                await CreatePullRequestAsync(newBranchName, commitSha, targetBranch, title, pullRequestBody);
            }
        }
    }

    private async Task<PullRequest?> GetExistingPullRequestAsync(string title, string targetBranch)
    {
        var request = new PullRequestRequest
        {
            Base = targetBranch
        };
        var existingPullRequest = await _client.PullRequest.GetAllForRepository(_repoOwner, _repoName, request);
        return existingPullRequest.FirstOrDefault(pr => pr.Title == title);
    }

    private async Task<string> CreateCommitAsync(string newSha, string headSha, string commitMessage)
    {
        var newCommit = new NewCommit(commitMessage, newSha, headSha);
        var commit = await _client.Git.Commit.Create(_repoOwner, _repoName, newCommit);
        return commit.Sha;
    }

    private async Task<bool> ShouldMakeUpdatesAsync(string headSha, string commitSha)
    {
        var comparison = await _client.Repository.Commit.Compare(_repoOwner, _repoName, headSha, commitSha);
        if (!comparison.Files.Any())
        {
            Log.LogInformation("No changes to commit. Skipping PR creation/updates.");
            return false;
        }
        return true;
    }

    private async Task UpdatePullRequestAsync(string branchName, string commitSha, string body, PullRequest pullRequest)
    {
        await UpdateReferenceAsync(branchName, commitSha);

        var pullRequestUpdate = new PullRequestUpdate
        {
            Body = body
        };
        await _client.PullRequest.Update(_repoOwner, _repoName, pullRequest.Number, pullRequestUpdate);

        Log.LogInformation($"Updated existing pull request #{pullRequest.Number}. URL: {pullRequest.HtmlUrl}");
    }

    private async Task CreatePullRequestAsync(string newBranchName, string commitSha, string targetBranch, string title, string body)
    {
        await CreateReferenceAsync(newBranchName, commitSha);

        var newPullRequest = new NewPullRequest(title, newBranchName, targetBranch)
        {
            Body = body
        };
        var pullRequest = await _client.PullRequest.Create(_repoOwner, _repoName, newPullRequest);

        Log.LogInformation($"Created pull request #{pullRequest.Number}. URL: {pullRequest.HtmlUrl}");
    }

    private async Task<string> GetHeadShaAsync(string branchName)
    {
        var reference = await _client.Git.Reference.Get(_repoOwner, _repoName, $"heads/{branchName}");
        return reference.Object.Sha;
    }

    private async Task UpdateReferenceAsync(string branchName, string commitSha)
    {
        var referenceUpdate = new ReferenceUpdate(commitSha);
        await _client.Git.Reference.Update(_repoOwner, _repoName, $"heads/{branchName}", referenceUpdate);
    }

    private async Task CreateReferenceAsync(string branchName, string commitSha)
    {
        var newReference = new NewReference($"refs/heads/{branchName}", commitSha);
        await _client.Git.Reference.Create(_repoOwner, _repoName, newReference);
    }
}