using System.Reactive;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using ReactiveUI;
using Xavissa.Frontend.Models;
using Xavissa.Frontend.Services;

namespace Xavissa.Frontend.ViewModels;

public class LicenseBlockedViewModel : ViewModelBase
{
    private readonly ILicenseStateService _licenseState;

    private string _title = "License required";
    public string Title
    {
        get => _title;
        private set => this.RaiseAndSetIfChanged(ref _title, value);
    }

    private string _message = "This workspace is blocked until the license is revalidated.";
    public string Message
    {
        get => _message;
        private set => this.RaiseAndSetIfChanged(ref _message, value);
    }

    public bool CanEnterLicenseKey { get; private set; }
    public ReactiveCommand<Unit, Unit> RetryCommand { get; }
    public ReactiveCommand<Unit, Unit> EnterLicenseCommand { get; }
    public Interaction<Unit, Unit> Unblocked { get; } = new();
    public Interaction<Unit, Unit> ShowActivation { get; } = new();

    public LicenseBlockedViewModel(ILicenseStateService licenseState)
    {
        _licenseState = licenseState;
        RetryCommand = ReactiveCommand.CreateFromTask(RetryAsync);
        EnterLicenseCommand = ReactiveCommand.CreateFromTask(async () => await ShowActivation.Handle(Unit.Default).ToTask());
    }

    public void Apply(LicenseStateResult result, bool canEnterLicenseKey)
    {
        CanEnterLicenseKey = canEnterLicenseKey;
        this.RaisePropertyChanged(nameof(CanEnterLicenseKey));

        Title = result.Status switch
        {
            LicenseAccessStatus.DemoExpired => "Demo expired",
            LicenseAccessStatus.TrialExpired => "Trial expired",
            LicenseAccessStatus.Suspended => "License suspended",
            LicenseAccessStatus.DeviceDeactivated => "Device deactivated",
            LicenseAccessStatus.DeviceNotActivated => "Device activation required",
            LicenseAccessStatus.OfflineLimitExceeded => "Online revalidation required",
            _ => "License blocked",
        };
        Message = string.IsNullOrWhiteSpace(result.Message)
            ? "Contact support to restore access."
            : result.Message;
    }

    private async Task RetryAsync()
    {
        var result = await _licenseState.EvaluateAsync(preferOnlineValidation: true, validationType: "StartupValidation");
        Apply(result, CanEnterLicenseKey);
        if (result.CanOpenWorkspace)
            await Unblocked.Handle(Unit.Default).ToTask();
    }
}
