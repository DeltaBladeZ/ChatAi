using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TaleWorlds.Library;

namespace ChatAi
{
	public class Player2SpeechToText
	{
		private static readonly HttpClient _httpClient = new HttpClient();
		private const string GameKey = "BannerlordChatAI";
		private static readonly string _logFilePath = PathHelper.GetModFilePath("mod_log.txt");

		private class LanguageInfo
		{
			public string code { get; set; }
			public string name { get; set; }
		}

		private class LanguageListResponse
		{
			public List<LanguageInfo> languages { get; set; }
		}

		private class LanguageResponse
		{
			public string code { get; set; }
			public string name { get; set; }
		}

		private class StartRequest
		{
			public int timeout { get; set; }
		}

		private class StopResponse
		{
			public string text { get; set; }
		}

		private static void Log(string message)
		{
			try
			{
				if (!ChatAi.SettingsUtil.IsDebugLoggingEnabled())
				{
					return;
				}

				System.IO.File.AppendAllText(_logFilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}\n");
			}
			catch { }
		}

		private static HttpRequestMessage CreateRequest(HttpMethod method, string path, HttpContent content = null)
		{
			string baseUrl = ChatAiSettings.Instance.Player2ApiUrl?.TrimEnd('/') ?? "http://localhost:4315";
			var req = new HttpRequestMessage(method, $"{baseUrl}{path}");
			req.Headers.Add("player2-game-key", GameKey);
			if (content != null)
			{
				req.Content = content;
			}
			return req;
		}

		public async Task<(string code, string name)> GetLanguageAsync()
		{
			try
			{
				using var req = CreateRequest(HttpMethod.Get, "/v1/stt/language");
				var resp = await _httpClient.SendAsync(req);
				var body = await resp.Content.ReadAsStringAsync();
				Log($"[STT] GET /stt/language -> {resp.StatusCode} {body}");
				resp.EnsureSuccessStatusCode();
				var data = JsonConvert.DeserializeObject<LanguageResponse>(body);
				return (data?.code, data?.name);
			}
			catch (Exception ex)
			{
				InformationManager.DisplayMessage(new InformationMessage($"STT get language failed: {ex.Message}"));
				return (null, null);
			}
		}

		public async Task<bool> SetLanguageAsync(string code)
		{
			try
			{
				var payload = JsonConvert.SerializeObject(new { code });
				using var req = CreateRequest(HttpMethod.Post, "/v1/stt/language", new StringContent(payload, Encoding.UTF8, "application/json"));
				var resp = await _httpClient.SendAsync(req);
				var body = await resp.Content.ReadAsStringAsync();
				Log($"[STT] POST /stt/language {payload} -> {resp.StatusCode} {body}");
				return resp.IsSuccessStatusCode;
			}
			catch (Exception ex)
			{
				InformationManager.DisplayMessage(new InformationMessage($"STT set language failed: {ex.Message}"));
				return false;
			}
		}

		public async Task<List<(string code, string name)>> ListLanguagesAsync()
		{
			var result = new List<(string code, string name)>();
			try
			{
				using var req = CreateRequest(HttpMethod.Get, "/v1/stt/languages");
				var resp = await _httpClient.SendAsync(req);
				var body = await resp.Content.ReadAsStringAsync();
				Log($"[STT] GET /stt/languages -> {resp.StatusCode} {body}");
				resp.EnsureSuccessStatusCode();
				var data = JsonConvert.DeserializeObject<LanguageListResponse>(body);
				if (data?.languages != null)
				{
					foreach (var lang in data.languages)
					{
						result.Add((lang.code, lang.name));
					}
				}
			}
			catch (Exception ex)
			{
				InformationManager.DisplayMessage(new InformationMessage($"STT list languages failed: {ex.Message}"));
			}
			return result;
		}

		public async Task<bool> StartAsync(int timeoutSeconds)
		{
			try
			{
				var payload = JsonConvert.SerializeObject(new StartRequest { timeout = timeoutSeconds });
				using var req = CreateRequest(HttpMethod.Post, "/v1/stt/start", new StringContent(payload, Encoding.UTF8, "application/json"));
				var resp = await _httpClient.SendAsync(req);
				var body = await resp.Content.ReadAsStringAsync();
				Log($"[STT] POST /stt/start {payload} -> {resp.StatusCode} {body}");
				if (!resp.IsSuccessStatusCode)
				{
					InformationManager.DisplayMessage(new InformationMessage($"STT start failed: {resp.StatusCode}"));
					return false;
				}
				return true;
			}
			catch (Exception ex)
			{
				InformationManager.DisplayMessage(new InformationMessage($"STT start error: {ex.Message}"));
				return false;
			}
		}

		public async Task<string> StopAsync()
		{
			try
			{
				using var req = CreateRequest(HttpMethod.Post, "/v1/stt/stop");
				var resp = await _httpClient.SendAsync(req);
				var body = await resp.Content.ReadAsStringAsync();
				Log($"[STT] POST /stt/stop -> {resp.StatusCode} {body}");
				if (!resp.IsSuccessStatusCode)
				{
					InformationManager.DisplayMessage(new InformationMessage($"STT stop failed: {resp.StatusCode}"));
					return null;
				}
				var data = JsonConvert.DeserializeObject<StopResponse>(body);
				return data?.text;
			}
			catch (Exception ex)
			{
				InformationManager.DisplayMessage(new InformationMessage($"STT stop error: {ex.Message}"));
				return null;
			}
		}
	}
}

