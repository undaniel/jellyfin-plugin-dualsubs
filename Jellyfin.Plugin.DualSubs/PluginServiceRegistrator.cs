using Jellyfin.Plugin.DualSubs.EventHandlers;
using Jellyfin.Plugin.DualSubs.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.DualSubs;

/// <summary>
/// Registers plugin services.
/// </summary>
public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<SubtitleTextProvider>();
        serviceCollection.AddSingleton<DualSubtitleService>();
        serviceCollection.AddHostedService<ItemAddedHandler>();
    }
}
