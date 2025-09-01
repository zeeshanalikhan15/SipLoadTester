# SIP Load Tester - IP Analysis Tool

This directory contains a Ruby script (`analyze_ips.rb`) that analyzes CSV files generated from SIP load tests to extract and categorize unique IP addresses based on their SIP/RTP functions.

## Overview

The SIP Load Tester generates detailed CSV logs during load testing that contain various IP addresses discovered during SIP call establishment and media negotiation. This analysis tool helps you understand the network infrastructure involved in your SIP communications.

## What the Script Analyzes

The script examines the following IP address sources from your CSV log:

- **ResolvedDestinationIp** - Main domain/server IPs
- **ContactHeaderIps** - RTP/Media endpoint IPs  
- **RecordRouteIps** - SIP proxy server IPs
- **ViaHeaderIps** - SIP signaling path IPs
- **SdpMediaIps** - RTP media stream IPs

## Prerequisites

- **Ruby 3.4.5** (managed with mise - see `.tool-versions` file)
- CSV file generated from SIP Load Tester (with successful call records)

### Ruby Installation with mise

This project uses [mise](https://mise.jdx.dev/) for Ruby version management. The required Ruby version is specified in `.tool-versions`.

**Install mise:**
```bash
curl https://mise.run | sh
```

**Install the required Ruby version:**
```bash
mise install
```

**Verify installation:**
```bash
ruby --version
```

## Usage

1. **Run your SIP Load Test**
   ```bash
   dotnet run
   ```
   This will generate a CSV file in the `logs/` directory with a timestamp (e.g., `call_ips_YYYYMMDD_HHMMSS.csv`)

2. **Update the script with your CSV file path** (if different)
   Edit `analyze_ips.rb` and modify this line:
   ```ruby
   csv_file = 'logs/your_csv_filename.csv'
   ```

3. **Run the IP analysis**
   ```bash
   ruby analyze_ips.rb
   ```

## Output Categories

### Network Type Classification
- **Public IPs** - Internet-routable addresses
- **Private Class A/B/C** - RFC 1918 private network ranges
- **Localhost/Loopback** - 127.0.0.1 addresses

### SIP/RTP Function Classification
- **Main Domain IPs** - Primary SIP server endpoints
- **Contact Header IPs** - RTP/Media endpoint addresses
- **Record Route IPs** - SIP proxy servers in the call path
- **Via Header IPs** - SIP signaling path components
- **SDP Media IPs** - RTP media stream endpoints

### Infrastructure Analysis
- **Main SIP Server IPs** - Primary SIP servers
- **Media/RTP Server IPs** - Dedicated media servers
- **Private/Local Network IPs** - Internal infrastructure
- **External/Public Infrastructure IPs** - External components

## Sample Output

```
=== SIP LOAD TESTER IP ANALYSIS ===
Total unique IPs found: 7

=== IP CATEGORIZATION BY NETWORK TYPE ===

Public (5 IPs):
  - XXX.XXX.XXX.XXX
  - XXX.XXX.XXX.XXX
  - XXX.XXX.XXX.XXX
  - XXX.XXX.XXX.XXX
  - XXX.XXX.XXX.XXX

=== IP CATEGORIZATION BY SIP/RTP FUNCTION ===

Main Domain IPs (2 IPs):
  - XXX.XXX.XXX.XXX
  - XXX.XXX.XXX.XXX

Contact Header IPs (RTP/Media Endpoints) (3 IPs):
  - 172.19.92.XXX
  - XXX.XXX.XXX.XXX
  - XXX.XXX.XXX.XXX
```

## Cross-Reference Analysis

The script also provides:
- **Multi-category IPs** - IPs that serve multiple functions
- **Infrastructure mapping** - How IPs relate to specific SIP/RTP roles
- **Media server range analysis** - Grouping of media server IPs by subnet

## Use Cases

- **Network troubleshooting** - Identify which servers handle signaling vs media
- **Infrastructure mapping** - Understand your SIP provider's architecture
- **Security analysis** - Catalog all IPs involved in your communications
- **Load balancing verification** - See distribution across multiple servers
- **Firewall configuration** - Generate IP lists for network access rules

## Customization

You can modify the script to:
- Add custom IP categorization rules
- Export results to different formats (JSON, XML, etc.)
- Filter by specific IP ranges or patterns
- Add geographic IP location analysis
- Integrate with network monitoring tools

## CSV File Format

The script expects CSV files with these headers:
- `CallTime`, `CallId`, `DestinationDomain`, `ResolvedDestinationIp`
- `CallStatus`, `ResponseCode`, `ContactHeaderIps`, `RecordRouteIps`
- `ViaHeaderIps`, `SdpMediaIps`, `ServerHeader`, `UserAgent`, `AllDetectedIps`

Only rows with `CallStatus` = "Success" are analyzed to ensure accurate IP mapping.

## Notes

- The script automatically handles multiple IPs in semicolon-separated fields
- Empty or null IP fields are safely ignored
- IP addresses are deduplicated across all categories
- Results are sorted alphabetically for easy reading

For questions or issues with the IP analysis tool, please refer to the main SIP Load Tester documentation or create an issue in the repository.
  
  
> **📋 For main application documentation, see [README.md](README.md)**