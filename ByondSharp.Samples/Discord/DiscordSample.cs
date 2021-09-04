using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using ByondSharp.FFI;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace ByondSharp.Samples.Discord
{
    public class DiscordSample
    {
        private static DiscordSocketClient _client;

        [ByondFFI]
        public static async Task InitializeDiscord()
        {
            // Do not re-initialize
            if (_client != null)
                return;

            _client = new DiscordSocketClient();
            await _client.LoginAsync(TokenType.Bot, "your-token-here");
            await _client.StartAsync();

            var services = new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton<CommandService>()
                .BuildServiceProvider();

            var commandService = services.GetRequiredService<CommandService>();
            var commandHandler = new CommandHandler(services, commandService, _client);
            await commandHandler.InitializeAsync();
        }

        [ByondFFI]
        public static string GetDiscordMessage()
        {
            return RepeaterModule.IncomingMessages.TryDequeue(out var result) ? result : null;
        }
    }

    public class CommandHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly IServiceProvider _services;

        public CommandHandler(IServiceProvider services, CommandService commands, DiscordSocketClient client)
        {
            _commands = commands;
            _services = services;
            _client = client;
        }

        public async Task InitializeAsync()
        {
            // Pass the service provider to the second parameter of
            // AddModulesAsync to inject dependencies to all modules 
            // that may require them.
            await _commands.AddModuleAsync<RepeaterModule>(_services);
            _client.MessageReceived += HandleCommandAsync;
        }

        public async Task HandleCommandAsync(SocketMessage messageParam)
        {
            // Don't process the command if it was a system message
            if (!(messageParam is SocketUserMessage message)) return;

            // Create a number to track where the prefix ends and the command begins
            int argPos = 0;

            // Determine if the message is a command based on the prefix and make sure no bots trigger commands
            if (!(message.HasCharPrefix('!', ref argPos) || 
                  message.HasMentionPrefix(_client.CurrentUser, ref argPos)) ||
                message.Author.IsBot)
                return;

            // Create a WebSocket-based command context based on the message
            var context = new SocketCommandContext(_client, message);

            // Execute the command with the command context we just
            // created, along with the service provider for precondition checks.
            await _commands.ExecuteAsync(
                context: context, 
                argPos: argPos,
                services: _services);
        }
    }

    public class RepeaterModule : ModuleBase<SocketCommandContext>
    {
        public static ConcurrentQueue<string> IncomingMessages = new ConcurrentQueue<string>();

        [Command("repeat")]
        public async Task RepeatMessage([Remainder] string message)
        {
            IncomingMessages.Enqueue($"{Context.User.Username}#{Context.User.DiscriminatorValue:0000}: {message}");
            await ReplyAsync("Sent message!");
        }
    }
}