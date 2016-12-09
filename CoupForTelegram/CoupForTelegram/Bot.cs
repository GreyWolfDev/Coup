using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using Telegram.Bot;
using Telegram.Bot.Args;

namespace CoupForTelegram
{
    internal static class Bot
    {
        internal static TelegramBotClient Api;
        internal static void Initialize()
        {
            var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                        .OpenSubKey("SOFTWARE\\Coup");
#if DEBUG
            var TelegramAPIKey = key.GetValue("DebugAPI").ToString();
#elif RELEASE
            var TelegramAPIKey = key.GetValue("ProductionAPI").ToString();
#endif
            Api = new TelegramBotClient(TelegramAPIKey);
            Api.OnMessage += ApiOnOnMessage;
            Api.StartReceiving();
        }

        private static void ApiOnOnMessage(object sender, MessageEventArgs messageEventArgs)
        {
            Api.SendTextMessageAsync(messageEventArgs.Message.Chat.Id, "I'm alive!!");
        }
    }
}
