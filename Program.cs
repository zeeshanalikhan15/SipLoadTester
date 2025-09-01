// See https://aka.ms/new-console-template for more information
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace SipLoadTester
{
	class Program
	{
		public class SipSettings
		{
			public string SipDomain { get; set; }
			public string Username { get; set; }
			public string Password { get; set; }
			public string ExternalDomain { get; set; }
			public int CallCount { get; set; } = 100;
		}

		public class LogSettings
		{
			public string LogDirectory { get; set; }
		}

		public class AppConfig
		{
			public SipSettings SipSettings { get; set; }
			public LogSettings LogSettings { get; set; }
		}

		static async Task Main(string[] args)
		{
			// Load config
			var config = await LoadConfig();
			Directory.CreateDirectory(config.LogSettings.LogDirectory);

			Console.WriteLine($"Loaded config for SIP domain: {config.SipSettings.SipDomain}");

			// Initialize SIP transport and add TLS channel
			var sipTransport = new SIPTransport();
			int tlsPort = 5061; // Standard SIP TLS port
			var tlsEndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Any, tlsPort);
			sipTransport.AddSIPChannel(new SIPTLSChannel(tlsEndPoint));


			// Register to SIP server
			Console.WriteLine("Registering to SIP server...");
			// Prepare registration details (official SIPSorcery example)
			string server = $"sips:{config.SipSettings.SipDomain}:5061";
			string username = config.SipSettings.Username;
			string password = config.SipSettings.Password;
			int expiry = 10000;


			// Enable SIPSorcery internal logging to console using Serilog
			var serilogLogger = new LoggerConfiguration()
				.Enrich.FromLogContext()
				.MinimumLevel.Is(LogEventLevel.Verbose)
				.WriteTo.Console()
				.CreateLogger();
			var factory = new Serilog.Extensions.Logging.SerilogLoggerFactory(serilogLogger);
			SIPSorcery.LogFactory.Set(factory);

			// Try both username and username@domain for auth
			string[] usernameVariants = { username, $"{username}@{config.SipSettings.SipDomain}" };
			bool registrationResult = false;
			foreach (var authUser in usernameVariants)
			{
				Console.WriteLine($"Trying registration with auth username: {authUser}");

				var regUserAgent = new SIPRegistrationUserAgent(
					sipTransport,
					username,
					password,
					server,
					expiry
				);

				var registrationTcs = new TaskCompletionSource<bool>();
				regUserAgent.RegistrationSuccessful += (uri, resp) => {
					Console.WriteLine("SIP registration successful.");
					registrationTcs.TrySetResult(true);
				};
				regUserAgent.RegistrationFailed += (uri, resp, err) => {
					Console.WriteLine($"SIP registration failed: {err}");
					registrationTcs.TrySetResult(false);
				};

				regUserAgent.Start();
				// Wait for registration or timeout after 10 seconds
				var completedTask = await Task.WhenAny(registrationTcs.Task, Task.Delay(10000));
				if (completedTask == registrationTcs.Task && registrationTcs.Task.Result)
				{
					registrationResult = true;
					break;
				}
				else if (completedTask != registrationTcs.Task)
				{
					Console.WriteLine("SIP registration timed out for this username.");
				}
			}

			if (!registrationResult)
			{
				Console.WriteLine("SIP registration failed for all username formats. Exiting.");
				return;
			}


			// Make outbound calls using external domain
			var callMaker = new CallMaker(
				sipTransport,
				config.SipSettings.Username,
				config.SipSettings.Password,
				config.SipSettings.SipDomain,
				config.SipSettings.ExternalDomain
			);

			for (int i = 0; i < config.SipSettings.CallCount; i++)
			{
				Console.WriteLine($"Starting call {i + 1} of {config.SipSettings.CallCount}...");
				await callMaker.MakeCall();
			}

			Console.WriteLine("Press Enter to exit...");
			Console.ReadLine();

	
			// Cleanup
			sipTransport.Shutdown();
			Console.WriteLine("All calls completed.");
		}

		static async Task<AppConfig> LoadConfig()
		{
			var configText = await File.ReadAllTextAsync("appsettings.json");
			return JsonSerializer.Deserialize<AppConfig>(configText);
		}
	}
}
