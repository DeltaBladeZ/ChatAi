using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TaleWorlds.Library;

namespace ChatAi
{
    public static class AIHelper
    {
        private static readonly string _logFilePath = Path.Combine(BasePath.Name, "Modules", "ChatAi", "mod_log.txt");

        private static void LogMessage(string message)
        {
            try
            {
                if (!ChatAiSettings.Instance.EnableDebugLogging)
                {
                    return;
                }

                string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}\n";
                File.AppendAllText(_logFilePath, logMessage);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"Logging error: {ex.Message}"));
            }
        }

        public static async Task<string> GetResponse(string prompt)
        {
            var settings = ChatAiSettings.Instance;
            string backend = settings.AIBackend.SelectedValue;

            LogMessage($"DEBUG: Selected backend: {backend}");

            string responseText = string.Empty;

            try
            {
                switch (backend)
                {
                    case "OpenAI":
                        responseText = await GetOpenAIResponse(prompt);
                        break;
                    case "KoboldCpp":
                        responseText = await GetKoboldCppResponse(prompt);
                        break;
                    case "OpenRouter":
                        responseText = await GetOpenRouterResponse(prompt);
                        break;
                    case "Ollama":
                        responseText = await GetOllamaResponse(prompt);
                        break;
                    default:
                        responseText = "Error: Invalid AI backend selected.";
                        break;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"DEBUG: Exception in GetResponse: {ex.Message}");
                responseText = $"Error: {ex.Message}";
            }

            return responseText;
        }

        private static async Task<string> GetOpenRouterResponse(string prompt)
        {
            string endpoint = "https://openrouter.ai/api/v1/chat/completions";

            var settings = ChatAiSettings.Instance;
            string apiKey = settings.OpenRouterAPIKey;
            string model = settings.OpenRouterModel;
            int maxTokens = settings.MaxTokens;

            LogMessage($"DEBUG: OpenRouter Endpoint: {endpoint}");
            LogMessage($"DEBUG: OpenRouter Model: {model}");
            LogMessage($"DEBUG: OpenRouter Max Tokens: {maxTokens}");

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return "Error: OpenRouter API key is not set. Please configure it in the Mod Options menu.";
            }

            var payload = new
            {
                model = model,
                max_tokens = maxTokens,
                temperature = 0.7,
                messages = new[]
                {
                    new { role = "system", content = "You are a medieval NPC in a fantasy world. Stay in character and maintain a medieval tone." },
                    new { role = "user", content = prompt }
                }
            };

            var jsonPayload = JsonConvert.SerializeObject(payload);
            LogMessage($"DEBUG: OpenRouter Payload: {jsonPayload}");

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
            };

            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            // Optional leaderboard headers - update these
            if (!string.IsNullOrWhiteSpace("https://your-site-url.com"))
            {
                request.Headers.Add("HTTP-Referer", "https://your-site-url.com");
            }
            if (!string.IsNullOrWhiteSpace("Your-Site-Name"))
            {
                request.Headers.Add("X-Title", "Your-Site-Name");
            }

            using var httpClient = new HttpClient();
            var response = await httpClient.SendAsync(request);

            LogMessage($"DEBUG: OpenRouter Response Status: {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                var errorDetails = await response.Content.ReadAsStringAsync();
                LogMessage($"DEBUG: OpenRouter Error Response: {errorDetails}");
                InformationManager.DisplayMessage(new InformationMessage($"OpenRouter request failed with status {response.StatusCode}. Details: {errorDetails}"));
                return $"Error: OpenRouter request failed with status {response.StatusCode}. Details: {errorDetails}";
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            LogMessage($"DEBUG: OpenRouter Raw Response: {responseBody}");

            var jsonResponse = JsonConvert.DeserializeObject<ApiResponse>(responseBody);

            return jsonResponse?.choices?.FirstOrDefault()?.message?.content ?? "Error: No response received from OpenRouter.";
        }


        private static async Task<string> GetOpenAIResponse(string prompt)
        {
            var settings = ChatAiSettings.Instance;
            string apiKey = settings.OpenAIAPIKey;
            string model = settings.OpenAIModel.SelectedValue;
            int maxTokens = settings.MaxTokens;

            LogMessage($"DEBUG: OpenAI Model: {model}");
            LogMessage($"DEBUG: OpenAI Max Tokens: {maxTokens}");

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                InformationManager.DisplayMessage(new InformationMessage("OpenAI API key is not set. Please configure it in the Mod Options menu."));
                return "Error: OpenAI API key is not set. Please configure it in the Mod Options menu.";
            }

            var payload = new
            {
                model = model,
                max_tokens = maxTokens,
                temperature = 0.7,
                messages = new[]
                {
                    new { role = "system", content = "You are a medieval NPC in a fantasy world. Stay in character and maintain a medieval tone." },
                    new { role = "user", content = prompt }
                }
            };

            var jsonPayload = JsonConvert.SerializeObject(payload);
            LogMessage($"DEBUG: OpenAI Payload: {jsonPayload}");

            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            var response = await httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);

            LogMessage($"DEBUG: OpenAI Response Status: {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                var errorDetails = await response.Content.ReadAsStringAsync();
                LogMessage($"DEBUG: OpenAI Error Response: {errorDetails}");
                InformationManager.DisplayMessage(new InformationMessage($"OpenAI request failed with status {response.StatusCode}. Details: {errorDetails}"));
                return $"Error: OpenAI request failed with status {response.StatusCode}. Details: {errorDetails}";
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            LogMessage($"DEBUG: OpenAI Raw Response: {responseBody}");

            var jsonResponse = JsonConvert.DeserializeObject<ApiResponse>(responseBody);

            return jsonResponse?.choices?.FirstOrDefault()?.message?.content ?? "Error: No response received from OpenAI.";
        }

        private static async Task<string> GetKoboldCppResponse(string prompt)
        {
            var settings = ChatAiSettings.Instance;
            string localModelUrl = settings.LocalModelURL;
            int maxTokens = settings.MaxTokens;

            LogMessage($"DEBUG: KoboldCpp URL: {localModelUrl}");

            var payload = new
            {
                prompt = prompt,
                temperature = 0.7,
                max_new_tokens = maxTokens,
                top_p = 0.9, // Nucleus sampling
                stop_sequence = new[] { "\n\n" }
            };

            var jsonPayload = JsonConvert.SerializeObject(payload);
            LogMessage($"DEBUG: KoboldCpp Payload: {jsonPayload}");

            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            using var httpClient = new HttpClient();
            var response = await httpClient.PostAsync(localModelUrl, content);

            LogMessage($"DEBUG: KoboldCpp Response Status: {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                var errorDetails = await response.Content.ReadAsStringAsync();
                LogMessage($"DEBUG: KoboldCpp Error Response: {errorDetails}");
                return $"Error: KoboldCpp request failed with status {response.StatusCode}. Details: {errorDetails}";
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            LogMessage($"DEBUG: KoboldCpp Raw Response: {responseBody}");

            var jsonResponse = JsonConvert.DeserializeObject<KoboldCppResponse>(responseBody);

            return jsonResponse?.results?.FirstOrDefault()?.text ?? "Error: No response received from KoboldCpp.";
        }


        private static async Task<string> GetOllamaResponse(string prompt)
        {
            try
            {
                var settings = ChatAiSettings.Instance;
                string ollamaBaseUrl = settings.OllamaURL;
                string model = settings.OllamaModel;

                LogMessage($"DEBUG: Ollama Base URL: {ollamaBaseUrl}");
                LogMessage($"DEBUG: Ollama Model: {model}");

                // Construct the full URL. The /api/chat will be added from settings
                string ollamaEndpoint = $"{ollamaBaseUrl}";

                // Prepare the payload with the messages array
                var payload = new
                {
                    model = model,
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    }
                    // ... other Ollama parameters if needed ...
                };

                var jsonPayload = JsonConvert.SerializeObject(payload);
                LogMessage($"DEBUG: Ollama Payload: {jsonPayload}");

                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                using var httpClient = new HttpClient();
                using var response = await httpClient.PostAsync(ollamaEndpoint, content);

                LogMessage($"DEBUG: Ollama Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorDetails = await response.Content.ReadAsStringAsync();
                    LogMessage($"DEBUG: Ollama Error Response: {errorDetails}");
                    return $"Error: Ollama request failed with status {response.StatusCode}. Details: {errorDetails}";
                }

                // --- Handle Streaming Responses (NDJSON) ---
                response.EnsureSuccessStatusCode();

                string fullResponse = "";
                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);

                while (!reader.EndOfStream)
                {
                    string line = await reader.ReadLineAsync();
                    if (!string.IsNullOrEmpty(line))
                    {
                        // Deserialize each line as an OllamaResponseChunk
                        var chunk = JsonConvert.DeserializeObject<OllamaResponseChunk>(line);
                        fullResponse += chunk.message.content; // Append the 'content' from each chunk
                    }
                }

                LogMessage($"DEBUG: Ollama Full Response: {fullResponse}");
                return fullResponse;

            }
            catch (Exception ex)
            {
                LogMessage($"DEBUG: Exception in GetOllamaResponse: {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }

        // Define the class to deserialize Ollama's response chunks
        public class OllamaResponseChunk
        {
            public string model { get; set; }
            public string created_at { get; set; }
            public OllamaMessage message { get; set; }
            public string done_reason { get; set; }
            public bool done { get; set; }
        }

        public class OllamaMessage
        {
            public string role { get; set; }
            public string content { get; set; }
        }

        public class ApiResponse
        {
            public Choice[] choices { get; set; } = Array.Empty<Choice>();
        }

        public class Choice
        {
            public Message message { get; set; } = new Message();
        }

        public class Message
        {
            public string role { get; set; } = string.Empty;
            public string content { get; set; } = string.Empty;
        }

        public class KoboldCppResponse
        {
            public Result[] results { get; set; } = Array.Empty<Result>();
        }

        public class Result
        {
            public string text { get; set; } = string.Empty;
            public string finish_reason { get; set; } = string.Empty;
            public int prompt_tokens { get; set; }
            public int completion_tokens { get; set; }
        }


    }
}
