using System.Windows;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Views;

public partial class OfflineReadOnlyResourceDialog : Window
{
    public OfflineReadOnlyResourceDialog(OfflineReadOnlyResourceViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        PreviewGraph.Loaded += (_, _) => PreviewGraph.FitAllNodes();
        PreviewGraph.NodeEditRequested += node => viewModel.TryOpenSubgraph(node);
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(viewModel.GraphPreview))
                Dispatcher.BeginInvoke(new Action(() => PreviewGraph.FitAllNodes()));
        };
        Closed += (_, _) => viewModel.Dispose();
    }
}
