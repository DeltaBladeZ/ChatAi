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

            return backend switch
            {
                "OpenAI" => await GetOpenAIResponse(prompt),
                "KoboldCpp" => await GetKoboldCppResponse(prompt),
                "LocalAI" => await GetLocalAIResponse(prompt),
                "OpenRouter" => await GetOpenRouterResponse(prompt),
                _ => "Error: Invalid AI backend selected."
            };
        }

        private static async Task<string> GetOpenRouterResponse(string prompt)
        {
            try
            {
                string endpoint = "https://openrouter.ai/api/v1/chat/completions"; // Hardcoded

                // Fetch settings
                var settings = ChatAiSettings.Instance;
                string apiKey = settings.OpenRouterAPIKey;
                string model = settings.OpenRouterModel; 
                int maxTokens = settings.MaxTokens;

                // Debug logs for settings
                LogMessage($"DEBUG: OpenRouter Endpoint: {endpoint}");
                LogMessage($"DEBUG: OpenRouter Model: {model}");
                LogMessage($"DEBUG: OpenRouter Max Tokens: {maxTokens}");

                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    return "Error: OpenRouter API key is not set. Please configure it in the Mod Options menu.";
                }

                // Prepare the payload
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

                // Create HTTP request
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                };

                // Add headers
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

                // Optional leaderboard headers
                if (!string.IsNullOrWhiteSpace("https://your-site-url.com"))
                {
                    request.Headers.Add("HTTP-Referer", "https://your-site-url.com");
                }
                if (!string.IsNullOrWhiteSpace("Your-Site-Name"))
                {
                    request.Headers.Add("X-Title", "Your-Site-Name");
                }

                // Send HTTP request
                using var httpClient = new HttpClient();
                var response = await httpClient.SendAsync(request);

                LogMessage($"DEBUG: OpenRouter Response Status: {response.StatusCode}");

                // Handle response
                if (!response.IsSuccessStatusCode)
                {
                    var errorDetails = await response.Content.ReadAsStringAsync();
                    LogMessage($"DEBUG: OpenRouter Error Response: {errorDetails}");
                    InformationManager.DisplayMessage(new InformationMessage($"OpenRouter request failed with status {response.StatusCode}. Details: {errorDetails}"));
                    return $"Error: OpenRouter request failed with status {response.StatusCode}. Details: {errorDetails}";
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                LogMessage($"DEBUG: OpenRouter Raw Response: {responseBody}");

                var jsonResponse = JsonConvert.DeserializeObject<OpenAIResponse>(responseBody);

                // Extract and return the response message
                var responseMessage = jsonResponse?.choices?.FirstOrDefault()?.message?.content ?? "Error: No response received from OpenRouter.";
                LogMessage($"DEBUG: OpenRouter Extracted Response: {responseMessage}");
                return responseMessage;
            }
            catch (Exception ex)
            {
                LogMessage($"DEBUG: Exception in GetOpenRouterResponse: {ex.Message}");
                InformationManager.DisplayMessage(new InformationMessage($"Error: {ex.Message}"));
                return $"Error: {ex.Message}";
            }
        }


        private static async Task<string> GetOpenAIResponse(string prompt)
        {
            try
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

                // Prepare payload
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

                var jsonResponse = JsonConvert.DeserializeObject<OpenAIResponse>(responseBody);

                var responseMessage = jsonResponse?.choices?.FirstOrDefault()?.message?.content ?? "Error: No response received from OpenAI.";
                LogMessage($"DEBUG: OpenAI Extracted Response: {responseMessage}");
                return responseMessage;
            }
            catch (Exception ex)
            {
                LogMessage($"DEBUG: Exception in GetOpenAIResponse: {ex.Message}");
                InformationManager.DisplayMessage(new InformationMessage($"Error: {ex.Message}"));
                return $"Error: {ex.Message}";
            }
        }

        private static async Task<string> GetKoboldCppResponse(string prompt)
        {
            try
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

                var responseMessage = jsonResponse?.results?.FirstOrDefault()?.text ?? "Error: No response received from KoboldCpp.";
                LogMessage($"DEBUG: KoboldCpp Extracted Response: {responseMessage}");
                return responseMessage;
            }
            catch (Exception ex)
            {
                LogMessage($"DEBUG: Exception in GetKoboldCppResponse: {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }


        // Response class for KoboldCpp
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


        private static async Task<string> GetLocalAIResponse(string prompt)
        {
            try
            {
                var settings = ChatAiSettings.Instance;
                string localAIUrl = settings.LocalAIUrl;

                LogMessage($"DEBUG: LocalAI URL: {localAIUrl}");

                var payload = new
                {
                    model = "llama-7b",
                    max_tokens = 500,
                    temperature = 0.7,
                    messages = new[]
                    {
                        new { role = "system", content = "You are a medieval NPC in a fantasy world." },
                        new { role = "user", content = prompt }
                    }
                };

                var jsonPayload = JsonConvert.SerializeObject(payload);
                LogMessage($"DEBUG: LocalAI Payload: {jsonPayload}");

                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                using var httpClient = new HttpClient();
                var response = await httpClient.PostAsync(localAIUrl, content);

                LogMessage($"DEBUG: LocalAI Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorDetails = await response.Content.ReadAsStringAsync();
                    LogMessage($"DEBUG: LocalAI Error Response: {errorDetails}");
                    InformationManager.DisplayMessage(new InformationMessage($"LocalAI request failed with status {response.StatusCode}. Details: {errorDetails}"));
                    return $"Error: LocalAI request failed with status {response.StatusCode}. Details: {errorDetails}";
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                LogMessage($"DEBUG: LocalAI Raw Response: {responseBody}");

                var jsonResponse = JsonConvert.DeserializeObject<OpenAIResponse>(responseBody);

                var responseMessage = jsonResponse?.choices?.FirstOrDefault()?.message?.content ?? "Error: No response received from LocalAI.";
                LogMessage($"DEBUG: LocalAI Extracted Response: {responseMessage}");
                return responseMessage;
            }
            catch (Exception ex)
            {
                LogMessage($"DEBUG: Exception in GetLocalAIResponse: {ex.Message}");
                InformationManager.DisplayMessage(new InformationMessage($"Error: {ex.Message}"));
                return $"Error: {ex.Message}";
            }
        }
    }

    public class LocalModelResponse
    {
        public string content { get; set; } = string.Empty;
    }


}
