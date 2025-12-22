namespace Sango.SoundBroadcast.Core;

/// <summary>
/// 控制命令接收事件参数
/// </summary>
public class ControlCommandReceivedEventArgs : EventArgs
{
    public ControlCommand Command { get; }
    public float Parameter { get; }
        
    public ControlCommandReceivedEventArgs(ControlCommand command, float parameter)
    {
        Command = command;
        Parameter = parameter;
    }
}