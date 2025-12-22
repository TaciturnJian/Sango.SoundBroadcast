using Sango.SoundBroadcast.Core;

Console.WriteLine("声音广播系统 - TCP音频传输");
Console.WriteLine("===========================");
Console.WriteLine();

if (args.Length > 0)
{
    // 命令行参数模式
    switch (args[0].ToLower())
    {
        case "server":
            TestFunctions.TestSoundServer();
            return;
        case "client":
            TestFunctions.TestSoundClient();
            return;
    }
}

// 交互模式
while (true)
{
    Console.WriteLine("请选择运行模式:");
    Console.WriteLine("1. 启动服务器 (广播音频)");
    Console.WriteLine("2. 启动客户端 (接收音频)");
    Console.WriteLine("3. 同时启动服务器和客户端 (多线程测试)");
    Console.WriteLine("4. 退出");
    Console.WriteLine();
    Console.Write("选择 (1-4): ");

    string input = Console.ReadLine();
    Console.WriteLine();

    switch (input)
    {
        case "1":
            RunServer();
            break;

        case "2":
            RunClient();
            break;

        case "3":
            RunBoth();
            break;

        case "4":
            Console.WriteLine("再见!");
            return;

        default:
            Console.WriteLine("无效选择，请重新输入");
            break;
    }

    Console.WriteLine();
}

static void RunServer()
{
    Console.Clear();
    Console.WriteLine("启动服务器模式...");
    Console.WriteLine("按 Ctrl+C 停止服务器");
    Console.WriteLine();

    TestFunctions.TestSoundServer();
}

static void RunClient()
{
    Console.Clear();
    Console.WriteLine("启动客户端模式...");
    Console.WriteLine();

    TestFunctions.TestSoundClient();
}

static void RunBoth()
{
    Console.Clear();
    Console.WriteLine("同时启动服务器和客户端 (多线程测试)...");
    Console.WriteLine();

    // 创建两个线程分别运行服务器和客户端
    Thread serverThread = new Thread(() =>
    {
        Console.WriteLine("[服务器线程] 启动...");
        TestFunctions.TestSoundServer();
    });

    Thread clientThread = new Thread(() =>
    {
        // 等待2秒让服务器启动
        Thread.Sleep(2000);
        Console.WriteLine("[客户端线程] 启动...");
        TestFunctions.TestSoundClient();
    });

    serverThread.IsBackground = true;
    clientThread.IsBackground = true;

    serverThread.Start();
    clientThread.Start();

    Console.WriteLine("服务器和客户端已启动");
    Console.WriteLine("按任意键停止...");
    Console.ReadKey();

    Console.WriteLine("正在停止所有线程...");
    // 注意：这里需要更优雅的停止机制
    // 实际应用中应该使用CancellationToken
    Environment.Exit(0);
}

