using Avalonia.Controls;
using Xavissa.Frontend.ViewModels;

namespace Xavissa.Frontend.Views
{
    public partial class HomeView : UserControl
    {
        public HomeView()
        {
            InitializeComponent();

            // Set up the ViewModel
            if (Design.IsDesignMode)
            {
                // Use sample data in design mode (optional)
                DataContext = new HomeViewModel();
            }
            else
            {
                DataContext = new HomeViewModel();
            }
        }

        // Optional: Allow injection from LoginView navigation
        public HomeView(HomeViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
