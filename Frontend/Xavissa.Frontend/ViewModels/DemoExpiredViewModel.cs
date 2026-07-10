using System;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using ReactiveUI;
using Xavissa.Frontend.Models;
using Xavissa.Frontend.Services;

namespace Xavissa.Frontend.ViewModels;

public class DemoExpiredViewModel : ViewModelBase
{
    private readonly ILicenseStateService _licenseState;
    private readonly IDemoStateService _demoState;
    private readonly INotificationService _notify;
    private readonly LicensingOptions _options;

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set => this.RaiseAndSetIfChanged(ref _isBusy, value);
    }

    public string ContactText => string.IsNullOrWhiteSpace(_options.DemoContactWhatsApp)
        ? "Contact Sales / WhatsApp"
        : "Contact Sales / WhatsApp";

    public ReactiveCommand<Unit, Unit> RestartDemoCommand { get; }
    public ReactiveCommand<Unit, Unit> ActivateLicenseCommand { get; }
    public ReactiveCommand<Unit, Unit> ContactSalesCommand { get; }
    public Interaction<Unit, Unit> Restarted { get; } = new();
    public Interaction<Unit, Unit> ShowActivation { get; } = new();

    public DemoExpiredViewModel(
        ILicenseStateService licenseState,
        IDemoStateService demoState,
        INotificationService notify,
        IOptions<LicensingOptions> options)
    {
        _licenseState = licenseState;
        _demoState = demoState;
        _notify = notify;
        _options = options.Value;

        RestartDemoCommand = ReactiveCommand.CreateFromTask(RestartDemoAsync);
        ActivateLicenseCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            await _demoState.TrackEventAsync("LicenseActivationClicked", description: "Activation clicked from expired demo.");
            await ShowActivation.Handle(Unit.Default).ToTask();
        });
        ContactSalesCommand = ReactiveCommand.CreateFromTask(ContactSalesAsync);
    }

    private async Task RestartDemoAsync()
    {
        IsBusy = true;
        try
        {
            await _demoState.TrackEventAsync("DemoRestarted", description: "Restart demo requested.");
            var result = await _licenseState.StartDemoAsync();
            if (!result.CanOpenWorkspace)
            {
                _notify.Show(result.Message, NotificationType.Error);
                return;
            }

            await Restarted.Handle(Unit.Default).ToTask();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ContactSalesAsync()
    {
        await _demoState.TrackEventAsync("ContactSalesClicked", description: "Contact sales clicked.");
        if (string.IsNullOrWhiteSpace(_options.DemoContactWhatsApp))
        {
            _notify.Show("Contact sales is not configured yet.", NotificationType.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(_options.DemoContactWhatsApp) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _notify.Show("Could not open contact link: " + ex.Message, NotificationType.Warning);
        }
    }
}
