using System.Security;
using System.Text;

using Microsoft.Win32;

using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace HouseBills.Wpf.Platform;

/// <summary>
/// Windows notifications for an unpackaged app: the app's notification identity (name, icon) and the
/// <c>housebills:</c> link that opens it on click are registered for the current user before each notification, so they
/// always point at the current program file. The installer removes them on uninstall.
/// </summary>
internal sealed class WindowsReminderNotifier : IReminderNotifier
{
    internal const string AppUserModelId = "HouseBills";
    internal const string AppUserModelIdKey = @"Software\Classes\AppUserModelId\" + AppUserModelId;
    internal const string ProtocolKey = @"Software\Classes\" + CommandLine.ProtocolScheme;

    public void Show(string title, IReadOnlyList<string> lines)
    {
        Register();

        var text = new StringBuilder();
        text.Append("<text>").Append(SecurityElement.Escape(title)).Append("</text>");
        foreach (var line in lines)
        {
            text.Append("<text>").Append(SecurityElement.Escape(line)).Append("</text>");
        }

        var xml = new XmlDocument();
        xml.LoadXml(
            $"<toast activationType=\"protocol\" launch=\"{CommandLine.ProtocolScheme}:open\">" +
            $"<visual><binding template=\"ToastGeneric\">{text}</binding></visual></toast>");
        ToastNotificationManager.CreateToastNotifier(AppUserModelId).Show(new ToastNotification(xml) { Tag = "bills" });
    }

    private static void Register()
    {
        var program = Environment.ProcessPath!;
        using (var identity = Registry.CurrentUser.CreateSubKey(AppUserModelIdKey))
        {
            identity.SetValue("DisplayName", "HouseBills");
            identity.SetValue("IconUri", System.IO.Path.Combine(AppContext.BaseDirectory, "HouseBills.ico"));
        }

        using var protocol = Registry.CurrentUser.CreateSubKey(ProtocolKey);
        protocol.SetValue(string.Empty, "URL:HouseBills");
        protocol.SetValue("URL Protocol", string.Empty);
        using var command = protocol.CreateSubKey(@"shell\open\command");
        command.SetValue(string.Empty, $"\"{program}\" \"%1\"");
    }
}