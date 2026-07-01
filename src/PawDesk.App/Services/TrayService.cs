using System;
using System.Drawing;
using System.Windows.Forms;

namespace PawDesk.App.Services;

public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;

    public TrayService(
        Action showPet,
        Action hidePet,
        Action changeImage,
        Action openSettings,
        Func<bool> getStartupEnabled,
        Action<bool> setStartupEnabled,
        Action exit)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("显示宠物", null, (_, _) => showPet());
        menu.Items.Add("隐藏宠物", null, (_, _) => hidePet());
        menu.Items.Add("更换图片", null, (_, _) => changeImage());
        menu.Items.Add("设置", null, (_, _) => openSettings());
        menu.Items.Add(new ToolStripSeparator());
        var startupItem = new ToolStripMenuItem("开机启动")
        {
            CheckOnClick = true,
            Checked = getStartupEnabled()
        };
        startupItem.CheckedChanged += (_, _) => setStartupEnabled(startupItem.Checked);
        menu.Items.Add(startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => exit());

        _notifyIcon = new NotifyIcon
        {
            Text = "PawDesk",
            Icon = SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true
        };

        _notifyIcon.DoubleClick += (_, _) => showPet();
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
