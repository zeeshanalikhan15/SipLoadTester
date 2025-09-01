using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using SIPSorcery.SIP;
using System.Net;
using System.Collections.Concurrent;

namespace SipLoadTester
{
    public class CallIpData
    {
        public DateTime CallTime { get; set; }
        public string CallId { get; set; } = string.Empty;
        public string DestinationDomain { get; set; } = string.Empty;
        public string ResolvedDestinationIp { get; set; } = string.Empty;
        public List<string> ContactHeaderIps { get; set; } = new List<string>();
        public List<string> RecordRouteIps { get; set; } = new List<string>();
        public List<string> ViaHeaderIps { get; set; } = new List<string>();
        public List<string> SdpMediaIps { get; set; } = new List<string>();
        public List<string> RtpIps { get; set; } = new List<string>();
        public string ServerHeader { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public List<string> AllDetectedIps { get; set; } = new List<string>();
        public string CallStatus { get; set; } = string.Empty;
        public string ResponseCode { get; set; } = string.Empty;
    }

    public class SipMessageLogger
    {
        private readonly string _csvFilePath;
        private readonly object _fileLock = new object();
        private bool _headerWritten = false;
        private readonly Regex _ipRegex = new Regex(@"\b(?:[0-9]{1,3}\.){3}[0-9]{1,3}\b", RegexOptions.Compiled);
        private readonly ConcurrentDictionary<string, CallIpData> _activeCalls = new ConcurrentDictionary<string, CallIpData>();
        private readonly SIPTransport _sipTransport;

        public SipMessageLogger(string csvFilePath, SIPTransport sipTransport)
        {
            _csvFilePath = csvFilePath;
            _sipTransport = sipTransport;
            SetupSipMessageLogging();
        }

        private void SetupSipMessageLogging()
        {
            // Hook into SIP transport to capture all incoming messages
            _sipTransport.SIPRequestInTraceEvent += (localEP, remoteEP, req) =>
            {
                try
                {
                    ProcessSipMessage(req, "Incoming Request", remoteEP?.Address?.ToString());
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing incoming SIP request: {ex.Message}");
                }
            };

            _sipTransport.SIPResponseInTraceEvent += (localEP, remoteEP, resp) =>
            {
                try
                {
                    ProcessSipMessage(resp, "Incoming Response", remoteEP?.Address?.ToString());
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing incoming SIP response: {ex.Message}");
                }
            };

            _sipTransport.SIPRequestOutTraceEvent += (localEP, remoteEP, req) =>
            {
                try
                {
                    ProcessSipMessage(req, "Outgoing Request", remoteEP?.Address?.ToString());
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing outgoing SIP request: {ex.Message}");
                }
            };

            _sipTransport.SIPResponseOutTraceEvent += (localEP, remoteEP, resp) =>
            {
                try
                {
                    ProcessSipMessage(resp, "Outgoing Response", remoteEP?.Address?.ToString());
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing outgoing SIP response: {ex.Message}");
                }
            };
        }

        private void ProcessSipMessage(object sipMessage, string direction, string? remoteIp)
        {
            string callId = "unknown";
            SIPHeader? header = null;
            string? body = null;

            // Extract header and body based on message type
            if (sipMessage is SIPRequest request)
            {
                callId = request.Header.CallId ?? "unknown";
                header = request.Header;
                body = request.Body;
            }
            else if (sipMessage is SIPResponse response)
            {
                callId = response.Header.CallId ?? "unknown";
                header = response.Header;
                body = response.Body;
            }
            else
            {
                return; // Unknown message type
            }
            
            // Get or create call data
            var callData = _activeCalls.GetOrAdd(callId, _ => new CallIpData
            {
                CallId = callId,
                CallTime = DateTime.UtcNow,
                DestinationDomain = _currentDestinationDomain,
                ResolvedDestinationIp = _currentResolvedIp
            });

            // Update call data with new information
            UpdateCallDataFromSipMessage(callData, sipMessage, header, body, remoteIp);

            Console.WriteLine($"{direction} - Call {callId}: Found {callData.AllDetectedIps.Count} unique IPs");
        }

