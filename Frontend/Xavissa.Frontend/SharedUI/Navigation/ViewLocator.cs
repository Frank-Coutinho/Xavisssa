using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Xavissa.Frontend
{
    public class ViewLocator : IDataTemplate
    {
        public ViewLocator()
        {
        }

        public Control Build(object data)
        {
            if (data == null)
                return new TextBlock { Text = "ViewModel missing" };

            var vmType = data.GetType();
            var viewName = vmType
                .FullName!.Replace(".ViewModels.", ".Views.")
                .Replace("ViewModel", "View");

            var assembly = vmType.Assembly;
            var viewType = assembly.GetType(viewName);

            if (viewType == null)
                return new TextBlock { Text = $"View not found for {vmType.Name}" };

            var view = (Control)Activator.CreateInstance(viewType)!;
            view.DataContext = data;
            return view;
        }

        public bool Match(object data)
        {
            return data is not null && data.GetType().Name.EndsWith("ViewModel");
        }
    }
}
