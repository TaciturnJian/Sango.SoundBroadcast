using System.Net;
using System.Net.Sockets;
using Sango.SoundBroadcast.Core;

if (args.Length < 2)
{
    Console.WriteLine("使用方式：\n\t<程序名> <监听地址> <捕获程序名称|!none> [广播客户端列表文件]");
    return;
}

var host_address = args[0];
if (!IPEndPoint.TryParse(host_address, out var host_endpoint))
{
    Console.WriteLine($"在解析主机地址({host_address})时出错");
    return;
}

var app_name = args[1];

using var udp = new UdpClient(host_endpoint);

var mute = args.Length < 3;
if (!mute)
{
    var broadcast_client_file = args[2];
    if (!File.Exists(broadcast_client_file))
    {
        Console.WriteLine($"找不到广播客户端列表文件({broadcast_client_file})");
        return;
    }

    var clients = new List<IPEndPoint>();
    foreach (var line in File.ReadAllLines(broadcast_client_file))
    {
        if (line.StartsWith('#') || line.Trim().Length == 0) continue;
        if (!IPEndPoint.TryParse(line, out var client))
        {
            Console.WriteLine($"在解析客户端地址时发生错误，字符串({line})可能不是正确的地址");
            continue;
        }

        Console.WriteLine($"将目标客户端({client.Serialize()})添加到广播列表");
        clients.Add(client);
    }

    if (clients.Count == 0)
    {
        Console.WriteLine("找不到要广播的客户端");
        return;
    }

    Console.WriteLine("正在执行广播任务");
    AudioUdp.BeginBroadcast(udp, app_name, clients);
}

Console.WriteLine("正在执行接收任务");
AudioUdp.ReceiveTask(udp);