        private void UpdateCallDataFromSipMessage(CallIpData callData, object sipMessage, SIPHeader? header, string? body, string? remoteIp)
        {
            var newIps = new HashSet<string>();

            try
            {
                // Add remote IP if available
                if (!string.IsNullOrEmpty(remoteIp) && IsValidIpAddress(remoteIp))
                {
                    newIps.Add(remoteIp);
                }

                if (header != null)
                {
                    // Extract IPs from Contact headers
                    if (header.Contact != null && header.Contact.Count > 0)
                    {
                        foreach (var contact in header.Contact)
                        {
                            var contactIps = ExtractIpsFromString(contact.ToString());
                            callData.ContactHeaderIps.AddRange(contactIps);
                            newIps.UnionWith(contactIps);
                        }
                    }

                    // Extract IPs from Routes - use alternative property name
                    try
                    {
                        var routes = header.Routes;
                        if (routes != null)
                        {
                            var routeString = routes.ToString();
                            var routeIps = ExtractIpsFromString(routeString);
                            callData.RecordRouteIps.AddRange(routeIps);
                            newIps.UnionWith(routeIps);
                        }
                    }
                    catch
                    {
                        // Ignore if Routes property doesn't exist
                    }

                    // Extract IPs from Via headers
                    if (header.Vias != null)
                    {
                        // Convert SIPViaSet to string and extract IPs
                        var viaString = header.Vias.ToString();
                        var viaIps = ExtractIpsFromString(viaString);
                        callData.ViaHeaderIps.AddRange(viaIps);
                        newIps.UnionWith(viaIps);
                    }

                    // Extract Server header
                    if (!string.IsNullOrEmpty(header.Server))
                    {
                        callData.ServerHeader = header.Server;
                    }

                    // Extract User-Agent header
                    if (!string.IsNullOrEmpty(header.UserAgent))
                    {
                        callData.UserAgent = header.UserAgent;
                    }

                    // Extract response code for responses
                    if (sipMessage is SIPResponse response)
                    {
                        callData.ResponseCode = $"{response.StatusCode} {response.ReasonPhrase}";
                        callData.CallStatus = response.StatusCode < 300 ? "Success" : 
                                            response.StatusCode < 400 ? "Redirection" : 
                                            response.StatusCode < 500 ? "Client Error" : "Server Error";
                    }
                }

                // Extract IPs from SDP content
                if (!string.IsNullOrEmpty(body))
                {
                    var sdpIps = ExtractIpsFromSdp(body);
                    callData.SdpMediaIps.AddRange(sdpIps);
                    newIps.UnionWith(sdpIps);
                }

                // Update all detected IPs (avoid duplicates)
                var existingIps = new HashSet<string>(callData.AllDetectedIps);
                existingIps.UnionWith(newIps);
                callData.AllDetectedIps = existingIps.ToList();

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating call data from SIP message: {ex.Message}");
            }
        }

        public async Task<string> ResolveDestinationIp(string destinationDomain)
        {
            try
            {
                // Remove sip: prefix if present
                var domain = destinationDomain.Replace("sip:", "").Replace("sips:", "");
                
                // Remove port if present
                if (domain.Contains(":"))
                {
                    domain = domain.Split(':')[0];
                }

                var hostEntry = await Dns.GetHostEntryAsync(domain);
                if (hostEntry.AddressList.Length > 0)
                {
                    return hostEntry.AddressList[0].ToString();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to resolve IP for {destinationDomain}: {ex.Message}");
            }
            return "N/A";
        }

        public void StartCall(string callId, string destinationDomain, string resolvedIp)
        {
            var callData = new CallIpData
            {
                CallId = callId,
                CallTime = DateTime.UtcNow,
                DestinationDomain = destinationDomain,
                ResolvedDestinationIp = resolvedIp
            };

            _activeCalls.TryAdd(callId, callData);
            
            // Also store destination domain for later reference
            _currentDestinationDomain = destinationDomain;
            _currentResolvedIp = resolvedIp;
            _currentTrackingCallId = callId;
        }

        private string _currentDestinationDomain = string.Empty;
        private string _currentResolvedIp = string.Empty;
        private string _currentTrackingCallId = string.Empty;

        public async Task FinishCall(string callId)
        {
            if (_activeCalls.TryRemove(callId, out var callData))
            {
                await LogCallData(callData);
            }
            else
            {
                // Log call data for our tracking call ID even if we don't have SIP message data
                var basicCallData = new CallIpData
                {
                    CallId = callId,
                    CallTime = DateTime.UtcNow,
                    DestinationDomain = "Unknown",
                    ResolvedDestinationIp = "N/A"
                };
                await LogCallData(basicCallData);
            }

            // Also log any remaining active calls that might have related data
            var remainingCalls = _activeCalls.ToArray();
            foreach (var kvp in remainingCalls)
            {
                if (kvp.Value.AllDetectedIps.Count > 0)
                {
                    // Update with our call tracking info
                    kvp.Value.CallId = $"{callId}_sip_{kvp.Key}";
                    if (_activeCalls.TryRemove(kvp.Key, out var sipCallData))
                    {
                        await LogCallData(sipCallData);
                    }
                }
            }
        }

        private List<string> ExtractIpsFromString(string text)
        {
            var ips = new List<string>();
            if (string.IsNullOrEmpty(text)) return ips;

            var matches = _ipRegex.Matches(text);
            foreach (Match match in matches)
            {
                if (IsValidIpAddress(match.Value))
                {
                    ips.Add(match.Value);
                }
            }
            return ips;
        }

        private List<string> ExtractIpsFromSdp(string sdpContent)
        {
            var ips = new List<string>();
            if (string.IsNullOrEmpty(sdpContent)) return ips;

            // Look for connection information (c=) and media descriptions (m=)
            var lines = sdpContent.Split('\n');
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Connection information: c=IN IP4 192.168.1.1
                if (trimmedLine.StartsWith("c=IN IP4 ") || trimmedLine.StartsWith("c=IN IP6 "))
                {
                    var parts = trimmedLine.Split(' ');
                    if (parts.Length >= 3)
                    {
                        var ip = parts[2];
                        if (IsValidIpAddress(ip))
                        {
                            ips.Add(ip);
                        }
                    }
                }
                
                // Also extract any IP addresses from the entire SDP content
                ips.AddRange(ExtractIpsFromString(trimmedLine));
            }

            return ips;
        }

