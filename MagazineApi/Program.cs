using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MagazineSubscriptionApp
{
    static class Program
    {
        static readonly string baseUrl = "http://magazinestore.azurewebsites.net";
        static readonly HttpClient httpClient = new HttpClient();

        static async Task Main(string[] args)
        {
            string token = await GetTokenAsync();
            List<string> categories = await GetCategoriesAsync(token);

            var magazinesByCategoryTasks = categories.Select(category => GetMagazinesAsync(token, category));
            var magazinesByCategory = (await Task.WhenAll(magazinesByCategoryTasks))
                .ToDictionary(result => result.Key, result => result.Value);

            List<Subscriber> subscribers = await GetSubscribersAsync(token);

            List<string> qualifiedSubscriberIds = IdentifyQualifiedSubscribers(subscribers, magazinesByCategory);

            await SubmitAnswerAsync(token, qualifiedSubscriberIds);
        }

        static async Task<string> GetTokenAsync()
        {
            try
            {
                HttpResponseMessage response = await httpClient.GetAsync($"{baseUrl}/api/token").ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                string jsonResult = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var tokenResult = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonResult);

                if (tokenResult?.TryGetValue("token", out var token) == true)
                {
                    return token.ToString();
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                // Handle or log the exception
                Console.WriteLine($"Error in GetTokenAsync: {ex.Message}");
                return string.Empty;
            }
        }

        static async Task<List<string>> GetCategoriesAsync(string token)
        {
            try
            {
                HttpResponseMessage response = await httpClient.GetAsync($"{baseUrl}/api/categories/{token}").ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                string jsonResult = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var jsonDocument = JsonDocument.Parse(jsonResult);

                return jsonDocument.RootElement.TryGetProperty("data", out var dataElement)
                    ? dataElement.EnumerateArray().Select(categoryElement => categoryElement.GetString()).ToList()
                    : null;
            }
            catch (Exception ex)
            {
                // Handle or log the exception
                Console.WriteLine($"Error in GetCategoriesAsync: {ex.Message}");
                return null;
            }
        }

        static async Task<KeyValuePair<string, List<Magazine>>> GetMagazinesAsync(string token, string category)
        {
            try
            {
                HttpResponseMessage response = await httpClient.GetAsync($"{baseUrl}/api/magazines/{token}/{category}").ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                string jsonResult = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var jsonDocument = JsonDocument.Parse(jsonResult);

                return jsonDocument.RootElement.TryGetProperty("data", out var dataElement)
                    ? new KeyValuePair<string, List<Magazine>>(category, JsonSerializer.Deserialize<List<Magazine>>(dataElement.GetRawText()))
                    : default;
            }
            catch (Exception ex)
            {
                // Handle or log the exception
                Console.WriteLine($"Error in GetMagazinesAsync for category {category}: {ex.Message}");
                return default;
            }
        }

        static async Task<List<Subscriber>> GetSubscribersAsync(string token)
        {
            try
            {
                HttpResponseMessage response = await httpClient.GetAsync($"{baseUrl}/api/subscribers/{token}").ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                string jsonResult = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var jsonDocument = JsonDocument.Parse(jsonResult);

                return jsonDocument.RootElement.TryGetProperty("data", out var dataElement)
                    ? JsonSerializer.Deserialize<List<Subscriber>>(dataElement.GetRawText())
                    : null;
            }
            catch (Exception ex)
            {
                // Handle or log the exception
                Console.WriteLine($"Error in GetSubscribersAsync: {ex.Message}");
                return null;
            }
        }

        static List<string> IdentifyQualifiedSubscribers(List<Subscriber> subscribers, Dictionary<string, List<Magazine>> magazinesByCategory)
        {
            List<string> qualifiedSubscriberIds = new List<string>();

            foreach (var subscriber in subscribers)
            {
                var subscriberMagazineIds = new HashSet<int>(subscriber.magazineIds);

                bool qualifies = magazinesByCategory.All(categoryPair =>
                    subscriberMagazineIds.Intersect(categoryPair.Value.Select(magazine => magazine.id)).Any());

                if (qualifies)
                {
                    qualifiedSubscriberIds.Add(subscriber.id);
                }
            }

            return qualifiedSubscriberIds;
        }

        static async Task SubmitAnswerAsync(string token, List<string> qualifiedSubscriberIds)
        {
            try
            {
                string answersEndpoint = $"{baseUrl}/api/answer/{token}";

                httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                var content = new StringContent(JsonSerializer.Serialize(qualifiedSubscriberIds), Encoding.UTF8, "application/json");

                HttpResponseMessage response = await httpClient.PostAsync(answersEndpoint, content).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Console.WriteLine(result);
            }
            catch (Exception ex)
            {
                // Handle or log the exception
                Console.WriteLine($"Error in SubmitAnswerAsync: {ex.Message}");
            }
        }
    }

    class Magazine
    {
        public int id { get; set; }
        public string name { get; set; }
        public string category { get; set; }
    }

    class Subscriber
    {
        public string id { get; set; }
        public string firstName { get; set; }
        public string lastName { get; set; }
        public List<int> magazineIds { get; set; }
    }
}
