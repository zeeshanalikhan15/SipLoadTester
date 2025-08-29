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

            Console.ReadLine();

			// Loop for the number of calls
			/*for (int i = 0; i < config.SipSettings.CallCount; i++)
			{
				Console.WriteLine($"Starting call {i + 1} of {config.SipSettings.CallCount}");

				// Prepare SIP call
				var callDescriptor = new SIPCallDescriptor(
					config.SipSettings.Username,
					config.SipSettings.Password,
					$"sip:{config.SipSettings.ExternalDomain}",
					$"sip:{config.SipSettings.Username}@{config.SipSettings.SipDomain}",
					null, // from display name
					null, // to display name
					null, // custom headers
					SIPCallDirection.Out,
					SDP.SDP_MIME_CONTENTTYPE,
					null, // SDP body (null for now)
					null, // auth username
					null, // auth password
					null, // proxy
					null  // user agent
				);

				var userAgent = new SIPUserAgent(sipTransport, null);
				string logFile = Path.Combine(config.LogSettings.LogDirectory, $"call_{i + 1}.log");
				var sipLog = new StringWriter();

				// Attach SIP message logging
				EventHandler<SIPMessageEventArgs> logHandler = (s, e) =>
				{
					sipLog.WriteLine($"{e.Direction} {e.RemoteEndPoint}: {e.SIPMessage.StatusLine ?? e.SIPMessage.FirstLine}" );
					sipLog.WriteLine(e.SIPMessage.RawMessage);
				};
				sipTransport.SIPMessageReceived += logHandler;
				sipTransport.SIPMessageSent += logHandler;

				try
				{
					// Place the call
					bool callResult = await userAgent.InitiateCall(callDescriptor);
					if (!callResult)
					{
						Console.WriteLine($"Call {i + 1} failed to initiate.");
						continue;
					}

					// Wait for media negotiation (RTP about to start)
					// For demo, wait for call to be confirmed (200 OK)
					var callAnswered = await userAgent.WaitForCallAnswered();
					if (callAnswered)
					{
						Console.WriteLine($"Call {i + 1} answered, disconnecting before RTP starts.");
						await userAgent.Hangup();
					}
					else
					{
						Console.WriteLine($"Call {i + 1} not answered, moving on.");
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Error during call {i + 1}: {ex.Message}");
				}
				finally
				{
					// Detach logging
					sipTransport.SIPMessageReceived -= logHandler;
					sipTransport.SIPMessageSent -= logHandler;
					// Save SIP logs
					await File.WriteAllTextAsync(logFile, sipLog.ToString());
					Console.WriteLine($"Call {i + 1} completed and logs saved.");
				}
			}
            */

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
