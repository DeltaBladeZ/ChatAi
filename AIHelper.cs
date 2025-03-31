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
using TaleWorlds.LinQuick;

namespace ChatAi
{
    public static class AIHelper
    {
        private static readonly string _logFilePath = Path.Combine(BasePath.Name, "Modules", "ChatAi", "mod_log.txt");

        private static void LogMessage(string message)
        {
            try
            {
                // Check if debug logging is enabled in the settings
                if (!ChatAiSettings.Instance.EnableDebugLogging)
                {
                    return; // Skip logging if disabled
                }

                string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}\n";

                // Determine the log file path dynamically
                string logFilePath = _logFilePath; // Default path
                string logDirectory = Path.GetDirectoryName(logFilePath);

                if (!Directory.Exists(logDirectory))
                {
                    // If the directory does not exist, fall back to the desktop
                    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string desktopLogDirectory = Path.Combine(desktopPath, "ChatAiLogs");

                    if (!Directory.Exists(desktopLogDirectory))
                    {
                        Directory.CreateDirectory(desktopLogDirectory);
                    }

                    logFilePath = Path.Combine(desktopLogDirectory, "mod_log.txt");
                }

                // Write the log message to the file
                File.AppendAllText(logFilePath, logMessage);
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
            string model = settings.KoboldCppModel;
            int maxTokens = settings.MaxTokens;

            LogMessage($"DEBUG: KoboldCpp URL: {localModelUrl}");
            LogMessage($"DEBUG: KoboldCpp Model: {model}");
            LogMessage($"DEBUG: KoboldCpp Max Tokens (From Settings): {maxTokens}");

            var payload = new
            {
                prompt = prompt,
                temperature = 0.7,
                max_length = maxTokens, // Ensure this uses the latest value
                top_p = 0.9,
                stop_sequence = new[] { "" }
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
                string model = settings.OllamaModel;  // Get selected Ollama model

                LogMessage($"DEBUG: Ollama Base URL: {ollamaBaseUrl}");
                LogMessage($"DEBUG: Ollama Model: {model}");

                string ollamaEndpoint = $"{ollamaBaseUrl}";

                var payload = new
                {
                    model = model,
                    messages = new[]
                    {
                new { role = "user", content = prompt }
            }
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

                string fullResponse = "";
                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);

                while (!reader.EndOfStream)
                {
                    string line = await reader.ReadLineAsync();
                    if (!string.IsNullOrEmpty(line))
                    {
                        var chunk = JsonConvert.DeserializeObject<OllamaResponseChunk>(line);
                        fullResponse += chunk.message.content;
                    }
                }

                LogMessage($"DEBUG: Ollama Full Response: {fullResponse}");

                // If DeepSeek is being used, clean the response
                if (model.IndexOf("deepseek", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    fullResponse = CleanDeepSeekResponse(fullResponse);
                }

                return fullResponse;
            }
            catch (Exception ex)
            {
                LogMessage($"DEBUG: Exception in GetOllamaResponse: {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }



        private static string CleanDeepSeekResponse(string rawResponse)
        {
            // Find the last occurrence of "</think>" to extract the final response
            int lastThinkEndIndex = rawResponse.LastIndexOf("</think>", StringComparison.OrdinalIgnoreCase);

            if (lastThinkEndIndex != -1)
            {
                // Extract everything after the last "</think>" tag
                string cleanedResponse = rawResponse.Substring(lastThinkEndIndex + "</think>".Length).Trim();

                LogMessage($"DEBUG: Cleaned DeepSeek Response: {cleanedResponse}");

                return cleanedResponse;
            }

            // If no "</think>" tag is found, return the full response as fallback
            return rawResponse.Trim();
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
