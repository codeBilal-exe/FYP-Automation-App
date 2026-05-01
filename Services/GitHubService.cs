using System.Net.Http.Headers;
using System.Text.Json;

namespace FYP_AutomationSystem.Services
{
    public class GitHubService
    {
        private readonly HttpClient _httpClient;
        private const string GitHubApiBaseUrl = "https://api.github.com";

        public GitHubService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "FYP-AutomationSystem");
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
        }

        /// <summary>
        /// Fetches repository statistics from GitHub API
        /// </summary>
        public async Task<Dictionary<string, object>> FetchRepoStats(string ownerRepo)
        {
            var result = new Dictionary<string, object>();

            try
            {
                if (string.IsNullOrWhiteSpace(ownerRepo))
                    return result;

                var parts = ownerRepo.Split('/');
                if (parts.Length != 2)
                    return result;

                var owner = parts[0].Trim();
                var repo = parts[1].Trim();

                // Fetch repository information
                var repoUrl = $"{GitHubApiBaseUrl}/repos/{owner}/{repo}";
                var repoResponse = await _httpClient.GetAsync(repoUrl);

                if (!repoResponse.IsSuccessStatusCode)
                {
                    result["Error"] = "Repository not found or API error";
                    return result;
                }

                var repoContent = await repoResponse.Content.ReadAsStringAsync();
                using (var doc = JsonDocument.Parse(repoContent))
                {
                    var root = doc.RootElement;
                    result["Stars"] = root.GetProperty("stargazers_count").GetInt32();
                    result["OpenIssues"] = root.GetProperty("open_issues_count").GetInt32();
                }

                // Fetch commit count
                var commitsUrl = $"{GitHubApiBaseUrl}/repos/{owner}/{repo}/commits?per_page=1";
                var commitsResponse = await _httpClient.GetAsync(commitsUrl);

                if (commitsResponse.IsSuccessStatusCode)
                {
                    var linkHeader = commitsResponse.Headers.FirstOrDefault(h => h.Key == "Link").Value;
                    if (linkHeader != null && linkHeader.Count() > 0)
                    {
                        var lastLink = linkHeader.FirstOrDefault()?.Split(',').Last();
                        if (lastLink != null)
                        {
                            var pageMatch = System.Text.RegularExpressions.Regex.Match(lastLink, @"page=(\d+)");
                            if (pageMatch.Success && int.TryParse(pageMatch.Groups[1].Value, out int lastPage))
                            {
                                result["CommitsCount"] = lastPage;
                            }
                            else
                            {
                                result["CommitsCount"] = 1;
                            }
                        }
                    }
                    else
                    {
                        result["CommitsCount"] = 1;
                    }

                    // Fetch latest commit date
                    var commitsJson = await commitsResponse.Content.ReadAsStringAsync();
                    using (var doc = JsonDocument.Parse(commitsJson))
                    {
                        if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                        {
                            var commit = doc.RootElement[0];
                            var commitDate = commit.GetProperty("commit")
                                .GetProperty("committer")
                                .GetProperty("date")
                                .GetString();
                            result["LastCommitDate"] = commitDate ?? "N/A";
                        }
                    }
                }

                // Fetch top languages
                var languagesUrl = $"{GitHubApiBaseUrl}/repos/{owner}/{repo}/languages";
                var languagesResponse = await _httpClient.GetAsync(languagesUrl);

                if (languagesResponse.IsSuccessStatusCode)
                {
                    var languagesContent = await languagesResponse.Content.ReadAsStringAsync();
                    using (var doc = JsonDocument.Parse(languagesContent))
                    {
                        var languages = new Dictionary<string, int>();
                        foreach (var property in doc.RootElement.EnumerateObject())
                        {
                            languages[property.Name] = property.Value.GetInt32();
                        }
                        result["Languages"] = languages;
                    }
                }

                result["Owner"] = owner;
                result["Repository"] = repo;
                result["Success"] = true;

                return result;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"GitHub API request error: {ex.Message}");
                result["Error"] = "Network error or API unavailable";
                result["Success"] = false;
                return result;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"JSON parsing error: {ex.Message}");
                result["Error"] = "Error parsing API response";
                result["Success"] = false;
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fetch repo stats error: {ex.Message}");
                result["Error"] = "Unexpected error";
                result["Success"] = false;
                return result;
            }
        }

        /// <summary>
        /// Fetches repository readme content
        /// </summary>
        public async Task<string?> FetchReadme(string ownerRepo)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ownerRepo))
                    return null;

                var parts = ownerRepo.Split('/');
                if (parts.Length != 2)
                    return null;

                var owner = parts[0].Trim();
                var repo = parts[1].Trim();

                var readmeUrl = $"{GitHubApiBaseUrl}/repos/{owner}/{repo}/readme";
                var response = await _httpClient.GetAsync(readmeUrl);

                if (!response.IsSuccessStatusCode)
                    return null;

                var content = await response.Content.ReadAsStringAsync();
                using (var doc = JsonDocument.Parse(content))
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("content", out var contentProp))
                    {
                        var base64Content = contentProp.GetString();
                        if (!string.IsNullOrEmpty(base64Content))
                        {
                            var decodedBytes = Convert.FromBase64String(base64Content);
                            return System.Text.Encoding.UTF8.GetString(decodedBytes);
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fetch readme error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Checks if a repository exists on GitHub
        /// </summary>
        public async Task<bool> RepositoryExists(string ownerRepo)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ownerRepo))
                    return false;

                var parts = ownerRepo.Split('/');
                if (parts.Length != 2)
                    return false;

                var owner = parts[0].Trim();
                var repo = parts[1].Trim();

                var url = $"{GitHubApiBaseUrl}/repos/{owner}/{repo}";
                var response = await _httpClient.GetAsync(url);

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Repository exists check error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Fetches recent commits from a repository
        /// </summary>
        public async Task<List<Dictionary<string, string>>> FetchRecentCommits(string ownerRepo, int count = 10)
        {
            var commits = new List<Dictionary<string, string>>();

            try
            {
                if (string.IsNullOrWhiteSpace(ownerRepo))
                    return commits;

                var parts = ownerRepo.Split('/');
                if (parts.Length != 2)
                    return commits;

                var owner = parts[0].Trim();
                var repo = parts[1].Trim();

                var url = $"{GitHubApiBaseUrl}/repos/{owner}/{repo}/commits?per_page={count}";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                    return commits;

                var content = await response.Content.ReadAsStringAsync();
                using (var doc = JsonDocument.Parse(content))
                {
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var commit in doc.RootElement.EnumerateArray())
                        {
                            var commitDict = new Dictionary<string, string>();
                            commitDict["Sha"] = commit.GetProperty("sha").GetString() ?? "N/A";
                            commitDict["Message"] = commit.GetProperty("commit").GetProperty("message").GetString() ?? "N/A";
                            commitDict["Author"] = commit.GetProperty("commit").GetProperty("author").GetProperty("name").GetString() ?? "Unknown";
                            commitDict["Date"] = commit.GetProperty("commit").GetProperty("author").GetProperty("date").GetString() ?? "N/A";

                            commits.Add(commitDict);
                        }
                    }
                }

                return commits;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fetch recent commits error: {ex.Message}");
                return commits;
            }
        }
    }
}
