using Microsoft.AspNetCore.SignalR;
using ServidorSGR.Models;
using ServidorSGR.Services;
using System.Threading.Tasks;
using System;

namespace ServidorSGR.Hubs
{
    public class DataHub : Hub
    {
        private readonly IConnectionMapping _connectionMapping;

        public DataHub(IConnectionMapping connectionMapping)
        {
            _connectionMapping = connectionMapping;
        }

        public async Task RegisterAlias(RegisterAliasRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Alias))
            {
                await Clients.Caller.SendAsync("AliasRegistrationFailed", "Alias cannot be null or empty.");
                return;
            }

            if (_connectionMapping.GetConnectionId(request.Alias) != null)
            {
                string? currentAlias = _connectionMapping.GetAliasByConnectionId(Context.ConnectionId);
                if (currentAlias != request.Alias)
                {
                    await Clients.Caller.SendAsync("AliasRegistrationFailed", $"Alias '{request.Alias}' is already taken.");
                    return;
                }
            }

            _connectionMapping.Add(request.Alias, Context.ConnectionId);

            await Clients.Caller.SendAsync("AliasRegistered", request.Alias);
        }

        public async Task SendData(SendDataRequest request)
        {
            if (request?.BatchContainer == null || string.IsNullOrWhiteSpace(request.RecipientAlias) || request.BatchContainer.Batches.Count == 0)
            {
                await Clients.Caller.SendAsync("SendMessageFailed", "Invalid send request. Recipient alias and batch container with batches are required.");
                return;
            }

            string? recipientConnectionId = _connectionMapping.GetConnectionId(request.RecipientAlias);

            if (string.IsNullOrEmpty(recipientConnectionId))
            {
                await Clients.Caller.SendAsync("SendMessageFailed", $"Recipient alias '{request.RecipientAlias}' not found or not connected.");
                return;
            }

            try
            {
                await Clients.Client(recipientConnectionId).SendAsync("ReceiveDataBatch", request.BatchContainer);
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("SendMessageFailed", $"Error sending message to '{request.RecipientAlias}': {ex.Message}");
            }
        }

        public override Task OnConnectedAsync()
        {
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            string? alias = _connectionMapping.GetAliasByConnectionId(Context.ConnectionId);
            if (alias != null)
            {
                _connectionMapping.RemoveByConnectionId(Context.ConnectionId);
            }

            return base.OnDisconnectedAsync(exception);
        }
    }
}