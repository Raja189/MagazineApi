using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Linq;
using System.Text;

namespace MagazineSubscriptionApp
{
   static class Program
    {
        static string baseUrl = "http://magazinestore.azurewebsites.net";

        // using asynchronous calls for paralleizing the Api calls
        static async Task Main(string[] args)
        {
            // Get token
            string token = await GetTokenAsync();

            // Get categories
            List<string> categories = await GetCategoriesAsync(token);

            // Get magazines for each category
            Dictionary<string, List<Magazine>> magazinesByCategory = new Dictionary<string, List<Magazine>>();
            foreach (var category in categories)
            {
                List<Magazine> magazines = await GetMagazinesAsync(token, category);
                magazinesByCategory.Add(category, magazines);
            }

            // Get subscribers and their subscriptions
            List<Subscriber> subscribers = await GetSubscribersAsync(token);

            // Identify subscribers with at least one subscription in each category
            List<string> qualifiedSubscriberIds = IdentifyQualifiedSubscribers(subscribers, magazinesByCategory);

            // Submit the answer
            await SubmitAnswerAsync(token, qualifiedSubscriberIds);
        }
        // Async 
        static async Task<string> GetTokenAsync()
        {
            using (HttpClient client = new HttpClient())
            {
                string tokenEndpoint = $"{baseUrl}/api/token";
                HttpResponseMessage response = await client.GetAsync(tokenEndpoint);
                response.EnsureSuccessStatusCode();
                string jsonResult = await response.Content.ReadAsStringAsync();
                var tokenResult = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonResult);

                if (tokenResult.TryGetValue("token", out var token))
                {
                    return token.ToString();
                }
                else
                    return "";
            }
            
        }

        static async Task<List<string>> GetCategoriesAsync(string token)
        {
            using (HttpClient client = new HttpClient())
            {
                string categoriesEndpoint = $"{baseUrl}/api/categories/{token}";
                HttpResponseMessage response = await client.GetAsync(categoriesEndpoint);
                response.EnsureSuccessStatusCode();
                string jsonResult = await response.Content.ReadAsStringAsync();
                var jsonDocument = JsonDocument.Parse(jsonResult);

                if (jsonDocument.RootElement.TryGetProperty("data", out var dataElement))
                {
                    return dataElement.EnumerateArray().Select(categoryElement => categoryElement.GetString()).ToList();
                }
                else
                    return null;
            }
        }

        static async Task<List<Magazine>> GetMagazinesAsync(string token, string category)
        {
            using (HttpClient client = new HttpClient())
            {
                string magazinesEndpoint = $"{baseUrl}/api/magazines/{token}/{category}";
                HttpResponseMessage response = await client.GetAsync(magazinesEndpoint);
                response.EnsureSuccessStatusCode();
                string jsonResult = await response.Content.ReadAsStringAsync();
                var jsonDocument = JsonDocument.Parse(jsonResult);

                if (jsonDocument.RootElement.TryGetProperty("data", out var dataElement))
                {
                    var result = JsonSerializer.Deserialize<List<Magazine>>(dataElement.GetRawText());
                    return result;
                }
                else
                    return null;
            }
        }

        static async Task<List<Subscriber>> GetSubscribersAsync(string token)
        {
            using (HttpClient client = new HttpClient())
            {
                string subscribersEndpoint = $"{baseUrl}/api/subscribers/{token}";
                HttpResponseMessage response = await client.GetAsync(subscribersEndpoint);
                response.EnsureSuccessStatusCode();
                string jsonResult = await response.Content.ReadAsStringAsync();
                var jsonDocument = JsonDocument.Parse(jsonResult);

                if (jsonDocument.RootElement.TryGetProperty("data", out var dataElement))
                {
                    return JsonSerializer.Deserialize<List<Subscriber>>(dataElement.GetRawText());
                }
                else
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
            using (HttpClient client = new HttpClient())
            {
                string answersEndpoint = $"{baseUrl}/api/answer/{token}";

                // Set the content type header to application/json
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                // Serialize the list to JSON
                var content = new StringContent(JsonSerializer.Serialize(qualifiedSubscriberIds), Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync(answersEndpoint, content);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadAsStringAsync();
                Console.WriteLine(result);
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