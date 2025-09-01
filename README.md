# SIP Load Tester with IP Logging

A C# .NET application that performs SIP load testing while comprehensively logging all addresses used by external domains during SIP communication. This tool is particularly useful for analyzing network paths, endpoints, and infrastructure used by SIP providers.

### Sample CSV Output:
```csv
CallTime,CallId,DestinationDomain,ResolvedDestinationIp,CallStatus,ResponseCode,ContactHeaderIps,RecordRouteIps,ViaHeaderIps,SdpMediaIps,ServerHeader,UserAgent,AllDetectedIps
"2025-09-01 09:42:45","call_001","target.example.com","192.0.2.149","Success","200 OK","198.51.100.48; 203.0.113.45","192.0.2.88","198.51.100.48; 203.0.113.166","198.51.100.18; 203.0.113.45","Example SIP Proxy","Example SIP Client","192.0.2.88; 198.51.100.48; 198.51.100.18; 203.0.113.166; 203.0.113.45"
```

## Features

- **SIP Registration**: Registers to a SIP server using TLS transport
- **Automated Call Generation**: Makes multiple outbound SIP calls to external domains
- **Comprehensive IP Logging**: Captures and logs all IP addresses from various SIP message sources
- **CSV Export**: Generates detailed CSV reports with all discovered IP addresses
- **Real-time Monitoring**: Displays live statistics during call execution

## Prerequisites

### Required
- **.NET 8.0 SDK** or later
- **Twilio SIP Domain Account** (or compatible SIP provider)
- **Network connectivity** for SIP/TLS communication (port 5061)

### SIP Provider Requirements
You need a SIP provider account with:
- SIP domain endpoint
- Username/password credentials
- TLS support (port 5061)
- Outbound calling permissions

## Setup

### 1. Clone the Repository
```bash
git clone https://github.com/zeeshanalikhan15/SipLoadTester.git
cd SipLoadTester
```

### 2. Configure Settings
Edit the `appsettings.json` file with your SIP provider credentials:

```json
{
  "SipSettings": {
    "SipDomain": "your-domain.sip.provider.com",
    "Username": "your-username",
    "Password": "your-password",
    "ExternalDomain": "target-domain.provider.com",
    "CallCount": 100,
    "CallDelayMs": 5000
  },
  "LogSettings": {
    "LogDirectory": "logs"
  }
}
```

#### Configuration Parameters:
- **SipDomain**: Your SIP provider's domain (e.g., `example-caller.sip.provider.com`)
- **Username**: Your SIP account username
- **Password**: Your SIP account password
- **ExternalDomain**: Target domain to call for testing
- **CallCount**: Number of calls to make (default: 100)
- **CallDelayMs**: Duration in milliseconds to wait during each call before hanging up (default: 5000ms = 5 seconds)
- **LogDirectory**: Directory where logs and CSV files will be saved

### 3. Build the Application
```bash
dotnet build
```

### 4. Run the Application
```bash
dotnet run
```

## How It Works

### Registration Process
1. **TLS Connection**: Establishes secure TLS connection to SIP provider (port 5061)
2. **Authentication**: Registers using provided credentials
3. **Verification**: Confirms successful registration before proceeding

### Call Execution
1. **DNS Resolution**: Resolves target domain to IP address
2. **SIP Call Initiation**: Places outbound call to external domain
3. **Call Duration**: Maintains active call for configured duration (`CallDelayMs`)
4. **Message Capture**: Intercepts all SIP messages (requests/responses)
5. **IP Extraction**: Analyzes messages for IP addresses from multiple sources
6. **Call Termination**: Properly hangs up the call
7. **Data Logging**: Records all discovered information

### IP Address Sources

The application captures IP addresses from:

| Source | Description | Example |
|--------|-------------|---------|
| **DNS Resolution** | IP resolved from destination domain | `192.0.2.149` |
| **Contact Headers** | IPs in SIP Contact headers | `<sip:user@198.51.100.100>` |
| **Via Headers** | IPs in SIP Via routing headers | `Via: SIP/2.0/TLS 203.0.113.1:5061` |
| **Record-Route** | IPs in routing information | `<sip:192.0.2.149:5061;transport=tls>` |
| **SDP Media** | Media endpoint IPs | `c=IN IP4 198.51.100.18` |
| **RTP Endpoints** | Real-time media IPs | `m=audio 13964 RTP/AVP 0` |
| **Server Headers** | Provider identification | `Server: Example SIP Proxy` |
| **Remote Endpoints** | Actual message source IPs | Transport layer IPs |

