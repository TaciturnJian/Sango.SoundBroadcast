using System.Net;
using System.Net.Sockets;
using NAudio.Wave;

using Sango.SoundBroadcast.Core;

if (args.Length < 2)
{
    Console.WriteLine("用法：\n\t<程序名> <用户名> <监听的IP地址:端口>");
    return;
}

var user_name = args[0];
var listen_ep_str = args[1];

if (!IPEndPoint.TryParse(listen_ep_str, out var host_ep))
{
    Console.WriteLine("无法解析要监听的地址或端口");
    return;
}

Console.WriteLine($"正在尝试作为用户({user_name})运行在({listen_ep_str})上的服务");

var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);

try
{
    socket.Bind(host_ep);
}
catch (Exception ex)
{
    Console.WriteLine($"绑定到目标端口失败：{ex}");
    return;
}


const string FILE_PATH = "./music.mp3";
var format = new WaveFormat();

using var cts = new CancellationTokenSource();
using var server = new SoundServer(format, cts);
server.Name = user_name;

var running = true;
server.Player.Player.OutEvent.PlaybackStopped += (_, _) => running = false;

var remote_end_points = new List<EndPoint>();
if (!ThreadPool.QueueUserWorkItem(ReceiveTask))
{
    Console.WriteLine("无法运行数据接收服务");
    return;
}

if (!ThreadPool.QueueUserWorkItem(SendTask))
{
    Console.WriteLine("无法运行数据发送服务");
    return;
}
    
while (running)
{
    Console.Write(">> ");
    var line = Console.ReadLine();
    if (line is null || line.Length == 0 || line.StartsWith('#')) continue;

    var first_space_index = line.IndexOf(' ');
    var cmd = (first_space_index < 0 ? line[0..first_space_index] : line).ToLower();
    var content = (first_space_index < 0 ? "" : line[first_space_index..]).Trim();
    switch (cmd)
    {
        case "add":
            AddRemoteEndpoint(content);
            break;

        case "speak":
            EnableMicrophone(content);
            break;

        case "mute":
            MuteMicrophone(content);
            break;
    }
}


return;

void AddRemoteEndpoint(string content)
{
    if (IPEndPoint.TryParse(content, out var ep))
    {
        remote_end_points.Add(ep);
    }
    else
    {
        Console.WriteLine($"远程主机({content})添加失败");
    }
}

void EnableMicrophone(string content)
{

}

void MuteMicrophone(string content)
{

}

void ReceiveTask(object? state)
{
    var buffer = new byte[1024];
    while (!cts.IsCancellationRequested)
    {
        EndPoint ep = new IPEndPoint(IPAddress.Any, 0);
        var read_bytes = socket.ReceiveFrom(buffer, ref ep);
        var data = new byte[read_bytes];
        Array.Copy(buffer, data, read_bytes);
        ThreadPool.QueueUserWorkItem(_ =>
        {
            server.HandleClientMessage(socket, ep, data);
        });
        Thread.Sleep(TimeSpan.FromMilliseconds(1));
    }
}

void SendTask(object? state)
{
    while (!cts.IsCancellationRequested)
    {
        foreach (var ep in remote_end_points)
        {
            server.SendHeartbeatMessage(socket, ep);
        }
        Thread.Sleep(TimeSpan.FromSeconds(5));
    }
}
