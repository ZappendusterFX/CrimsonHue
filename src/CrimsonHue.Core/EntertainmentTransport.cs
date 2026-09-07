using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;

namespace CrimsonHue.Core;

public static class EntertainmentPacket
{
    public static byte[] Build(string areaId, byte sequence, IReadOnlyList<ChannelColor> colors)
    {
        if (!Guid.TryParseExact(areaId, "D", out _) || colors.Count is < 1 or > 160 || colors.Select(c => c.Id).Distinct().Count() != colors.Count)
            throw new CrimsonHueException("Invalid Entertainment packet layout.");
        var packet = new byte[52 + colors.Count * 7];
        Encoding.ASCII.GetBytes("HueStream").CopyTo(packet, 0);
        packet[9] = 2; // HueStream v2.0, RGB, reserved fields zero.
        packet[11] = sequence;
        Encoding.ASCII.GetBytes(areaId).CopyTo(packet, 16);
        var offset = 52;
        foreach (var color in colors)
        {
            if (!color.Rgb.IsFinite) throw new CrimsonHueException("Invalid output color.");
            packet[offset++] = color.Id;
            foreach (var value in new[] { color.Rgb.X, color.Rgb.Y, color.Rgb.Z })
            {
                BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(offset, 2), (ushort)Math.Round(Math.Clamp(value, 0, 1) * 65535));
                offset += 2;
            }
        }
        return packet;
    }
}

public interface IEntertainmentTransport : IDisposable
{
    Task ConnectAsync(BridgeCredentials credentials, CancellationToken token);
    void Send(byte[] packet);
}

public sealed class EntertainmentTransport : IEntertainmentTransport
{
    private Socket? socket;
    private DtlsTransport? transport;
    private readonly int port;
    public EntertainmentTransport(int port = 2100) => this.port = port;
    public async Task ConnectAsync(BridgeCredentials credentials, CancellationToken token)
    {
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Connect(new Uri(credentials.Address).Host, port);
        var datagrams = new SocketDatagrams(socket);
        using var registration = token.Register(() => socket.Dispose());
        try
        {
            transport = await Task.Run(() => new DtlsClientProtocol().Connect(new HuePskClient(credentials), datagrams), token);
            token.ThrowIfCancellationRequested();
        }
        catch { Dispose(); throw; }
    }
    public void Send(byte[] packet)
    {
        if (transport == null) throw new InvalidOperationException("DTLS is not connected.");
        if (packet.Length > transport.GetSendLimit()) throw new IOException("Entertainment datagram is too large.");
        transport.Send(packet, 0, packet.Length);
    }
    public void Dispose()
    {
        try { transport?.Close(); } catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException) { }
        socket?.Dispose();
        transport = null;
        socket = null;
    }
    private sealed class HuePskClient(BridgeCredentials credentials) : PskTlsClient(new BcTlsCrypto(new SecureRandom()),
        Encoding.UTF8.GetBytes(credentials.ApplicationKey), Convert.FromHexString(credentials.ClientKey))
    {
        protected override ProtocolVersion[] GetSupportedVersions() => ProtocolVersion.DTLSv12.Only();
        protected override int[] GetSupportedCipherSuites() => [CipherSuite.TLS_PSK_WITH_AES_128_GCM_SHA256];
        public override int GetHandshakeTimeoutMillis() => 6000;
    }
    private sealed class SocketDatagrams(Socket socket) : DatagramTransport
    {
        public int GetReceiveLimit() => 1400;
        public int GetSendLimit() => 1400;
        public int Receive(byte[] buf, int off, int len, int waitMillis)
        {
            if (!socket.Poll(Math.Min(waitMillis, 1000) * 1000, SelectMode.SelectRead)) return -1;
            return socket.Receive(buf, off, len, SocketFlags.None);
        }
        public int Receive(Span<byte> buffer, int waitMillis)
        {
            if (!socket.Poll(Math.Min(waitMillis, 1000) * 1000, SelectMode.SelectRead)) return -1;
            return socket.Receive(buffer);
        }
        public void Send(byte[] buf, int off, int len) => socket.Send(buf, off, len, SocketFlags.None);
        public void Send(ReadOnlySpan<byte> buffer) => socket.Send(buffer);
        public void Close() => socket.Dispose();
    }
}
