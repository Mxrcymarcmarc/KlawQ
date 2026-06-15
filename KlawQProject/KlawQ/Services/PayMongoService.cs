using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace KlawQ.Services
{
    public class PayMongoService
    {
        private readonly HttpClient _httpClient;
        private readonly string _secretKey;

        public PayMongoService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _secretKey = configuration["PayMongo:SecretKey"] ?? throw new ArgumentNullException("PayMongo Secret Key missing.");

            // PayMongo uses Basic Auth where the Secret Key is the username, and the password is left blank
            var authHeaderValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_secretKey}:"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeaderValue);
        }

        public async Task<string> CreateCheckoutSessionAsync(decimal amountInPhp, string description, string successUrl, string cancelUrl)
        {
            // PayMongo reads amounts in CENTAVOS (e.g., 150 PHP = 15000 Centavos)
            long amountInCentavos = (long)(amountInPhp * 100);

            var payload = new
            {
                data = new
                {
                    attributes = new
                    {
                        amount = amountInCentavos,
                        payment_method_types = new[] { "gcash" }, // Limits choices straight to GCash
                        description = description,
                        line_items = new[]
                        {
                            new {
                                amount = amountInCentavos,
                                currency = "PHP",
                                name = "Reservation Fee",
                                quantity = 1
                            }
                        },
                        success_url = successUrl,
                        cancel_url = cancelUrl
                    }
                }
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("https://api.paymongo.com/v1/checkout_sessions", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"PayMongo API Error: {errorContent}");
            }

            var responseString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseString);

            // Dig into the JSON response to grab the unique checkout URL
            return doc.RootElement
                .GetProperty("data")
                .GetProperty("attributes")
                .GetProperty("checkout_url")
                .GetString();
        }
    }
}
