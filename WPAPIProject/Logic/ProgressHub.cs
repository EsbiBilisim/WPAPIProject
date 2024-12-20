using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace WPAPIProject.Logic;

public class ProgressHub : Hub
{
    public async Task SendProgress(string message, int percentage)
    {
        await Clients.All.SendAsync("ReceiveProgress", message, percentage);
    }
}