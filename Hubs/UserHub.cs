using Microsoft.AspNetCore.SignalR;

namespace LargeFileUploader.Hubs;


// handshake - {"protocol":"json","version":1}
public class UserHub : Hub
{
    public override Task OnConnectedAsync()
    {
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        return base.OnDisconnectedAsync(exception);
    }

}