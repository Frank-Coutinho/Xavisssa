using System;
using System.Reactive;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using ReactiveUI;
using Xavissa.Frontend.Models;
using Xavissa.Frontend.Services;

namespace Xavissa.Frontend.ViewModels;

public class LicenseActivationViewModel : ViewModelBase
{
    private readonly ILicenseStateService _licenseState;
    private readonly INotificationService _notify;
    private readonly LicensingOptions _options;

    private string _licenseKey = string.Empty;
    public string LicenseKey
    {
        get => _licenseKey;
        set => this.RaiseAndSetIfChanged(ref _licenseKey, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set => this.RaiseAndSetIfChanged(ref _isBusy, value);
    }

    private string _message = "Activate this device or start a demo.";
    public string Message
    {
        get => _message;
        private set => this.RaiseAndSetIfChanged(ref _message, value);
    }

    public bool DemoEnabled => _options.DemoEnabled;
    public bool HasVideo => !string.IsNullOrWhiteSpace(_options.DemoVideoUrl);
    public ReactiveCommand<Unit, Unit> WatchVideoCommand { get; }
    public ReactiveCommand<Unit, Unit> ActivateCommand { get; }
    public ReactiveCommand<Unit, Unit> TryDemoCommand { get; }
    public Interaction<Unit, Unit> Activated { get; } = new();
    public Interaction<LicenseStateResult, Unit> Blocked { get; } = new();

    public LicenseActivationViewModel(
        ILicenseStateService licenseState,
        INotificationService notify,
        IOptions<LicensingOptions> options)
    {
        _licenseState = licenseState;
        _notify = notify;
        _options = options.Value;

        ActivateCommand = ReactiveCommand.CreateFromTask(ActivateAsync);
        TryDemoCommand = ReactiveCommand.CreateFromTask(StartDemoAsync);
        WatchVideoCommand = ReactiveCommand.Create(() =>
        {
            if (!string.IsNullOrWhiteSpace(_options.DemoVideoUrl))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_options.DemoVideoUrl) { UseShellExecute = true });
        });
    }

    private async Task ActivateAsync()
    {
        if (string.IsNullOrWhiteSpace(LicenseKey))
        {
            Message = "Enter a license key first.";
            _notify.Show(Message, NotificationType.Warning);
            return;
        }

        IsBusy = true;
        Message = "Activating this device...";
        try
        {
            var result = await _licenseState.ActivateAsync(LicenseKey.Trim());
            LicenseKey = string.Empty;
            await HandleResultAsync(result);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task StartDemoAsync()
    {
        IsBusy = true;
        Message = "Starting demo...";
        try
        {
            var result = await _licenseState.StartDemoAsync();
            await HandleResultAsync(result);
        }
        catch (Exception ex)
        {
            Message = "Demo mode could not be started.";
            _notify.Show(Message + " " + ex.Message, NotificationType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task HandleResultAsync(LicenseStateResult result)
    {
        Message = result.Message;
        if (result.CanOpenWorkspace)
        {
            _notify.Show(result.Message, NotificationType.Success);
            await Activated.Handle(Unit.Default).ToTask();
            return;
        }

        _notify.Show(result.Message, NotificationType.Error);
        if (result.ShouldShowBlocked)
            await Blocked.Handle(result).ToTask();
    }
}
