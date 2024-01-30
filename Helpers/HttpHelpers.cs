using System.Collections.Generic;
using System.Net;
using System.Text;
using Playlistic.Models;
using System.Net.Http;
using System;
using System.Text.Json;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using Newtonsoft.Json;

namespace Playlistic.Helpers
{
    public static class HttpHelpers
    {
        public static async Task<List<SpotifySearchObject>> MakeOpenAIRequest(string OpenAIAssistantSetupString, string OpenAIReadyInputListString, string OpenAIAccessToken)
        {
            List<SpotifySearchObject> spotifySearchObjects = new();

            var payload = new
            {
                model = "gpt-4-turbo-preview",
                temperature = 2,
                max_tokens = 2048,
                top_p = 0,
                frequency_penalty = 0,
                presence_penalty = 0,
                messages = new[]
                {
                    new {
                            role = "system",
                            content = OpenAIAssistantSetupString
                        },
                    new {
                            role = "user",
                            content = OpenAIReadyInputListString
                        }
                }
            };
            using HttpClient httpClient = new();
            httpClient.Timeout = new TimeSpan(0, 0, 300);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", OpenAIAccessToken);
            string payloadString = System.Text.Json.JsonSerializer.Serialize(payload);
            StringContent payloadContent = new(payloadString, Encoding.UTF8, "application/json");
            HttpResponseMessage httpResponseMessage = await httpClient.PostAsync(new Uri("https://api.openai.com/v1/chat/completions"), payloadContent);

            if (httpResponseMessage.StatusCode == HttpStatusCode.OK)
            {
                string AIPlaylistGenerationResponse = await httpResponseMessage.Content.ReadAsStringAsync();
                OpenAIResult openAIResult = JsonConvert.DeserializeObject<OpenAIResult>(AIPlaylistGenerationResponse);
                string rawResult = openAIResult.choices[0].message.content;
                JsonAIResult jsonAIResult = JsonConvert.DeserializeObject<JsonAIResult>(rawResult);
                spotifySearchObjects = jsonAIResult.spotifySearchObjects;
            }
            return spotifySearchObjects;
        }
    }
}