## Output

### CSV Report
Each run generates a timestamped CSV file in the logs directory:
```
logs/call_ips_YYYYMMDD_HHMMSS.csv
```

#### CSV Columns:
- **CallTime**: Timestamp of the call
- **CallId**: Unique identifier for the call
- **DestinationDomain**: Target domain called
- **ResolvedDestinationIp**: DNS-resolved IP of target
- **CallStatus**: Call result (Success/Error)
- **ResponseCode**: SIP response code (e.g., "200 OK")
- **ContactHeaderIps**: IPs from Contact headers
- **RecordRouteIps**: IPs from routing headers
- **ViaHeaderIps**: IPs from Via headers
- **SdpMediaIps**: IPs from SDP media descriptions
- **ServerHeader**: Server identification string
- **UserAgent**: User agent identification
- **AllDetectedIps**: Complete list of unique IPs found

### Sample CSV Output:
```csv
CallTime,CallId,DestinationDomain,ResolvedDestinationIp,CallStatus,ResponseCode,ContactHeaderIps,RecordRouteIps,ViaHeaderIps,SdpMediaIps,ServerHeader,UserAgent,AllDetectedIps
"2025-09-01 09:42:45","call_001","target.example.com","192.0.2.149","Success","200 OK","198.51.100.48; 203.0.113.45","192.0.2.88","198.51.100.48; 203.0.113.166","198.51.100.18; 203.0.113.45","Example SIP Proxy","Example SIP Client","192.0.2.88; 198.51.100.48; 198.51.100.18; 203.0.113.166; 203.0.113.45"
```

### IP Analysis Tool
> **🔍 For detailed IP analysis and categorization, see [IP_ANALYSIS_README.md](IP_ANALYSIS_README.md)**
> 
> Use the included Ruby script to automatically analyze your CSV files and categorize unique IPs by their SIP/RTP functions.

## Use Cases

### Network Analysis
- **Infrastructure Mapping**: Discover all servers and endpoints in provider's network
- **Load Balancer Detection**: Identify multiple backend servers
- **Routing Analysis**: Understand SIP message routing paths

### Security Assessment
- **IP Range Discovery**: Map provider's IP address ranges
- **Endpoint Enumeration**: Catalog all accessible endpoints
- **Traffic Analysis**: Analyze communication patterns

### Performance Testing
- **Load Testing**: Simulate high call volumes
- **Latency Measurement**: Monitor call setup times
- **Capacity Planning**: Test system limits

## Example Usage

### Basic Load Test
```bash
# Configure for 50 calls with 5-second duration each
# Edit appsettings.json: "CallCount": 50, "CallDelayMs": 5000
dotnet run
```

### High-Volume Testing
```bash
# Configure for 1000 calls with shorter 2-second duration
# Edit appsettings.json: "CallCount": 1000, "CallDelayMs": 2000
dotnet run
```

### Extended Call Duration Testing
```bash
# Configure for longer calls (30 seconds each) to capture more data
# Edit appsettings.json: "CallCount": 10, "CallDelayMs": 30000
dotnet run
```

## Troubleshooting

### Common Issues

#### Registration Failures
- **Check credentials**: Verify username/password in `appsettings.json`
- **Network connectivity**: Ensure port 5061 (TLS) is accessible
- **Domain format**: Use correct SIP domain format

#### No IP Data Captured
- **SIP provider compatibility**: Some providers may not expose detailed headers
- **Network filtering**: Firewalls might strip header information
- **Call success**: Ensure calls are completing successfully

#### Build Errors
```bash
# Clean and rebuild
dotnet clean
dotnet build
```

## Dependencies

- **SIPSorcery**: SIP protocol implementation
- **System.Text.Json**: Configuration file parsing
- **Microsoft.Extensions.Logging**: Logging infrastructure
- **Serilog**: Enhanced logging capabilities

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## License

This project is provided as-is for educational and testing purposes. Please ensure compliance with your SIP provider's terms of service and applicable laws when using this tool.

## Support

For issues or questions:
1. Check the troubleshooting section
2. Review SIP provider documentation
3. Create an issue in the GitHub repository

---

**Note**: This tool generates SIP traffic and should be used responsibly. Ensure you have permission to test against the target domains and comply with your SIP provider's acceptable use policies.
