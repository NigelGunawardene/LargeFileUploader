namespace LargeFileUploader.Events.Args;

public class MyEventArgs : EventArgs
{
    public string eventContent { get; set; }

    public MyEventArgs(string text)
    {
        this.eventContent = text;
    }
}