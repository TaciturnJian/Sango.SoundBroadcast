using System.Net;
using System.Net.Sockets;

namespace Sango.SoundBroadcast.Core;

public class ClientInfo(EndPoint remote, string name, DateTime time)
{
    public const int DefaultAttenuation = 10;

    public int Attenuation { get; set; } = DefaultAttenuation;

    public EndPoint Remote { get; set; } = remote;

    public string Name { get; set; } = name;

    public DateTime LastHeartbeat { get; set; } = time;
}