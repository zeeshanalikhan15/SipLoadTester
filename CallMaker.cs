using System;
using System.Threading.Tasks;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SIPSorcery.Media;
using SipLoadTester;
using SIPSorcery.Net;
using System.Net.Sockets;
using System.Collections.Generic;

namespace SipLoadTester
{
    public class CallMaker
    {
        private readonly SIPTransport _sipTransport;
        private readonly string _username;
        private readonly string _password;
        private readonly string _sipDomain;
        private readonly string _externalDomain;

        public CallMaker(SIPTransport sipTransport, string username, string password, string sipDomain, string externalDomain)
        {
            _sipTransport = sipTransport;
            _username = username;
            _password = password;
            _sipDomain = sipDomain;
            _externalDomain = externalDomain;
        }

        public async Task MakeCall()
        {
            try
            {
                var userAgent = new SIPUserAgent(_sipTransport, null);

                var callDescriptor = new SIPCallDescriptor(
                    _username, // username
                    _password, // password
                    $"sip:{_externalDomain}", // destination URI
                    $"sip:{_username}@{_sipDomain}", // from
                    null, // to
                    null, // routeSet
                    null, // customHeaders
                    null, // authUsername
                    SIPCallDirection.Out, // callDirection
                    null, // contentType
                    null, // content
                    null // mangleIPAddress
                );

                // Create a basic RTP session for the call
                var mediaSession = new RTPSession(false, false, false);
                
                // Add audio capabilities
                List<SDPAudioVideoMediaFormat> audioFormats = new List<SDPAudioVideoMediaFormat>
                {
                    new SDPAudioVideoMediaFormat(SDPMediaTypesEnum.audio, 0, "PCMU", 8000),
                    new SDPAudioVideoMediaFormat(SDPMediaTypesEnum.audio, 8, "PCMA", 8000)
                };
                
                var audioTrack = new MediaStreamTrack(SDPMediaTypesEnum.audio, false, audioFormats);
                mediaSession.addTrack(audioTrack);

                bool callResult = await userAgent.Call(callDescriptor, mediaSession);
                if (callResult)
                {
                    Console.WriteLine($"Call to {_externalDomain} initiated successfully.");
                    
                    // Wait a bit before hanging up (simulate call duration)
                    await Task.Delay(2000);
                    
                    // Properly end the call
                    userAgent.Hangup();
                }
                else
                {
                    Console.WriteLine($"Call to {_externalDomain} failed to initiate.");
                }
                
                // Wait a moment for cleanup
                await Task.Delay(500);
                
                // Clean up
                userAgent.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception during call to {_externalDomain}: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}