static class TestFunctions
{
    /// <summary>
    /// 测试音频服务器（循环播放本地音频文件）
    /// </summary>
    public static void TestSoundServer()
    {
        Console.WriteLine("=== 启动音频服务器测试 ===");

        try
        {
            // 1. 检查音频文件是否存在
            string audioFilePath = Path.Combine(Directory.GetCurrentDirectory(), "music.mp3");

            if (!File.Exists(audioFilePath))
            {
                Console.WriteLine($"错误: 找不到音频文件: {audioFilePath}");
                Console.WriteLine("请将音乐文件重命名为 'music.mp3' 并放置在程序目录下");
                Console.WriteLine("或者指定其他音频文件路径");

                // 创建示例音频文件（仅用于测试）
                Console.WriteLine("正在创建测试音频文件...");
                CreateTestAudioFile(audioFilePath);
            }

            Console.WriteLine($"使用音频文件: {audioFilePath}");

            // 2. 创建音频混合器
            Console.WriteLine("创建音频混合器...");
            var mixer = new SimpleSoundMixer("Server Mixer")
            {
                SampleRate = 44100,
                Channels = 2,
                BitsPerSample = 16
            };

            // 3. 创建文件音频提供者
            Console.WriteLine("创建文件音频提供者...");
            var fileProvider = new FileSoundProvider(audioFilePath)
            {
                Loop = true, // 循环播放
                Volume = 0.8f // 80% 音量
            };

            // 4. 将文件提供者添加到混合器
            Console.WriteLine("将文件提供者添加到混合器...");
            string providerId = mixer.AddProvider(fileProvider, "Background Music", 1.0f);

            // 5. 设置混合器输出格式
            mixer.SetOutputFormat(44100, 2, 16);

            // 6. 创建TCP音频服务器
            Console.WriteLine("创建TCP音频服务器...");
            var server = new SimpleSoundServer(5050)
            {
                Metadata = new AudioMetadata
                {
                    SampleRate = 44100,
                    Channels = 2,
                    BitsPerSample = 16,
                    ProviderName = "Server Audio Mixer"
                }
            };

            // 7. 设置服务器事件处理器
            server.ClientConnected += (sender, e) =>
            {
                Console.WriteLine($"客户端连接: {e.Client.Id} 来自 {e.Client.IpAddress}");
            };

            server.ClientDisconnected += (sender, e) => { Console.WriteLine($"客户端断开: {e.Client.Id} - {e.Reason}"); };

            server.ServerStarted += (sender, e) => { Console.WriteLine("服务器已启动，等待客户端连接..."); };

            // 8. 连接混合器到服务器
            mixer.AudioDataAvailable += (sender, e) => { server.BroadcastAudioData(e.AudioData); };

            // 9. 启动服务器
            Console.WriteLine("启动服务器...");
            server.Start();

            // 10. 启动混合器
            Console.WriteLine("启动音频混合器...");
            mixer.Start();

            // 11. 显示服务器信息
            Console.WriteLine("\n===== 服务器信息 =====");
            Console.WriteLine($"服务器地址: {server.IpAddress}:{server.Port}");
            Console.WriteLine($"音频文件: {Path.GetFileName(audioFilePath)}");
            Console.WriteLine($"格式: {mixer.SampleRate}Hz, {mixer.Channels}声道, {mixer.BitsPerSample}位");
            Console.WriteLine($"循环播放: 是");
            Console.WriteLine("=====================\n");
            Console.WriteLine("服务器正在运行...");
            Console.WriteLine("按 'S' 查看状态");
            Console.WriteLine("按 'G' 调整增益 (+/-)");
            Console.WriteLine("按 'Q' 停止服务器并退出");

            // 12. 处理用户输入
            bool running = true;
            float currentGain = 1.0f;

            while (running)
            {
                if (System.Console.KeyAvailable)
                {
                    var key = System.Console.ReadKey(true).Key;

                    switch (key)
                    {
                        case ConsoleKey.S:
                            ShowServerStatus(server, mixer);
                            break;

                        case ConsoleKey.G:
                            Console.Write("输入新的增益值 (0.0-3.0): ");
                            if (float.TryParse(System.Console.ReadLine(), out float newGain))
                            {
                                newGain = Math.Max(0.0f, Math.Min(3.0f, newGain));
                                mixer.SetProviderGain(providerId, newGain);
                                currentGain = newGain;
                                Console.WriteLine($"增益已设置为: {newGain}");
                            }

                            break;

                        case ConsoleKey.Add:
                        case ConsoleKey.OemPlus:
                            currentGain = Math.Min(3.0f, currentGain + 0.1f);
                            mixer.SetProviderGain(providerId, currentGain);
                            Console.WriteLine($"增益增加至: {currentGain}");
                            break;

                        case ConsoleKey.Subtract:
                        case ConsoleKey.OemMinus:
                            currentGain = Math.Max(0.0f, currentGain - 0.1f);
                            mixer.SetProviderGain(providerId, currentGain);
                            Console.WriteLine($"增益减少至: {currentGain}");
                            break;

                        case ConsoleKey.Q:
                            running = false;
                            break;
                    }
                }

                Thread.Sleep(100);
            }

            // 13. 停止服务器
            Console.WriteLine("\n正在停止服务器...");
            mixer.Stop();
            server.Stop();

            Console.WriteLine("服务器已停止");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"服务器错误: {ex.Message}");
            Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// 显示服务器状态
    /// </summary>
    private static void ShowServerStatus(SimpleSoundServer server, SimpleSoundMixer mixer)
    {
        Console.WriteLine("\n===== 服务器状态 =====");
        Console.WriteLine($"运行状态: {(server.IsRunning ? "运行中" : "已停止")}");
        Console.WriteLine($"客户端数量: {server.ClientCount}");
        Console.WriteLine($"服务器地址: {server.IpAddress}:{server.Port}");

        var stats = mixer.GetStatistics();
        Console.WriteLine($"\n===== 混合器状态 =====");
        Console.WriteLine($"总提供者数: {stats.TotalProviders}");
        Console.WriteLine($"活跃提供者数: {stats.ActiveProviders}");
        Console.WriteLine($"处理总字节数: {stats.TotalBytesProcessed}");
        Console.WriteLine($"平均增益: {stats.AverageGain:F2}");
        Console.WriteLine($"最后混合时间: {stats.LastMixTime}");

        Console.WriteLine($"\n===== 客户端列表 =====");
        var clients = server.GetClients();
        if (clients.Count == 0)
        {
            Console.WriteLine("无客户端连接");
        }
        else
        {
            foreach (var client in clients)
            {
                Console.WriteLine($"ID: {client.Id}");
                Console.WriteLine($"  IP地址: {client.IpAddress}");
                Console.WriteLine($"  连接时间: {client.ConnectedTime}");
                Console.WriteLine($"  最后活动: {client.LastActivity}");
                Console.WriteLine("  ---");
            }
        }

        Console.WriteLine("=====================\n");
    }

    /// <summary>
    /// 创建测试音频文件（正弦波）
    /// </summary>
    private static void CreateTestAudioFile(string filePath)
    {
        try
        {
            // 创建1kHz的正弦波测试音频
            int sampleRate = 44100;
            int channels = 2;
            int bitsPerSample = 16;
            double durationSeconds = 30; // 30秒

            using (var writer = new NAudio.Wave.WaveFileWriter(filePath,
                       new NAudio.Wave.WaveFormat(sampleRate, bitsPerSample, channels)))
            {
                int totalSamples = (int)(sampleRate * durationSeconds);
                double frequency = 1000; // 1kHz

                for (int i = 0; i < totalSamples; i++)
                {
                    double time = (double)i / sampleRate;
                    double sampleValue = 0.3 * Math.Sin(2 * Math.PI * frequency * time);

                    short sample = (short)(sampleValue * 32767);

                    // 写入左右声道
                    writer.WriteSample(sample / 32768f);
                    writer.WriteSample(sample / 32768f);
                }
            }

            Console.WriteLine($"测试音频文件已创建: {filePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"创建测试音频文件失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 测试音频客户端（连接服务器并播放音频）
    /// </summary>
    public static void TestSoundClient()
    {
        Console.WriteLine("=== 启动音频客户端测试 ===");

        try
        {
            // 1. 获取服务器地址
            Console.Write("输入服务器地址 (默认: localhost): ");
            string serverAddress = System.Console.ReadLine();
            if (string.IsNullOrWhiteSpace(serverAddress))
                serverAddress = "localhost";

            // 2. 创建TCP音频客户端
            Console.WriteLine($"正在连接到服务器 {serverAddress}:5050 ...");
            var client = new SimpleSoundClient(serverAddress, 5050);

            // 3. 创建音频播放器
            var audioPlayer = new AudioPlayer(44100, 2, 16);

            // 4. 设置客户端事件处理器
            client.Connected += (sender, e) => { Console.WriteLine("成功连接到服务器"); };

            client.Disconnected += (sender, e) =>
            {
                Console.WriteLine($"与服务器断开连接: {e.Reason}");
                audioPlayer.Stop();
            };

            client.MetadataReceived += (sender, e) =>
            {
                Console.WriteLine("\n===== 接收到音频元数据 =====");
                Console.WriteLine($"提供者: {e.Metadata.ProviderName}");
                Console.WriteLine($"采样率: {e.Metadata.SampleRate}Hz");
                Console.WriteLine($"声道数: {e.Metadata.Channels}");
                Console.WriteLine($"位深度: {e.Metadata.BitsPerSample}位");
                Console.WriteLine($"时间戳: {e.Metadata.Timestamp}");
                Console.WriteLine("===========================\n");
            };

            client.AudioDataReceived += (sender, e) =>
            {
                // 将接收到的音频数据发送到播放器
                audioPlayer.AddAudioData(e.AudioData);

                // 显示接收统计（每秒更新一次）
                if (e.SequenceNumber % 50 == 0) // 大约每秒更新一次
                {
                    Console.SetCursorPosition(0, System.Console.CursorTop);
                    Console.Write($"接收到音频数据: 序列 #{e.SequenceNumber}, 大小: {e.AudioData.Length} 字节, " +
                                  $"缓冲区: {audioPlayer.GetBufferDuration():F2} 秒");
                }
            };

            client.ControlCommandReceived += (sender, e) =>
            {
                Console.WriteLine($"接收到控制命令: {e.Command} 参数: {e.Parameter}");
            };

            // 5. 连接到服务器
            client.Connect();

            // 6. 启动音频播放器
            Console.WriteLine("启动音频播放器...");
            audioPlayer.Start();

            // 7. 显示客户端信息
            Console.WriteLine("\n===== 客户端信息 =====");
            Console.WriteLine($"客户端ID: {client.ClientId}");
            Console.WriteLine($"服务器地址: {client.ServerAddress}:{client.ServerPort}");
            Console.WriteLine($"连接状态: {(client.IsConnected ? "已连接" : "未连接")}");
            Console.WriteLine("=====================\n");

            Console.WriteLine("客户端正在运行...");
            Console.WriteLine("按 'R' 请求元数据");
            Console.WriteLine("按 'C' 发送控制命令");
            Console.WriteLine("按 'S' 显示状态");
            Console.WriteLine("按 'Q' 断开连接并退出");

            // 8. 处理用户输入
            bool running = true;

            while (running && client.IsConnected)
            {
                if (System.Console.KeyAvailable)
                {
                    var key = System.Console.ReadKey(true).Key;

                    switch (key)
                    {
                        case ConsoleKey.R:
                            Console.WriteLine("请求音频元数据...");
                            client.RequestMetadata();
                            break;

                        case ConsoleKey.C:
                            ShowControlCommandMenu(client);
                            break;

                        case ConsoleKey.S:
                            ShowClientStatus(client, audioPlayer);
                            break;

                        case ConsoleKey.Q:
                            running = false;
                            break;
                    }
                }

                System.Threading.Thread.Sleep(100);
            }

            // 9. 断开连接
            Console.WriteLine("\n正在断开连接...");
            audioPlayer.WaitForBufferToEmpty();
            audioPlayer.Stop();
            client.Disconnect();

            Console.WriteLine("客户端已停止");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"客户端错误: {ex.Message}");
            Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// 显示控制命令菜单
    /// </summary>
    private static void ShowControlCommandMenu(SimpleSoundClient client)
    {
        Console.WriteLine("\n===== 控制命令菜单 =====");
        Console.WriteLine("1. 开始传输");
        Console.WriteLine("2. 停止传输");
        Console.WriteLine("3. 暂停传输");
        Console.WriteLine("4. 恢复传输");
        Console.WriteLine("5. 设置音量");
        Console.WriteLine("0. 返回");
        Console.WriteLine("========================");

        Console.Write("选择命令: ");
        string input = System.Console.ReadLine();

        if (int.TryParse(input, out int choice))
        {
            switch (choice)
            {
                case 1:
                    client.SendControlCommand(ControlCommand.Start);
                    Console.WriteLine("已发送开始传输命令");
                    break;

                case 2:
                    client.SendControlCommand(ControlCommand.Stop);
                    Console.WriteLine("已发送停止传输命令");
                    break;

                case 3:
                    client.SendControlCommand(ControlCommand.Pause);
                    Console.WriteLine("已发送暂停传输命令");
                    break;

                case 4:
                    client.SendControlCommand(ControlCommand.Resume);
                    Console.WriteLine("已发送恢复传输命令");
                    break;

                case 5:
                    Console.Write("输入音量 (0.0-1.0): ");
                    if (float.TryParse(System.Console.ReadLine(), out float volume))
                    {
                        volume = Math.Max(0.0f, Math.Min(1.0f, volume));
                        client.SendControlCommand(ControlCommand.SetVolume, volume);
                        Console.WriteLine($"已发送设置音量命令: {volume}");
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// 显示客户端状态
    /// </summary>
    private static void ShowClientStatus(SimpleSoundClient client, AudioPlayer audioPlayer)
    {
        Console.WriteLine("\n===== 客户端状态 =====");
        Console.WriteLine($"连接状态: {(client.IsConnected ? "已连接" : "未连接")}");
        Console.WriteLine($"客户端ID: {client.ClientId}");
        Console.WriteLine($"服务器地址: {client.ServerAddress}:{client.ServerPort}");

        if (client.Metadata != null)
        {
            Console.WriteLine($"\n===== 音频信息 =====");
            Console.WriteLine($"提供者: {client.Metadata.ProviderName}");
            Console.WriteLine($"采样率: {client.Metadata.SampleRate}Hz");
            Console.WriteLine($"声道数: {client.Metadata.Channels}");
            Console.WriteLine($"位深度: {client.Metadata.BitsPerSample}位");
        }

        Console.WriteLine($"\n===== 播放器状态 =====");
        Console.WriteLine($"播放状态: {(audioPlayer.IsPlaying ? "播放中" : "已停止")}");
        Console.WriteLine($"缓冲区时长: {audioPlayer.GetBufferDuration():F2} 秒");
        Console.WriteLine("====================\n");
    }
}