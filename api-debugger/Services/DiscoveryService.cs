using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MusicBeeRemote.ApiDebugger.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MusicBeeRemote.ApiDebugger.Services;

public class DiscoveryService : IDisposable
{
    private const int DiscoveryPort = 45345;
    private static readonly IPAddress MulticastAddress = IPAddress.Parse("239.1.5.10");
    private bool _disposed;

    public async Task<List<ConnectionInfo>> DiscoverAsync(int timeoutMs = 3000)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var discovered = new List<ConnectionInfo>();
        var localAddresses = GetLocalIpAddresses();

        using var cts = new CancellationTokenSource(timeoutMs);

        // Try discovery from each local interface
        var tasks = localAddresses.Select(localAddress =>
            DiscoverFromInterfaceAsync(localAddress, discovered, cts.Token)).ToList();

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            // Expected when timeout occurs
        }

        return discovered;
    }

    private static async Task DiscoverFromInterfaceAsync(
        IPAddress localAddress,
        List<ConnectionInfo> discovered,
        CancellationToken cancellationToken)
    {
        UdpClient? udpClient = null;

        try
        {
            udpClient = new UdpClient(AddressFamily.InterNetwork);
            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udpClient.Client.Bind(new IPEndPoint(localAddress, 0));
            udpClient.JoinMulticastGroup(MulticastAddress, localAddress);

            // Send discovery request
            var discoveryMessage = new
            {
                context = "discovery",
                address = localAddress.ToString()
            };

            var messageBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(discoveryMessage));
            var multicastEndpoint = new IPEndPoint(MulticastAddress, DiscoveryPort);

            await udpClient.SendAsync(messageBytes, messageBytes.Length, multicastEndpoint);

            // Listen for responses
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var receiveTask = udpClient.ReceiveAsync(cancellationToken);
                    var result = await receiveTask;
                    var response = Encoding.UTF8.GetString(result.Buffer);

                    ProcessDiscoveryResponse(response, discovered);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (SocketException)
                {
                    // Socket error, continue
                }
            }
        }
        catch (SocketException)
        {
            // Failed to bind to this interface, skip
        }
        catch (OperationCanceledException)
        {
            // Timeout, expected
        }
        finally
        {
            if (udpClient != null)
            {
                try
                {
                    udpClient.DropMulticastGroup(MulticastAddress);
                }
                catch
                {
                    // Ignore errors when dropping multicast group
                }

                udpClient.Close();
                udpClient.Dispose();
            }
        }
    }

    private static void ProcessDiscoveryResponse(
        string response,
        List<ConnectionInfo> discovered)
    {
        try
        {
            var json = JObject.Parse(response);
            var context = json["context"]?.ToString();

            if (context == "notify")
            {
                var address = json["address"]?.ToString();
                var name = json["name"]?.ToString();
                var port = json["port"]?.Value<int>() ?? 3000;

                if (!string.IsNullOrEmpty(address))
                {
                    // Check if already discovered
                    lock (discovered)
                    {
                        if (!discovered.Any(d => d.IpAddress == address && d.Port == port))
                        {
                            discovered.Add(new ConnectionInfo
                            {
                                IpAddress = address,
                                Port = port,
                                Name = name ?? "MusicBee Remote",
                                DiscoveredAt = DateTime.Now
                            });
                        }
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Invalid JSON response, ignore
        }
    }

    private static List<IPAddress> GetLocalIpAddresses()
    {
        var addresses = new List<IPAddress>();

        try
        {
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up)
                    continue;

                if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                var properties = networkInterface.GetIPProperties();

                foreach (var unicast in properties.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        addresses.Add(unicast.Address);
                    }
                }
            }
        }
        catch
        {
            // Fallback: try to get any local address
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                addresses.AddRange(host.AddressList.Where(ip =>
                    ip.AddressFamily == AddressFamily.InterNetwork));
            }
            catch
            {
                // Last resort
                addresses.Add(IPAddress.Any);
            }
        }

        return addresses;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