        private bool IsValidIpAddress(string ip)
        {
            return IPAddress.TryParse(ip, out _);
        }

        public Task LogCallData(CallIpData callData)
        {
            try
            {
                var csvLine = BuildCsvLine(callData);
                
                lock (_fileLock)
                {
                    // Write header if this is the first entry
                    if (!_headerWritten)
                    {
                        var header = "CallTime,CallId,DestinationDomain,ResolvedDestinationIp,CallStatus,ResponseCode,ContactHeaderIps,RecordRouteIps,ViaHeaderIps,SdpMediaIps,ServerHeader,UserAgent,AllDetectedIps";
                        File.AppendAllText(_csvFilePath, header + Environment.NewLine);
                        _headerWritten = true;
                    }
                    
                    File.AppendAllText(_csvFilePath, csvLine + Environment.NewLine);
                }

                Console.WriteLine($"Logged IP data for call {callData.CallId}. Total IPs detected: {callData.AllDetectedIps.Count}");
                if (callData.AllDetectedIps.Count > 0)
                {
                    Console.WriteLine($"  Detected IPs: {string.Join(", ", callData.AllDetectedIps)}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error logging call data: {ex.Message}");
            }
            
            return Task.CompletedTask;
        }

        private string BuildCsvLine(CallIpData callData)
        {
            var sb = new StringBuilder();
            
            // Escape and quote CSV values
            sb.Append($"\"{callData.CallTime:yyyy-MM-dd HH:mm:ss}\",");
            sb.Append($"\"{EscapeCsvValue(callData.CallId)}\",");
            sb.Append($"\"{EscapeCsvValue(callData.DestinationDomain)}\",");
            sb.Append($"\"{EscapeCsvValue(callData.ResolvedDestinationIp)}\",");
            sb.Append($"\"{EscapeCsvValue(callData.CallStatus)}\",");
            sb.Append($"\"{EscapeCsvValue(callData.ResponseCode)}\",");
            sb.Append($"\"{string.Join("; ", callData.ContactHeaderIps.Distinct())}\",");
            sb.Append($"\"{string.Join("; ", callData.RecordRouteIps.Distinct())}\",");
            sb.Append($"\"{string.Join("; ", callData.ViaHeaderIps.Distinct())}\",");
            sb.Append($"\"{string.Join("; ", callData.SdpMediaIps.Distinct())}\",");
            sb.Append($"\"{EscapeCsvValue(callData.ServerHeader)}\",");
            sb.Append($"\"{EscapeCsvValue(callData.UserAgent)}\",");
            sb.Append($"\"{string.Join("; ", callData.AllDetectedIps.Distinct())}\"");
            
            return sb.ToString();
        }

        private string EscapeCsvValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("\"", "\"\"");
        }

        public void AddRtpIp(string callId, string rtpIp)
        {
            if (_activeCalls.TryGetValue(callId, out var callData))
            {
                if (!callData.RtpIps.Contains(rtpIp))
                {
                    callData.RtpIps.Add(rtpIp);
                    if (!callData.AllDetectedIps.Contains(rtpIp))
                    {
                        callData.AllDetectedIps.Add(rtpIp);
                    }
                }
            }
            Console.WriteLine($"RTP IP discovered for call {callId}: {rtpIp}");
        }
    }
}
