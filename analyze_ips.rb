#!/usr/bin/env ruby

require 'csv'

# Function to extract IPs from a string
def extract_ips(ip_string)
  return [] if ip_string.nil? || ip_string.empty?
  ip_string.split(';').map(&:strip).reject(&:empty?)
end

# Function to categorize IP type
def categorize_ip_type(ip)
  case ip
  when /^127\./
    'Localhost/Loopback'
  when /^10\./
    'Private Class A'
  when /^172\.(\d+)\./
    second_octet = $1.to_i
    (16..31).include?(second_octet) ? 'Private Class B' : 'Public'
  when /^192\.168\./
    'Private Class C'
  else
    'Public'
  end
end

# Read CSV file
csv_file = 'logs/call_ips_20250901_120307.csv'
all_ips = Set.new
ip_categories = Hash.new { |h, k| h[k] = Set.new }

puts "Reading CSV file: #{csv_file}"
puts

CSV.foreach(csv_file, headers: true) do |row|
  # Skip rows without successful calls
  next unless row['CallStatus'] == 'Success'
  
  # Extract IPs from different fields
  resolved_dest_ip = row['ResolvedDestinationIp']
  contact_ips = extract_ips(row['ContactHeaderIps'])
  record_route_ips = extract_ips(row['RecordRouteIps'])
  via_header_ips = extract_ips(row['ViaHeaderIps'])
  sdp_media_ips = extract_ips(row['SdpMediaIps'])
  
  # Add resolved destination IP (main domain IPs)
  if resolved_dest_ip && !resolved_dest_ip.empty?
    all_ips.add(resolved_dest_ip)
    ip_categories['Main Domain IPs'].add(resolved_dest_ip)
  end
  
  # Contact Header IPs (usually RTP/media endpoints)
  contact_ips.each do |ip|
    all_ips.add(ip)
    ip_categories['Contact Header IPs (RTP/Media Endpoints)'].add(ip)
  end
  
  # Record Route IPs (SIP proxy servers)
  record_route_ips.each do |ip|
    all_ips.add(ip)
    ip_categories['Record Route IPs (SIP Proxies)'].add(ip)
  end
  
  # Via Header IPs (SIP signaling path)
  via_header_ips.each do |ip|
    all_ips.add(ip)
    ip_categories['Via Header IPs (SIP Signaling Path)'].add(ip)
  end
  
  # SDP Media IPs (RTP media streams)
  sdp_media_ips.each do |ip|
    all_ips.add(ip)
    ip_categories['SDP Media IPs (RTP Streams)'].add(ip)
  end
end

puts '=== SIP LOAD TESTER IP ANALYSIS ==='
puts "Total unique IPs found: #{all_ips.size}"
puts

# Categorize by IP type
ip_type_categories = Hash.new { |h, k| h[k] = Set.new }
all_ips.each do |ip|
  ip_type = categorize_ip_type(ip)
  ip_type_categories[ip_type].add(ip)
end

puts '=== IP CATEGORIZATION BY NETWORK TYPE ==='
ip_type_categories.each do |category, ips|
  puts "\n#{category} (#{ips.size} IPs):"
  ips.sort.each { |ip| puts "  - #{ip}" }
end

puts "\n=== IP CATEGORIZATION BY SIP/RTP FUNCTION ==="
ip_categories.each do |category, ips|
  puts "\n#{category} (#{ips.size} IPs):"
  ips.sort.each { |ip| puts "  - #{ip}" }
end

# Cross-reference analysis
puts "\n=== CROSS-REFERENCE ANALYSIS ==="
puts "IPs that appear in multiple categories:"
ip_usage_count = Hash.new { |h, k| h[k] = [] }
ip_categories.each do |category, ips|
  ips.each { |ip| ip_usage_count[ip] << category }
end

multi_category_ips = ip_usage_count.select { |ip, categories| categories.size > 1 }
multi_category_ips.sort.each do |ip, categories|
  puts "  #{ip}: #{categories.join(', ')}"
end

puts "\n=== SUMMARY BY SIGNALWIRE INFRASTRUCTURE ==="
# Group by likely function based on patterns
signalwire_main = Set.new
signalwire_media = Set.new
private_local = Set.new
external_public = Set.new

all_ips.each do |ip|
  case ip
  when '141.94.209.88', '141.94.210.149'
    signalwire_main.add(ip)
  when /^57\.128\./
    signalwire_media.add(ip)
  when /^172\.19\./, '127.0.0.1'
    private_local.add(ip)
  when /^178\.25\./
    external_public.add(ip)
  else
    external_public.add(ip)
  end
end

puts "\nSignalWire Main Domain IPs (#{signalwire_main.size} IPs):"
signalwire_main.sort.each { |ip| puts "  - #{ip}" }

puts "\nSignalWire Media/RTP IPs (#{signalwire_media.size} IPs):"
signalwire_media.sort.each { |ip| puts "  - #{ip}" }

puts "\nPrivate/Local Network IPs (#{private_local.size} IPs):"
private_local.sort.each { |ip| puts "  - #{ip}" }

puts "\nExternal/Public Infrastructure IPs (#{external_public.size} IPs):"
external_public.sort.each { |ip| puts "  - #{ip}" }

# Additional analysis for SignalWire specific patterns
puts "\n=== DETAILED SIGNALWIRE ANALYSIS ==="
puts "\nMain SIP Server IPs (for dt-vq-test-lineup.dapp.eu-signalwire.com):"
signalwire_main.sort.each do |ip|
  puts "  - #{ip} (Primary SIP endpoint)"
end

puts "\nMedia Server IP Range Analysis:"
media_ip_ranges = signalwire_media.group_by { |ip| ip.split('.')[0..2].join('.') }
media_ip_ranges.each do |range, ips|
  puts "  - #{range}.x (#{ips.size} IPs): #{ips.sort.join(', ')}"
end

puts "\nSIP Proxy/Infrastructure IPs:"
proxy_ips = ip_categories['Record Route IPs (SIP Proxies)'] + ip_categories['Via Header IPs (SIP Signaling Path)']
proxy_ips.uniq.sort.each { |ip| puts "  - #{ip}" }
